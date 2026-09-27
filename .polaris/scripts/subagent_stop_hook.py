"""Claude Code SubagentStop hook - captures a Task-tool subagent's own work.

Called by Claude Code every time a Task-tool-dispatched subagent finishes
(the Agent/Task tool - forks, general-purpose agents, Explore, etc.). A
subagent runs as an isolated nested conversation and never otherwise
triggers the top-level Stop hook, so without this its work - including
genuinely parallel subagent dispatches - is invisible to telemetry
entirely, understating agent-hours for any workflow that leans on
subagent dispatch (autopilot fan-out, parallel review, etc.).

Receives a JSON payload on stdin with session_id (the PARENT session - the
subagent's own transcript carries the same sessionId, confirmed by direct
inspection of local session data) and transcript_path (the SUBAGENT's own
transcript, distinct from the parent's).

Each firing reports only the messages added since the previous one, tracked
by a per-transcript watermark (subagent-progress-<hash>.json). The
transcript Claude Code passes here ACCUMULATES across a session rather than
isolating a single subagent, so summing the whole file on every firing
re-counted every earlier subagent. Production telemetry showed 12 records
sharing one started_at with token totals climbing 1.7M -> 158M: tokens
inflated ~6.7x and agent-hours ~4.6x, because both the sum and the
first/last timestamp span were recomputed over the entire file each time.
Duration is therefore the span of THIS firing's new messages, and token
usage their sum.

Recorded through the SAME pipeline the main Stop hook uses: append a
TelemetryEvent to ~/.polaris/telemetry/events.jsonl (durable local record),
then flush to the configured service - a successful flush archives it, a
failed one leaves it for the next flush to retry. command_category is set
to "subagent" (distinct from "generic" command events and the Stop hook's
"token-usage" cumulative-counter records): the backend's hook-evidence term
sums agent_working_ms across every non-token-usage command event without
session grouping specifically so genuinely parallel work adds up - matching
the project constitution's "parallel agent sessions genuinely sum".

This subagent's span is always a subset of its PARENT turn's own wall-clock
span (the parent is blocked on the Task tool call for at least that long),
so counting both independently would double-count that overlap. To keep
"genuinely parallel work adds up" without double-counting sequential/nested
work, this hook also drops a small per-subagent marker file
(subagent-time-<session_id>-<suffix>.json) that the parent's own Stop hook
(telemetry_hook.py) sums and subtracts from its own agent_working_ms before
recording its event.

Grok CLI loads ~/.claude/settings.json too, so this same script runs there,
against a payload with no transcript_path at all (see _is_grok_payload).
Everything above that depends on a transcript - the watermark, the token
sum, the first/last message span - has nothing to read, so the Grok path
skips it entirely and instead:

- times the run from the CHILD session's own prompt-start breadcrumb
  (prompt_start_hook.py fires inside Grok subagent sessions too) to now,
  falling back to created_at in the child's summary.json;
- takes the model from that summary.json and the subagent type and
  description from the payload and Grok's
  sessions/<cwd>/<parent>/subagents/<child>/meta.json;
- leaves tokens None ("not captured"), because Grok records billed usage
  nowhere a hook can read - never 0, which would assert the run was free;
- records one event per subagent TURN rather than per subagent session: a
  Grok child session can serve several prompts, each with its own
  breadcrumb (see _claim_grok_subagent);
- keys the subagent-time marker by the PARENT session id from that
  meta.json. Under Claude the parent and the subagent share one session id;
  under Grok every subagent gets its own, so without that lookup the
  parent's Stop hook would never find the marker and would double-count the
  span it was blocked for.

Never raises or prints errors - silent failure is intentional.
The agent CLI is never blocked by this script.
"""
import hashlib
import json
import os
import sys
import time
import uuid
from datetime import datetime, timezone
from pathlib import Path

# How long a Grok subagent's "already recorded" claim is kept before it is
# swept. A claim only has to outlive the session it names.
_CLAIM_STALE_HOURS = 24


def _session_id_field(payload: dict) -> str:
    """Read the session id, accepting Grok's camelCase key alongside Claude's.

    Grok CLI auto-loads ~/.claude/settings.json for "Claude Code compatibility"
    (its own hooks doc says so), so this same SubagentStop hook fires there
    too - but Grok's stdin envelope uses camelCase (sessionId), not Claude's
    snake_case (session_id). Without this fallback, session_id resolves
    empty under Grok and main() returns before recording anything.

    SYNC: duplicated verbatim in prompt_start_hook.py / subagent_stop_hook.py /
    telemetry_hook.py because hooks deploy as standalone single-file scripts
    (see the upgrade migrations that copy them into .polaris/scripts/); keep
    the three copies in sync - guarded by
    tests/specify_cli/scripts/test_hook_session_id_parity.py.
    """
    return str(payload.get("session_id") or payload.get("sessionId") or "")


def _is_grok_payload(payload: dict) -> bool:
    """True when this hook was fired by Grok CLI rather than Claude Code.

    Grok loads ~/.claude/settings.json for "Claude Code compatibility", so
    the same scripts run under both, but a Grok SubagentStop carries no
    transcript_path - the field every line of the Claude path below reads -
    so the two have to be told apart before anything else happens.

    Three independent signals, any of which is sufficient - hookEventName
    (present on every Grok payload, absent from Claude's), a camelCase
    sessionId with no snake_case session_id, and the GROK_* variables Grok
    exports into the hook process even if the envelope ever changes.

    SYNC: duplicated verbatim in prompt_start_hook.py / subagent_stop_hook.py /
    telemetry_hook.py because hooks deploy as standalone single-file scripts
    (see the upgrade migrations that copy them into .polaris/scripts/); keep
    the three copies in sync - guarded by
    tests/specify_cli/scripts/test_grok_hook_payloads.py.
    """
    if payload.get("hookEventName"):
        return True
    if payload.get("sessionId") and not payload.get("session_id"):
        return True
    return bool(
        os.environ.get("GROK_SESSION_ID") or os.environ.get("GROK_HOOK_EVENT")
    )


def _grok_home() -> "Path":
    """Root of Grok CLI's on-disk state: $GROK_HOME, else ~/.grok.

    SYNC: duplicated verbatim in telemetry_hook.py - guarded by
    tests/specify_cli/scripts/test_grok_hook_payloads.py.
    """
    env_home = os.environ.get("GROK_HOME", "").strip()
    if env_home:
        return Path(env_home)
    return Path.home() / ".grok"


def _grok_session_dir(session_id: str) -> "Path | None":
    """Locate a Grok session directory by id, or None when not found.

    Grok stores each session under
    <grok home>/sessions/<url-encoded cwd>/<session id>/. The hook payload
    carries the session id but not Grok's encoding of the cwd, so the cwd
    segment is globbed rather than reconstructed. The id itself is checked
    before it reaches the glob so a hostile or malformed one cannot walk out
    of the session store.

    SYNC: duplicated verbatim in telemetry_hook.py - guarded by
    tests/specify_cli/scripts/test_grok_hook_payloads.py.
    """
    if not session_id:
        return None
    if not all(c.isalnum() or c in "-_" for c in session_id):
        return None
    try:
        for summary in _grok_home().glob(f"sessions/*/{session_id}/summary.json"):
            return summary.parent
    except Exception:
        pass
    return None


def _grok_session_summary(session_id: str) -> dict:
    """Read a Grok session's summary.json. Empty dict on any failure.

    For a subagent this is the only readable record of which model actually
    ran (current_model_id) and when the session began (created_at), and its
    agent_name is the subagent type Grok dispatched.

    SYNC: duplicated verbatim in telemetry_hook.py - guarded by
    tests/specify_cli/scripts/test_grok_hook_payloads.py.
    """
    directory = _grok_session_dir(session_id)
    if directory is None:
        return {}
    try:
        data = json.loads(
            (directory / "summary.json").read_text(encoding="utf-8-sig")
        )
    except Exception:
        return {}
    return data if isinstance(data, dict) else {}


def _grok_subagent_meta(child_session_id: str) -> dict:
    """Read the parent-to-child mapping Grok records for a subagent.

    Grok writes <grok home>/sessions/<url-encoded cwd>/<parent session
    id>/subagents/<child session id>/meta.json carrying parent_session_id,
    child_session_id, subagent_type and description. Only the CHILD id
    reaches this hook, so the cwd and parent segments are both globbed.
    Empty dict on any failure - the parent id it yields is the difference
    between the parent Stop hook finding this subagent's time marker and
    double-counting the span it was blocked for, so it is worth the glob.
    """
    if not child_session_id:
        return {}
    if not all(c.isalnum() or c in "-_" for c in child_session_id):
        return {}
    try:
        pattern = f"sessions/*/*/subagents/{child_session_id}/meta.json"
        for path in _grok_home().glob(pattern):
            try:
                data = json.loads(path.read_text(encoding="utf-8-sig"))
            except Exception:
                continue
            if isinstance(data, dict):
                return data
    except Exception:
        pass
    return {}


def _parse_iso(value: str) -> "datetime | None":
    """Parse an ISO-8601 timestamp, treating a naive one as UTC.

    Grok writes a trailing Z and nanosecond precision; Claude transcripts
    write offsets. None on anything unparseable.
    """
    if not value:
        return None
    try:
        parsed = datetime.fromisoformat(value.replace("Z", "+00:00"))
    except Exception:
        return None
    if parsed.tzinfo is None:
        parsed = parsed.replace(tzinfo=timezone.utc)
    return parsed


def _read_prompt_start_dt(session_id: str) -> "datetime | None":
    """Start time from the prompt-start breadcrumb for *session_id*.

    prompt_start_hook.py writes one per submitted prompt, and it fires
    inside Grok subagent sessions too - each of which has its own session id
    - which makes it the only clock a Grok subagent run can be measured
    from. A narrowed copy of telemetry_hook._read_prompt_start, which also
    returns the cwd and the raw string that only the Stop hook needs.
    """
    if not session_id:
        return None
    try:
        path = (
            Path.home()
            / ".polaris"
            / "telemetry"
            / f"prompt-start-{session_id}.json"
        )
        data = json.loads(path.read_text(encoding="utf-8"))
    except Exception:
        return None
    return _parse_iso(str(data.get("started_at") or ""))


def _consume_prompt_start(session_id: str) -> None:
    """Delete a subagent's own prompt-start breadcrumb once it is recorded.

    Left behind, it would hand a later turn in that same session id a start
    time from this run and inflate its duration - the same reason the Stop
    hook consumes the parent's.
    """
    if not session_id:
        return
    try:
        (
            Path.home()
            / ".polaris"
            / "telemetry"
            / f"prompt-start-{session_id}.json"
        ).unlink()
    except Exception:
        pass


def _claim_grok_subagent(
    child_session_id: str, start_iso: str, timed_from_breadcrumb: bool
) -> bool:
    """Claim the telemetry record for ONE Grok subagent turn.

    True for the first caller for that turn, False for every later one. The
    Claude path is kept honest by its per-transcript watermark; Grok has no
    transcript, so an exclusive marker file plays that role. Without it a
    second SubagentStop for the same run - by then with its breadcrumb
    consumed - would fall back to summary.json's created_at and record the
    whole run a second time, the exact double-count the watermark exists to
    prevent.

    The claim is keyed by *start_iso* (the run's start time) rather than by
    the session id alone, because a Grok subagent session can serve several
    turns: Grok fires UserPromptSubmit inside it for each one, so each turn
    has its own start time and deserves its own event. One claim per session
    would have recorded only the first turn and silently dropped the rest,
    undercounting exactly the long-lived children that do the most work.

    *timed_from_breadcrumb* is what keeps that from reopening the double-count
    it replaced. A caller timed from this turn's own prompt-start breadcrumb
    has a start time unique to the turn, so a fresh key means a genuinely new
    turn. A caller that fell back to summary.json's created_at does NOT: that
    value is fixed for the whole session, and the fallback is reached
    precisely because an earlier firing already consumed the breadcrumb. Such
    a caller is refused outright once ANY claim exists for the child, since it
    can only be a repeat of a run already recorded.

    Sweeps its own claims after 24h: the CLI's breadcrumb cleanup does not
    know this filename, and a claim outlives its usefulness the moment the
    session it names is over.
    """
    if not child_session_id:
        return False
    safe = "".join(
        c for c in child_session_id if c.isalnum() or c in "-_"
    )[:64]
    if not safe:
        return False

    telemetry_dir = Path.home() / ".polaris" / "telemetry"
    try:
        telemetry_dir.mkdir(parents=True, exist_ok=True)
    except Exception:
        return False

    try:
        cutoff = time.time() - _CLAIM_STALE_HOURS * 3600
        for stale in telemetry_dir.glob("subagent-done-*.json"):
            try:
                if stale.stat().st_mtime < cutoff:
                    stale.unlink()
            except Exception:
                pass
    except Exception:
        pass

    if not timed_from_breadcrumb:
        # No per-turn clock, so this firing cannot be told apart from one
        # already recorded for this child. Record it only when nothing has
        # been recorded for the child at all (a run whose UserPromptSubmit
        # never fired). The bare legacy name is checked too, so a claim
        # written by an earlier build still suppresses a repeat.
        try:
            claimed = list(telemetry_dir.glob(f"subagent-done-{safe}-*.json"))
            if (telemetry_dir / f"subagent-done-{safe}.json").exists():
                claimed.append(telemetry_dir / f"subagent-done-{safe}.json")
            if claimed:
                return False
        except Exception:
            return False

    turn_key = hashlib.sha256(
        (start_iso or "").encode("utf-8")
    ).hexdigest()[:16]
    try:
        fd = os.open(
            str(telemetry_dir / f"subagent-done-{safe}-{turn_key}.json"),
            os.O_CREAT | os.O_EXCL | os.O_WRONLY,
        )
        os.close(fd)
    except Exception:
        return False
    return True


def _git_config_value(work_dir: str, key: str) -> str:
    if not work_dir:
        return ""
    try:
        import subprocess as _sp
        result = _sp.run(
            ["git", "config", "--get", key],
            capture_output=True, text=True, timeout=3, cwd=work_dir,
        )
        if result.returncode == 0:
            return result.stdout.strip()
    except Exception:
        pass
    return ""


def _git_user_email(work_dir: str) -> str:
    value = _git_config_value(work_dir, "user.email").strip().lower()
    return value if "@" in value else ""


def _sso_email() -> str:
    """Entra SSO signed-in email, or "" when unavailable.

    Prefers the shared resolver in specify_cli.telemetry so the hooks and the
    CLI can never disagree on precedence; falls back to reading the auth store
    directly when this hook runs under an interpreter that cannot import the
    package. Both routes go through store.read_session_meta because the file
    is DPAPI-protected and a raw read yields ciphertext.

    Never raises: no session, no module and no auth dir all mean "no SSO
    identity", and telemetry must not break over any of them.
    """
    try:
        from specify_cli.telemetry import _detect_sso_email

        return _detect_sso_email()
    except Exception:
        pass
    try:
        from specify_cli.auth import store

        value = str(store.read_session_meta().get("email") or "").strip().lower()
        return value if "@" in value else ""
    except Exception:
        return ""


def _agent_email() -> str:
    """Agent (Claude/Copilot) account email, or "" when undetectable.

    Delegates to the shared detector so this hook honours the same precedence
    and the same on-disk week-long cache as the CLI, instead of paying the
    ~1.7s `claude auth status` spawn once per subagent turn.

    Distinct from _sso_email: this identifies the AI vendor account, not the
    human. Never raises.
    """
    try:
        from specify_cli.telemetry import _detect_agent_email

        return _detect_agent_email()
    except Exception:
        return ""


def _read_last_command_context(min_valid_ts: "datetime | None") -> tuple[str, str, str]:
    """Read (project, user_email, feature_slug) from last-command.json.

    Written by the parent session's own CLI invocation - a subagent
    completes mid-turn, before the parent's Stop hook consumes this file,
    so it is expected to still be present and valid.

    This file is a single machine-global path shared by every polaris
    invocation on the machine, not scoped to this subagent's own session or
    project. If it was modified any time after this subagent was already
    dispatched (min_valid_ts - the subagent's own transcript start), some
    *other* command - a different terminal/project on the same machine, or
    a later command in the same turn - has since clobbered it, and
    trusting it would mis-attribute this subagent's event to the wrong
    project/feature/user.
    """
    try:
        path = Path.home() / ".polaris" / "telemetry" / "last-command.json"
        if not path.exists():
            return "", "", ""
        if min_valid_ts is not None:
            try:
                mtime = datetime.fromtimestamp(path.stat().st_mtime, tz=timezone.utc)
                if mtime > min_valid_ts:
                    return "", "", ""
            except Exception:
                pass
        data = json.loads(path.read_text(encoding="utf-8"))
        return (
            str(data.get("project") or ""),
            str(data.get("git_user_email") or "").strip().lower(),
            str(data.get("feature_slug") or ""),
        )
    except Exception:
        return "", "", ""


def _read_subagent_messages(transcript_path: str) -> list:
    """Every assistant message in the transcript, oldest first.

    Returns a list of ``(timestamp, effective_in, out, cache_read,
    cache_creation, model_id)``. The caller slices off the ones an earlier
    firing already counted - see _claim_new_messages for why that matters.

    A message with no usage block is kept, not skipped: it still carries a
    timestamp, and the reported span is the min/max across these rows. A
    subagent that produced no billable usage is still a subagent that took
    time, and dropping those rows would both lose that span and let the
    watermark drift out of step with the file. Never raises.
    """
    if not transcript_path:
        return []
    try:
        path = Path(transcript_path)
        if not path.exists():
            return []
        lines = path.read_text(encoding="utf-8-sig").splitlines()
    except Exception:
        return []

    out_rows = []
    for line in lines:
        stripped = line.strip()
        if not stripped:
            continue
        try:
            obj = json.loads(stripped)
        except Exception:
            continue
        if obj.get("type") != "assistant":
            continue
        msg = obj.get("message") or {}
        usage = msg.get("usage") or {}
        inp = int(usage.get("input_tokens") or 0)
        out = int(usage.get("output_tokens") or 0)
        cache_read = int(usage.get("cache_read_input_tokens") or 0)
        cache_creation = int(usage.get("cache_creation_input_tokens") or 0)
        ts = None
        raw_ts = obj.get("timestamp")
        if raw_ts:
            try:
                ts = datetime.fromisoformat(str(raw_ts).replace("Z", "+00:00"))
                if ts.tzinfo is None:
                    ts = ts.replace(tzinfo=timezone.utc)
            except Exception:
                ts = None

        out_rows.append(
            (
                ts,
                inp + cache_read + cache_creation,
                out,
                cache_read,
                cache_creation,
                str(msg.get("model") or ""),
            )
        )
    return out_rows


def _aggregate_messages(rows: list):
    """Fold message rows into the totals one telemetry event reports.

    Returns ``(first_ts, last_ts, tokens_in, tokens_out, cache_read,
    cache_creation, model_id)``. Timestamps are the min/max across the rows
    (not the first/last element) so a line appended out of order cannot
    under-report the span.
    """
    first_ts = last_ts = None
    tokens_in = tokens_out = cache_read = cache_creation = 0
    model_id = ""
    for ts, eff_in, out, c_read, c_create, model in rows:
        if ts is not None:
            if first_ts is None or ts < first_ts:
                first_ts = ts
            if last_ts is None or ts > last_ts:
                last_ts = ts
        tokens_in += eff_in
        tokens_out += out
        cache_read += c_read
        cache_creation += c_create
        if not model_id and model:
            model_id = model
    return (
        first_ts,
        last_ts,
        tokens_in,
        tokens_out,
        cache_read,
        cache_creation,
        model_id,
    )


def _progress_path(transcript_path: str) -> "Path":
    """Per-transcript watermark file recording what has already been counted."""
    digest = hashlib.sha256(
        str(Path(transcript_path).resolve()).encode("utf-8", "replace")
    ).hexdigest()[:16]
    return Path.home() / ".polaris" / "telemetry" / f"subagent-progress-{digest}.json"


def _claim_new_messages(transcript_path: str, total_seen: int) -> "int | None":
    """Reserve the assistant messages not yet counted for this transcript.

    Returns how many messages were already accounted for by earlier firings,
    or None when another process holds the lock (in which case this firing
    records nothing rather than risk double-counting).

    Claude Code hands SubagentStop a transcript that ACCUMULATES across a
    session rather than one isolated per-subagent file, so summing the whole
    file on every firing re-counts every earlier subagent. Observed in
    production telemetry: 12 records sharing one started_at with token counts
    climbing 1.7M -> 158M, inflating both tokens and agent-hours ~6x. The
    watermark makes each firing report only its own delta.

    The read-modify-write is guarded because genuinely parallel subagents can
    finish within milliseconds of each other, and two firings that both read
    the same watermark would each claim the same messages.
    """
    path = _progress_path(transcript_path)
    lock = path.with_suffix(".lock")
    try:
        path.parent.mkdir(parents=True, exist_ok=True)
        # An abandoned lock (owner crashed mid-update) must not wedge every
        # later firing, so treat a stale one as free.
        try:
            if time.time() - lock.stat().st_mtime > 30:
                lock.unlink()
        except Exception:
            pass
        try:
            fd = os.open(str(lock), os.O_CREAT | os.O_EXCL | os.O_WRONLY)
            os.close(fd)
        except Exception:
            return None

        try:
            previous = 0
            try:
                previous = int(json.loads(path.read_text(encoding="utf-8")).get("counted") or 0)
            except Exception:
                previous = 0
            if previous > total_seen:
                # The transcript shrank or was replaced; restart from zero
                # rather than silently reporting nothing forever.
                previous = 0
            tmp = path.with_suffix(f".json.tmp-{os.getpid()}")
            tmp.write_text(json.dumps({"counted": total_seen}), encoding="utf-8")
            os.replace(tmp, path)
            return previous
        finally:
            try:
                lock.unlink()
            except Exception:
                pass
    except Exception:
        return None


def _write_subagent_time_marker(session_id: str, duration_ms: int) -> None:
    """Record this subagent's duration for the parent turn's Stop hook.

    One file per subagent (a unique pid+random suffix, matching the
    fallback-token-file pattern elsewhere in this pipeline) rather than a
    single shared counter, so multiple subagents finishing around the same
    time - the common case for genuinely parallel dispatch - never lose an
    update to a read-modify-write race. The parent Stop hook sums and
    deletes every marker for its session.
    """
    if not session_id or duration_ms <= 0:
        return
    try:
        telemetry_dir = Path.home() / ".polaris" / "telemetry"
        telemetry_dir.mkdir(parents=True, exist_ok=True)
        suffix = f"{os.getpid()}-{uuid.uuid4().hex[:8]}"
        marker = telemetry_dir / f"subagent-time-{session_id}-{suffix}.json"
        tmp = marker.with_suffix(".json.tmp")
        tmp.write_text(json.dumps({"duration_ms": duration_ms}), encoding="utf-8")
        os.replace(tmp, marker)
    except Exception:
        pass


def _read_subagent_meta(transcript_path: str) -> tuple[str, str]:
    """Read (agent_type, description) from the sibling <name>.meta.json file
    Claude Code writes next to a subagent transcript. Empty strings on any
    failure - this is best-effort labeling, never load-bearing."""
    try:
        path = Path(transcript_path)
        meta_path = path.with_suffix("").with_suffix(".meta.json")
        if not meta_path.exists():
            return "", ""
        data = json.loads(meta_path.read_text(encoding="utf-8"))
        return str(data.get("agentType") or ""), str(data.get("description") or "")
    except Exception:
        return "", ""


def _record_subagent_event(
    payload: dict,
    session_id: str,
    marker_session_id: str,
    first_ts: "datetime",
    last_ts: "datetime",
    duration_ms: int,
    args: list,
    tokens_in: "int | None",
    tokens_out: "int | None",
    cache_read: "int | None",
    cache_creation: "int | None",
    model_id: str,
    agent_name: str = "",
) -> None:
    """Append one subagent event and drop the parent turn's time marker.

    Shared by both paths so a Grok subagent lands in exactly the same shape
    a Claude one does - same command, same "subagent" category, same
    duration_ms == agent_working_ms convention - and the backend's
    hook-evidence term sums them together without knowing which CLI ran.

    *marker_session_id* is the session the PARENT Stop hook will look the
    time marker up under: the same id for Claude (parent and subagent share
    one), the parent_session_id from Grok's meta.json for Grok. Empty when
    the parent is unknown, which writes no marker at all - the parent globs
    only its own id, so a marker it cannot match is dead weight, and this
    hook records nothing rather than mis-attributing.

    None token values mean "not captured" and are passed through rather than
    flattened to 0, which would assert the subagent ran for free.
    """
    work_dir = str(payload.get("cwd") or payload.get("workspaceRoot") or "")
    project, user_email, feature_slug = _read_last_command_context(first_ts)
    # Captured before the SSO override below reassigns user_email. This is a
    # GIT address: _read_last_command_context reads it from
    # last-command.json's git_user_email, written by the parent session's own
    # CLI invocation. A subagent frequently runs where `git config
    # user.email` is not resolvable, so this is the only way its event keeps
    # a git identity - but it must never pick up the SSO address, which is
    # what reading user_email after the override would do.
    _last_command_git_email = user_email
    if not project and work_dir:
        try:
            project = Path(work_dir).name
        except Exception:
            pass
    # SSO identity outranks both last-command.json and git: it is the account
    # the user signed into, bound to a tenant-issued token.
    _sso = _sso_email()
    if _sso:
        user_email = _sso
    elif not user_email:
        user_email = _git_user_email(work_dir)

    try:
        hostname = __import__("socket").gethostname()
    except Exception:
        hostname = ""

    version = ""
    try:
        from specify_cli import __version__ as version
    except Exception:
        pass

    # Recorded through the same durable local pipeline the main Stop hook
    # uses (events.jsonl append + immediate flush attempt). If specify_cli
    # itself is not importable there is no local-durability fallback to
    # degrade to here (unlike the main Stop hook's legacy path) - this is
    # an edge case rare enough (same interpreter that runs every other
    # Polaris hook) not to warrant duplicating that whole mechanism.
    try:
        from specify_cli import telemetry
    except Exception:
        return

    # Project identity (spec 054) - reuse the same helpers every other
    # TelemetryEvent call site uses so subagent/autopilot-fan-out events
    # roll up to the right project instead of an undifferentiated null
    # bucket in per-project cost/agent-hours reporting.
    project_root = Path(work_dir) if work_dir else None
    project_id = ""
    project_version = ""
    git_remote_url = ""
    try:
        project_id, project_version, git_remote_url = telemetry.project_identity(
            project_root
        )
    except Exception:
        pass

    _total_s = duration_ms // 1000
    _mins = _total_s // 60
    _secs = _total_s % 60
    event_time = f"{_mins}m {_secs}s" if _mins > 0 else f"{_secs}s"

    if tokens_in is None and tokens_out is None:
        tokens_total = None
    else:
        tokens_total = (tokens_in or 0) + (tokens_out or 0)

    try:
        event = telemetry.TelemetryEvent(
            command="subagent",
            args=args,
            git_user_name=_git_config_value(work_dir, "user.name"),
            # Git identity only. The fallback is the git address recorded
            # in last-command.json, NOT user_email: by this point user_email
            # holds the SSO address whenever a session exists, so `or
            # user_email` conflated the two sources for any user with no git
            # user.email set - the exact thing this field is separate from
            # sso_email in order to avoid.
            git_user_email=_git_user_email(work_dir) or _last_command_git_email,
            sso_email=_sso,
            # Agent login identity: this hook previously omitted the field
            # entirely, which is why every command="subagent" event landed
            # with an empty agent_email while CLI events carried one.
            agent_email=_agent_email(),
            timestamp=last_ts.isoformat(),
            duration_ms=duration_ms,
            success=True,
            exit_code=0,
            feature_slug=feature_slug,
            polaris_version=version,
            hostname=hostname,
            project=project,
            project_id=project_id,
            project_version=project_version,
            git_remote_url=git_remote_url,
            session_id=session_id,
            session_seq=1,
            started_at=first_ts.isoformat(),
            completed_at=last_ts.isoformat(),
            command_category="subagent",
            tokens_in=tokens_in,
            tokens_out=tokens_out,
            tokens_total=tokens_total,
            # Components of tokens_in, not additions to it. Local-only:
            # this event reaches the service via _to_service_event's
            # allowlist, which does not carry these fields.
            cache_read_tokens=cache_read,
            cache_creation_tokens=cache_creation,
            model=model_id,
            agent_name=agent_name,
            agent_working_ms=duration_ms,
            event_time=event_time,
        )
        telemetry._append_event(event)
    except Exception:
        return

    # Record this subagent's duration for the parent turn's Stop hook to
    # subtract from its own agent_working_ms. The parent's turn spans the
    # ENTIRE prompt (including the time it was blocked on this subagent),
    # so without this, the same wall-clock span is double-counted: once
    # here, once again in the parent's own turn-level event.
    _write_subagent_time_marker(marker_session_id, duration_ms)

    try:
        telemetry.flush_to_service()
    except Exception:
        pass


def _record_grok_subagent(payload: dict, child_session_id: str) -> None:
    """Record a Grok subagent run, which arrives with no transcript at all.

    Grok gives every subagent its own session id and writes no per-subagent
    transcript, so neither the watermark nor the token summation below
    applies. The run is timed from the child's own prompt-start breadcrumb
    (prompt_start_hook.py fires inside subagent sessions too) to now,
    falling back to created_at in the child's summary.json, and labelled
    from that summary plus Grok's subagents/<child>/meta.json.
    """
    summary = _grok_session_summary(child_session_id)
    started_dt = _read_prompt_start_dt(child_session_id)
    # Whether this run has a clock of its OWN (this turn's breadcrumb) or only
    # the session-wide created_at decides whether a fresh claim may record it
    # - see _claim_grok_subagent.
    timed_from_breadcrumb = started_dt is not None
    if started_dt is None:
        started_dt = _parse_iso(str(summary.get("created_at") or ""))
    if started_dt is None:
        # Nothing left to time this run by - typically a repeat firing whose
        # breadcrumb an earlier one already consumed, with Grok's session
        # store unreadable. Recording now would invent a span.
        return

    now = datetime.now(timezone.utc)
    duration_ms = max(0, int((now - started_dt).total_seconds() * 1000))
    if duration_ms <= 0:
        # No tokens are readable under Grok, so a zero-length span would be
        # a record of nothing at all.
        return

    # Claimed BEFORE the breadcrumb is consumed so a repeat firing that
    # falls back to summary.json's created_at still stops here.
    if not _claim_grok_subagent(
        child_session_id, started_dt.isoformat(), timed_from_breadcrumb
    ):
        return
    _consume_prompt_start(child_session_id)

    meta = _grok_subagent_meta(child_session_id)
    agent_type = str(
        payload.get("subagentType")
        or meta.get("subagent_type")
        or summary.get("agent_name")
        or ""
    )
    description = str(meta.get("description") or "")
    # No fallback to the child's own id: the parent only ever globs
    # subagent-time-<its own id>-*.json, so a marker filed under the child
    # could never be consumed - dead weight until the 24h sweep, and a
    # mis-attribution if that id is ever a parent elsewhere. With the parent
    # unknown the run is still recorded; only the subtraction is skipped
    # (an empty id makes _write_subagent_time_marker a no-op).
    parent_session_id = str(meta.get("parent_session_id") or "")

    _record_subagent_event(
        payload=payload,
        session_id=child_session_id,
        marker_session_id=parent_session_id,
        first_ts=started_dt,
        last_ts=now,
        duration_ms=duration_ms,
        args=[v for v in (agent_type, description) if v],
        tokens_in=None,
        tokens_out=None,
        cache_read=None,
        cache_creation=None,
        model_id=str(summary.get("current_model_id") or ""),
        agent_name="grok",
    )


def main() -> None:
    try:
        raw = sys.stdin.buffer.read().decode("utf-8-sig")
        payload = json.loads(raw)
    except Exception:
        return

    session_id = _session_id_field(payload)
    if not session_id:
        return

    transcript_path = str(payload.get("transcript_path") or "")
    if not transcript_path:
        # Grok's SubagentStop payload has no transcript field at all, so
        # everything below has nothing to read; its session store and the
        # prompt-start breadcrumb carry enough to record the run instead.
        # Any other transcript-less payload still has nothing to report.
        if _is_grok_payload(payload):
            _record_grok_subagent(payload, session_id)
        return

    # Count only what this firing added. Claude Code's SubagentStop
    # transcript accumulates across a session rather than isolating one
    # subagent, so summing the whole file every time re-counts every earlier
    # subagent - the cause of records sharing one started_at with totals
    # climbing 1.7M -> 158M and agent-hours inflated ~4.6x.
    all_rows = _read_subagent_messages(transcript_path)
    if not all_rows:
        return
    already_counted = _claim_new_messages(transcript_path, len(all_rows))
    if already_counted is None:
        # A concurrent firing holds the watermark; recording now would
        # double-count the same messages.
        return
    rows = all_rows[already_counted:]
    if not rows:
        return

    (
        first_ts,
        last_ts,
        tokens_in,
        tokens_out,
        cache_read,
        cache_creation,
        model_id,
    ) = _aggregate_messages(rows)
    if first_ts is None or last_ts is None:
        return

    try:
        duration_ms = max(0, int((last_ts - first_ts).total_seconds() * 1000))
    except Exception:
        duration_ms = 0
    # A subagent that never produced a single timestamped line either way
    # has nothing meaningful to report.
    if duration_ms == 0 and (tokens_in + tokens_out) == 0:
        return

    agent_type, description = _read_subagent_meta(transcript_path)

    # Under Claude the parent and the subagent share one session id, so the
    # parent finds the time marker under the id that arrived on this payload.
    _record_subagent_event(
        payload=payload,
        session_id=session_id,
        marker_session_id=session_id,
        first_ts=first_ts,
        last_ts=last_ts,
        duration_ms=duration_ms,
        args=[v for v in (agent_type, description) if v],
        tokens_in=tokens_in,
        tokens_out=tokens_out,
        cache_read=cache_read,
        cache_creation=cache_creation,
        model_id=model_id,
    )


if __name__ == "__main__":
    main()
