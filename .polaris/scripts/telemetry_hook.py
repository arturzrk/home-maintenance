"""Claude Code / Grok CLI Stop hook - captures each agent turn.

Called after each agent turn completes.
Receives a JSON payload on stdin with session_id and transcript_path.

The Stop hook payload does NOT include usage data. Token counts are
read from the last assistant message in the transcript file instead.

Grok CLI loads ~/.claude/settings.json too, so this same script runs there,
and its payload is shaped differently (see _is_grok_payload):

- It is camelCase (sessionId, workspaceRoot) and carries no transcript path
  and no model, so no token counts are readable. Grok records billed usage
  nowhere a hook can reach: chat_history.jsonl has no usage blocks and
  updates.jsonl's _meta.totalTokens is the context-window size, not what the
  turn cost. tokens_in/out/total are therefore left None ("not captured"),
  never 0, which would claim the turn was free.
- The model is read from Grok's own session store instead
  (<grok home>/sessions/<url-encoded cwd>/<session id>/summary.json).
- Duration is unaffected: it comes from the prompt-start breadcrumb, exactly
  as it does under Claude.
- Grok fires Stop twice per session - once at the real turn end
  (reason "end_turn") and once at session teardown ("channel_closed" /
  "shutdown"). Only the end_turn fire is a turn; the rest return before
  touching any turn state.

Every turn is captured: when a polaris command ran during the turn the
event carries that command; when the user prompted Claude directly the
event is captured with the fallback command "implement". Turn duration
(event_time / agent_working_ms) is measured from the prompt-start
breadcrumb written by prompt_start_hook.py (UserPromptSubmit hook).

The event is recorded through the SAME pipeline the CLI uses: append to
~/.polaris/telemetry/events.jsonl (durable local record, readable by
`polaris cost`), then flush to the configured service - a successful flush
moves the sent lines into ~/.polaris/telemetry/archive/. If the flush
fails the event stays in events.jsonl and the next flush retries it.
Only when the specify_cli package itself is not importable does the hook
fall back to the legacy direct-push / token-file path.

After recording, the turn's breadcrumbs (prompt-start-<sid>.json and
last-command.json) are consumed so stale state from this turn can never
mislabel a later one.

Never raises or prints errors - silent failure is intentional.
The CLI is never blocked by this script.
"""
import json
import os
import sys
import time
from datetime import datetime, timedelta, timezone
from pathlib import Path

# This script must stay standalone (it defines its own helpers rather than
# importing specify_cli.telemetry), so it keeps its own copy of the sentinel
# email used when no real identity is resolvable. Keep the VALUE identical to
# specify_cli.telemetry.UNKNOWN_USER_EMAIL - both feed the same service and a
# mismatch would split the DB's "unknown" bucket.
UNKNOWN_USER_EMAIL = "unknown@unknown.com"


def _read_service_config() -> tuple[str, str]:
    """Read service_url and api_key from ~/.polaris/config.yaml."""
    config_path = Path.home() / ".polaris" / "config.yaml"
    if not config_path.exists():
        return "", ""
    try:
        import yaml
        config = yaml.safe_load(config_path.read_text(encoding="utf-8")) or {}
        telemetry = config.get("telemetry", {})
        if not isinstance(telemetry, dict):
            return "", ""
        return telemetry.get("service_url", ""), telemetry.get("api_key", "")
    except Exception:
        return "", ""


def _post_json(endpoint: str, payload_obj, api_key: str) -> bool:
    """POST a JSON payload. Returns True on 2xx, False on any failure."""
    try:
        import urllib.request
        payload = json.dumps(payload_obj).encode("utf-8")
        headers = {
            "Content-Type": "application/json",
            "X-API-Key": api_key,
        }
        req = urllib.request.Request(endpoint, data=payload, headers=headers, method="POST")
        with urllib.request.urlopen(req, timeout=5) as resp:
            return resp.status < 300
    except Exception:
        return False


def _to_event_record(token_data: dict) -> dict:
    """Map the hook's token record to the /events/batch API schema.

    Deployed telemetry services may predate the /tokens/batch endpoint;
    /events/batch is the original ingest route every service version has.
    Mirrors telemetry._to_service_event so the turn appears in the events
    table immediately with command, token counts, and event_time.
    """
    import socket
    import uuid

    raw_sid = str(token_data.get("session_id") or "")
    try:
        sid = str(uuid.UUID(raw_sid))
    except (ValueError, AttributeError, TypeError):
        sid = str(uuid.uuid5(uuid.NAMESPACE_DNS, raw_sid or uuid.uuid4().hex))

    awm = token_data.get("agent_working_ms") or 0
    try:
        awm = int(awm)
    except (TypeError, ValueError):
        awm = 0
    if awm < 0 or awm > 24 * 3600 * 1000:
        awm = 0

    try:
        hostname = socket.gethostname()
    except Exception:
        hostname = ""

    version = ""
    try:
        from specify_cli import __version__ as version
    except Exception:
        pass

    try:
        seq = int(token_data.get("session_seq") or 0)
    except (TypeError, ValueError):
        seq = 0

    # Deployed services validate user_email as a REQUIRED string that must
    # contain "@" - both "" and None are rejected outright. Mirrors
    # telemetry._to_service_event's fallback for the same /events/batch
    # endpoint when no real git identity is resolvable. Normalized
    # (trimmed + lowercased) so identity matching is case-insensitive.
    raw_email = (token_data.get("user_email") or "").strip().lower()
    user_email = raw_email if "@" in raw_email else UNKNOWN_USER_EMAIL

    return {
        "event_id": str(uuid.uuid4()),
        "command": token_data.get("command") or None,
        "args": {},
        # Deployed services validate user_name as str (not Optional) and
        # reject the whole event when it is null.
        "user_name": token_data.get("user_name") or "",
        "user_email": user_email,
        "hostname": hostname or None,
        "project": token_data.get("project") or None,
        "feature_slug": token_data.get("feature_slug") or None,
        "polaris_version": version or None,
        "duration_ms": awm,
        "success": True,
        "exit_code": 0,
        "session_id": sid,
        "session_seq": seq if seq >= 1 else 1,
        "session_elapsed_ms": 0,
        "occurred_at": token_data.get("occurred_at"),
        "command_category": "generic",
        "tokens_in": token_data.get("tokens_in") or None,
        "tokens_out": token_data.get("tokens_out") or None,
        "tokens_total": token_data.get("tokens_total") or None,
        "model": token_data.get("model") or None,
        "agent_working_ms": token_data.get("agent_working_ms") or None,
        "agent_email": token_data.get("agent_email") or None,
        "event_time": token_data.get("event_time") or None,
    }


def _push_to_service(token_data: dict, service_url: str, api_key: str) -> bool:
    """Push the turn record to the service. Returns True on success.

    Tries /tokens/batch first (newer services), then falls back to
    /events/batch (every service version) so the turn is recorded
    IMMEDIATELY at turn end instead of waiting for a later polaris CLI
    invocation to sweep a local fallback file.
    """
    base = service_url.rstrip("/")
    if _post_json(f"{base}/tokens/batch", [token_data], api_key):
        return True
    return _post_json(f"{base}/events/batch", [_to_event_record(token_data)], api_key)


def _write_fallback_file(token_data: dict, session_id: str, session_seq: int) -> None:
    """Write token data to a local file as fallback for next CLI sweep.

    The filename must be unique per hook fire. A millisecond timestamp alone
    still collides when two turns (or parallel CLI instances) fire within the
    same millisecond, and two processes writing the same path can interleave
    into a corrupt half-record. Adding the pid and a short random token makes
    collisions effectively impossible, and the write goes through a temp file
    + atomic rename so a reader never sees a partially written record.
    """
    try:
        import os
        import time
        import uuid
        token_dir = Path.home() / ".polaris" / "telemetry"
        token_dir.mkdir(parents=True, exist_ok=True)
        suffix = f"{int(time.time() * 1000)}-{os.getpid()}-{uuid.uuid4().hex[:8]}"
        token_file = token_dir / f"token-{session_id}-{session_seq}-{suffix}.json"
        tmp = token_file.with_suffix(".json.tmp")
        tmp.write_text(json.dumps(token_data), encoding="utf-8")
        os.replace(tmp, token_file)
    except Exception:
        pass


# Shares the on-disk cache written by specify_cli.telemetry so the ~1.7s
# `claude auth status` spawn is paid at most once a week per machine rather
# than on every hook invocation (this hook runs per agent turn). Kept as a
# standalone copy because this script must not import specify_cli.
_AGENT_EMAIL_CACHE_TTL_S = 7 * 24 * 3600
_AGENT_EMAIL_NEGATIVE_TTL_S = 3600


def _agent_email_cache_path() -> Path:
    return Path.home() / ".polaris" / "agent-email.json"


def _load_cached_agent_email() -> str | None:
    """Return cached email, or None when absent/stale/unreadable."""
    try:
        path = _agent_email_cache_path()
        if not path.exists():
            return None
        data = json.loads(path.read_text(encoding="utf-8"))
        if not isinstance(data, dict):
            # A truncated or hand-edited cache can parse as a list or null,
            # and .get would then raise AttributeError - which is not in the
            # caught tuple below, so it would escape into the finally block
            # that records telemetry and take the whole command down.
            return None
        email = data.get("email", "")
        if not isinstance(email, str):
            return None
        # An empty result means the lookup ran and found nothing, which goes
        # stale far sooner than a real address: the user is one `claude
        # /login` away from having one. A week of negative caching would
        # leave their events unattributed for that whole week.
        ttl = _AGENT_EMAIL_CACHE_TTL_S if email else _AGENT_EMAIL_NEGATIVE_TTL_S
        if time.time() - float(data.get("cached_at", 0)) > ttl:
            return None
        return email
    except (OSError, ValueError, TypeError):
        return None


def _save_cached_agent_email(email: str) -> None:
    """Persist the resolved email atomically. Never raises."""
    try:
        path = _agent_email_cache_path()
        path.parent.mkdir(parents=True, exist_ok=True)
        tmp = path.with_name(f"{path.name}.tmp-{os.getpid()}")
        tmp.write_text(
            json.dumps({"email": email, "cached_at": time.time()}), encoding="utf-8"
        )
        os.replace(tmp, path)
    except OSError:
        pass


def _detect_agent_email() -> str:
    """Detect the logged-in agent email (Claude/Copilot account).

    Priority:
    1. POLARIS_AGENT_EMAIL env var
    2. ~/.polaris/config.yaml telemetry.agent_email (set via polaris config set-telemetry)
    3. claude auth status --json
    4. ~/.claude/settings.json
    5. gh api user --jq .email
    Steps 3-5 spawn subprocesses, so their result is cached on disk
    (~/.polaris/agent-email.json) for a week; steps 1-2 always win over it.

    Returns empty string on any failure. Never raises.
    """
    env_email = os.environ.get("POLARIS_AGENT_EMAIL", "").strip()
    if env_email and "@" in env_email:
        return env_email

    try:
        import yaml
        config_path = Path.home() / ".polaris" / "config.yaml"
        if config_path.exists():
            config = yaml.safe_load(config_path.read_text(encoding="utf-8")) or {}
            email = (config.get("telemetry") or {}).get("agent_email", "")
            if email and "@" in str(email):
                return str(email).strip()
    except Exception:
        pass

    cached = _load_cached_agent_email()
    if cached is not None:
        return cached

    try:
        import subprocess as _sp
        result = _sp.run(
            ["claude", "auth", "status", "--json"],
            capture_output=True, text=True, timeout=5,
        )
        if result.returncode == 0:
            data = json.loads(result.stdout)
            email = data.get("email", "")
            if email and "@" in email:
                _save_cached_agent_email(email)
                return email
    except Exception:
        pass

    try:
        settings = Path.home() / ".claude" / "settings.json"
        if settings.exists():
            data = json.loads(settings.read_text(encoding="utf-8"))
            email = (
                data.get("emailAddress")
                or data.get("email")
                or (data.get("oauthAccount") or {}).get("emailAddress")
                or (data.get("oauthAccount") or {}).get("email")
                or ""
            )
            if email and "@" in str(email):
                _save_cached_agent_email(str(email).strip())
                return str(email).strip()
    except Exception:
        pass

    try:
        import subprocess as _sp
        result = _sp.run(
            ["gh", "api", "user", "--jq", ".email"],
            capture_output=True, text=True, timeout=3,
        )
        if result.returncode == 0:
            email = result.stdout.strip()
            if email and "@" in email and email != "null":
                _save_cached_agent_email(email)
                return email
    except Exception:
        pass

    # Cache the negative result so a machine with no detectable email does not
    # re-spawn both CLIs on every hook invocation.
    _save_cached_agent_email("")
    return ""


def _session_id_field(payload: dict) -> str:
    """Read the session id, accepting Grok's camelCase key alongside Claude's.

    Grok CLI auto-loads ~/.claude/settings.json for "Claude Code compatibility"
    (its own hooks doc says so), so this same Stop hook fires there too - but
    Grok's stdin envelope uses camelCase (sessionId), not Claude's snake_case
    (session_id). Without this fallback, session_id resolves empty under
    Grok, no prompt-start breadcrumb is ever matched, and the hook's own
    no-usage-and-no-turn-start guard drops the event entirely and silently.

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
    the same scripts run under both, but the payloads differ enough that the
    Stop path has to branch: Grok's envelope is camelCase, names the event in
    hookEventName, carries a "reason" distinguishing a real turn end from
    session teardown, and has no transcript to read tokens or a model from.

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

    SYNC: duplicated verbatim in subagent_stop_hook.py - guarded by
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

    SYNC: duplicated verbatim in subagent_stop_hook.py - guarded by
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

    This is the only place a Grok turn's model id is recorded in a form a
    hook can read (current_model_id) - the Stop payload has no model field
    and there is no transcript to fall back on. Also carries agent_name,
    session_kind ("main" / "subagent") and created_at.

    SYNC: duplicated verbatim in subagent_stop_hook.py - guarded by
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


def _detect_provider(model_id: str) -> str:
    """Infer LLM provider from the model identifier prefix.

    Deliberately duplicates cost.pricing.PROVIDER_PREFIXES rather than importing
    it: this hook runs as a standalone script under whatever interpreter the
    agent settings point at, so it cannot depend on specify_cli being importable.
    Any prefix added here must be added there too, and vice versa.
    """
    lower = model_id.lower()
    prefixes = {
        "codestral-": "mistral",
        "mistral-": "mistral",
        "claude-": "anthropic",
        "grok-": "xai",
        "gpt-": "openai",
        "o1-": "openai",
        "o3-": "openai",
        "o4-": "openai",
        # xAI (feature 105) - without this, Grok turns are attributed to
        # anthropic by the fallback below and priced at Claude rates.
        "grok-": "xai",
    }
    for prefix, provider in prefixes.items():
        if lower.startswith(prefix):
            return provider
    return "anthropic"


def _read_tokens_from_transcript(
    transcript_path: str, turn_start=None
) -> tuple[int, int, int, int, str]:
    """Read THIS turn's token usage and model from the transcript.

    The Stop hook payload has no usage data - tokens live in the transcript.
    Returns (input_tokens, output_tokens, cache_read, cache_creation, model_id).

    When *turn_start* is known (from the UserPromptSubmit breadcrumb), usage is
    SUMMED across every assistant message at or after it. A turn issues one API
    request per tool round-trip and each is billed separately, so a turn that
    ran thirty tool calls has thirty usage blocks. Reading only the last one -
    the previous behaviour - reported a fraction of what the turn cost:
    measured against a real transcript, 6 turns averaging 45.7 assistant
    messages each reported 1,109,818 effective input tokens instead of
    44,700,839, a 40x under-count. It also made subagent events (which sum
    their whole run) look enormous beside a parent measured on a different
    basis.

    This function only ever reports TOKENS. Turn duration / agent_working_ms
    is derived from the prompt-start breadcrumb by main() and is untouched by
    anything here.

    Without a turn boundary there is no safe way to tell this turn's messages
    from the rest of the conversation, so the last-message reading is kept: it
    under-reports, but summing an entire multi-hour session into one turn would
    over-report far more.
    """
    if not transcript_path:
        return 0, 0, 0, 0, ""
    try:
        path = Path(transcript_path)
        if not path.exists():
            return 0, 0, 0, 0, ""
        lines = path.read_text(encoding="utf-8-sig").splitlines()
    except Exception:
        return 0, 0, 0, 0, ""

    if turn_start is None:
        return _last_message_usage(lines)

    total_in = total_out = total_cache_read = total_cache_creation = 0
    model_id = ""
    matched = False

    for line in lines:
        if not line.strip():
            continue
        try:
            obj = json.loads(line)
        except Exception:
            continue
        if obj.get("type") != "assistant":
            continue

        raw_ts = obj.get("timestamp")
        if not raw_ts:
            # Cannot place it relative to the turn boundary. Skipping
            # under-counts by one message; including it risks pulling in the
            # entire prior conversation.
            continue
        try:
            ts = datetime.fromisoformat(str(raw_ts).replace("Z", "+00:00"))
            if ts.tzinfo is None:
                ts = ts.replace(tzinfo=timezone.utc)
        except Exception:
            continue
        if ts < turn_start:
            continue

        msg = obj.get("message") or {}
        usage = msg.get("usage") or {}
        inp = int(usage.get("input_tokens") or 0)
        out = int(usage.get("output_tokens") or 0)
        cache_read = int(usage.get("cache_read_input_tokens") or 0)
        cache_creation = int(usage.get("cache_creation_input_tokens") or 0)
        if inp + out + cache_read + cache_creation == 0:
            continue
        matched = True
        total_in += inp
        total_out += out
        total_cache_read += cache_read
        total_cache_creation += cache_creation
        if not model_id and msg.get("model"):
            model_id = str(msg.get("model"))

    if not matched:
        # Clock skew between the breadcrumb writer and the transcript, or
        # timestamps we could not parse, would otherwise report a real turn as
        # zero-cost. Fall back rather than drop it.
        return _last_message_usage(lines)

    return total_in, total_out, total_cache_read, total_cache_creation, model_id


def _last_message_usage(lines: list) -> tuple[int, int, int, int, str]:
    """Usage from the last assistant message carrying any (the legacy read)."""
    try:
        for line in reversed(lines):
            if not line.strip():
                continue
            try:
                obj = json.loads(line)
            except Exception:
                continue
            if obj.get("type") != "assistant":
                continue
            msg = obj.get("message") or {}
            usage = msg.get("usage") or {}
            inp = int(usage.get("input_tokens") or 0)
            out = int(usage.get("output_tokens") or 0)
            if inp + out == 0:
                continue
            cache_read = int(usage.get("cache_read_input_tokens") or 0)
            cache_creation = int(usage.get("cache_creation_input_tokens") or 0)
            model_id = str(msg.get("model") or "")
            return inp, out, cache_read, cache_creation, model_id
    except Exception:
        pass
    return 0, 0, 0, 0, ""


def _read_prompt_start(session_id: str) -> tuple["datetime | None", str, str]:
    """Read the prompt-start breadcrumb written by prompt_start_hook.py.

    The UserPromptSubmit hook stamps when the current turn began. This lets
    the Stop hook (a) time the ENTIRE turn (prompt submitted -> agent stopped)
    and (b) tell whether last-command.json was written during this turn or is
    stale context from an earlier command.

    Returns (started_at datetime, cwd, raw started_at string) or
    (None, "", "") when unavailable. The raw string is compared against
    last-command.json's turn_started_at for exact turn matching.
    """
    if not session_id:
        return None, "", ""
    try:
        path = (
            Path.home()
            / ".polaris"
            / "telemetry"
            / f"prompt-start-{session_id}.json"
        )
        if not path.exists():
            return None, "", ""
        data = json.loads(path.read_text(encoding="utf-8"))
        started = str(data.get("started_at") or "")
        if not started:
            return None, "", ""
        started_dt = datetime.fromisoformat(started.replace("Z", "+00:00"))
        return started_dt, str(data.get("cwd") or ""), started
    except Exception:
        return None, "", ""


def _git_config_value(work_dir: str, key: str) -> str:
    """Read a git config value for *work_dir*. Empty string on any failure."""
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
    """Read git user.email for *work_dir*. Empty string on any failure.

    Normalized (trimmed + lowercased) so identity matching is case-insensitive.
    """
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


def _record_via_telemetry(
    token_data: dict,
    started_at_iso: str,
    cache_read: "int | None" = 0,
    cache_creation: "int | None" = 0,
    agent_name: str = "",
    sso_email: str = "",
    git_email: str = "",
) -> bool:
    """Record the turn as ONE event through the shared telemetry pipeline.

    agent_name labels which CLI produced the turn ("grok"; empty keeps the
    historical Claude default). It is passed separately rather than through
    token_data because token_data is POSTed verbatim to /tokens/batch, for
    the same reason the cache components are.

    sso_email / git_email are the two human-identity sources, passed as
    arguments for that same verbatim-body reason. sso_email is the Entra
    signed-in account and takes precedence for attribution; git_email is
    retained separately so the fallback stays visible on the event and the
    two are never conflated.

    cache_read / cache_creation are the cache components already summed into
    token_data["tokens_in"], passed as arguments rather than added to
    token_data on purpose: token_data is POSTed VERBATIM to /tokens/batch by
    _push_to_service, so putting them there would change the request body the
    service receives. They belong only on the local TelemetryEvent, which
    reaches the service through _to_service_event's explicit allowlist and
    therefore cannot carry them onto the wire.

    Appends a full TelemetryEvent to ~/.polaris/telemetry/events.jsonl (the
    same local store CLI command events use) and then attempts an immediate
    service flush - on success the sent lines are archived by
    flush_to_service(); on failure they simply stay in events.jsonl for the
    next retry. Returns True once the event is durably written locally.
    """
    try:
        from specify_cli import telemetry
    except Exception:
        return False

    try:
        import socket

        now_iso = datetime.now(timezone.utc).isoformat()

        try:
            duration = int(token_data.get("agent_working_ms") or 0)
        except (TypeError, ValueError):
            duration = 0
        if duration < 0:
            duration = 0

        command = token_data.get("command") or ""
        slug = token_data.get("feature_slug") or ""
        try:
            category = telemetry._classify_command_category(command, slug)
        except Exception:
            category = "generic"

        try:
            hostname = socket.gethostname()
        except Exception:
            hostname = ""

        version = ""
        try:
            from specify_cli import __version__ as version
        except Exception:
            pass

        try:
            seq = int(token_data.get("session_seq") or 0)
        except (TypeError, ValueError):
            seq = 0

        event = telemetry.TelemetryEvent(
            command=command,
            git_user_name=token_data.get("user_name") or "",
            # git_email only. token_data["user_email"] is the SSO-preferring
            # attribution slot (set to the SSO address at the call site), so
            # falling back to it put the SSO identity in the git field for
            # anyone with no git user.email configured - collapsing the two
            # sources this field exists to keep distinct.
            git_user_email=git_email,
            sso_email=sso_email,
            timestamp=token_data.get("occurred_at") or now_iso,
            duration_ms=duration,
            success=True,
            exit_code=0,
            started_at=started_at_iso or "",
            completed_at=now_iso,
            feature_slug=slug,
            polaris_version=version,
            hostname=hostname,
            project=token_data.get("project") or "",
            session_id=token_data.get("session_id") or "",
            session_seq=seq,
            command_category=category,
            tokens_in=token_data.get("tokens_in") or None,
            tokens_out=token_data.get("tokens_out") or None,
            tokens_total=token_data.get("tokens_total") or None,
            # Components of tokens_in, recorded locally so `polaris cost` can
            # bill cache reads at the cache rate instead of the fresh-input
            # rate. A number is written as a plain int (not `or None`) so a
            # genuine zero stays zero; None is reserved for "breakdown not
            # captured" and is passed straight through (Grok turns, where no
            # usage of any kind is readable).
            cache_read_tokens=(
                None if cache_read is None else max(0, int(cache_read or 0))
            ),
            cache_creation_tokens=(
                None
                if cache_creation is None
                else max(0, int(cache_creation or 0))
            ),
            model=token_data.get("model") or "",
            agent_name=agent_name,
            agent_working_ms=token_data.get("agent_working_ms"),
            agent_email=token_data.get("agent_email") or "",
            event_time=token_data.get("event_time") or "",
        )
        telemetry._append_event(event)
    except Exception:
        return False

    try:
        telemetry.flush_to_service()
    except Exception:
        pass
    return True


def _consume_subagent_time(claude_session_id: str) -> int:
    """Sum and delete every subagent-time marker written for this session.

    Each Task-tool subagent that finished during this turn wrote its own
    marker file (subagent_stop_hook.py) recording its own duration as a
    separate, already-recorded telemetry event. That same wall-clock span
    is also included in THIS turn's own agent_working_ms below (the parent
    was blocked on the subagent for that whole span), so it must be
    subtracted here or the same span is double-counted. Markers are always
    consumed (even when there is nothing to subtract into) so a stray one
    can never leak into a later turn.
    """
    total = 0
    if not claude_session_id:
        return total
    telemetry_dir = Path.home() / ".polaris" / "telemetry"
    try:
        for marker in telemetry_dir.glob(f"subagent-time-{claude_session_id}-*.json"):
            try:
                data = json.loads(marker.read_text(encoding="utf-8"))
                total += max(0, int(data.get("duration_ms") or 0))
            except Exception:
                pass
            finally:
                try:
                    marker.unlink()
                except Exception:
                    pass
    except Exception:
        pass
    return total


def _consume_turn_state(claude_session_id: str) -> None:
    """Delete this turn's breadcrumbs once the turn has been recorded.

    A leftover prompt-start file would hand a later turn a wrong start time
    (inflating its event_time) if the UserPromptSubmit hook ever stops
    firing; a leftover last-command.json could only mis-attribute the next
    turn. Both are per-turn state - consume them at turn end.
    """
    base = Path.home() / ".polaris" / "telemetry"
    for name in (f"prompt-start-{claude_session_id}.json", "last-command.json"):
        try:
            (base / name).unlink()
        except Exception:
            pass


def main() -> None:
    try:
        raw = sys.stdin.buffer.read().decode("utf-8-sig")
        payload = json.loads(raw)
    except Exception:
        return

    claude_session_id = _session_id_field(payload)
    payload_cwd = str(payload.get("cwd") or payload.get("workspaceRoot") or "")

    is_grok = _is_grok_payload(payload)
    if is_grok:
        # Grok fires Stop twice per session: once when the turn actually ends
        # (reason "end_turn") and once more as the session tears down
        # ("channel_closed" / "shutdown"). Only the first is a turn. Returning
        # HERE - before any turn state is read or consumed - is what makes the
        # order of the two fires irrelevant: the teardown fire leaves the
        # breadcrumb and the subagent-time markers exactly as it found them,
        # so whichever fire carries end_turn still records a complete turn.
        reason = str(payload.get("reason") or "")
        if reason and reason != "end_turn":
            return

    # When the current turn began (written by the UserPromptSubmit hook).
    prompt_start_dt, prompt_cwd, prompt_start_raw = _read_prompt_start(
        claude_session_id
    )

    # The Stop hook payload has no usage field - read tokens from the transcript.
    transcript_path = str(payload.get("transcript_path") or "")
    input_tokens, output_tokens, cache_read, cache_creation, model_id = (
        _read_tokens_from_transcript(transcript_path, prompt_start_dt)
    )

    # A turn with no usage in the transcript is still a turn: when we know
    # when it started (prompt-start breadcrumb), record it with empty token
    # counts instead of silently dropping it. With neither tokens nor a
    # turn start there is nothing meaningful to record.
    if (input_tokens + output_tokens) == 0 and prompt_start_dt is None:
        return

    # Effective input tokens includes cache hits (still consumed compute)
    effective_input = input_tokens + cache_read + cache_creation

    tokens_in_value = effective_input
    tokens_out_value = output_tokens
    tokens_total_value = effective_input + output_tokens
    cache_read_value = cache_read
    cache_creation_value = cache_creation
    if is_grok and (effective_input + output_tokens) == 0:
        # Grok exposes no billed usage to a hook at all - no transcript, no
        # usage blocks in chat_history.jsonl, and updates.jsonl's
        # _meta.totalTokens is the context-window size rather than what the
        # turn cost. None records "not captured"; 0 would assert the turn was
        # free and quietly drag every Grok-inclusive average down.
        tokens_in_value = None
        tokens_out_value = None
        tokens_total_value = None
        cache_read_value = None
        cache_creation_value = None

    # Read session and last command info from last-command.json
    last_cmd_file = Path.home() / ".polaris" / "telemetry" / "last-command.json"
    command = ""
    session_id = ""
    session_seq = 0
    project = ""
    user_email = ""
    feature_slug = ""

    # last-command.json is only trusted when the polaris command actually ran
    # during THIS turn. Preferred check: the CLI stamps turn_started_at with
    # the prompt-start timestamp it observed, so an exact string match ties
    # the file to this very turn. Fallback for older CLIs without the stamp:
    # file mtime >= prompt submit time. A stale file left over from an
    # earlier turn or session would mis-attribute a direct prompt to the
    # previous command. When there is no prompt-start breadcrumb (older
    # installs without the UserPromptSubmit hook), trust the file as before.
    last: dict = {}
    last_cmd_is_current = False
    if last_cmd_file.exists():
        try:
            last = json.loads(last_cmd_file.read_text(encoding="utf-8"))
        except Exception:
            last = {}
        turn_started = str(last.get("turn_started_at") or "")
        if prompt_start_dt is None:
            last_cmd_is_current = bool(last)
        elif turn_started:
            last_cmd_is_current = turn_started == prompt_start_raw
        else:
            try:
                last_cmd_is_current = (
                    last_cmd_file.stat().st_mtime >= prompt_start_dt.timestamp()
                )
            except Exception:
                last_cmd_is_current = False

    if last_cmd_is_current and last:
        try:
            # turn_command is the FIRST polaris command of this turn (what the
            # prompt was NLP-routed to); command is merely the last subcommand.
            command = last.get("turn_command") or last.get("command") or ""
            session_id = last.get("session_id") or ""
            session_seq = int(last.get("session_seq") or 0)
            project = last.get("project") or ""
            user_email = last.get("git_user_email") or ""
            feature_slug = last.get("feature_slug") or ""
        except Exception:
            pass

    work_dir = payload_cwd or prompt_cwd
    # Resolved unconditionally: the event-recording call below reads it on
    # every path, not just the no-command branch that also uses it to fill
    # in user_email.
    _sso = _sso_email()
    if not command:
        # Direct prompt: the user talked to Claude without a polaris command
        # this turn (typed directly, not routed via NLP to a command). The
        # event is still captured - command falls back to "implement" and
        # every other field (tokens, project, emails, timing) is populated
        # as usual.
        command = "implement"
        if not session_id:
            session_id = claude_session_id
        if not project and work_dir:
            try:
                project = Path(work_dir).name
            except Exception:
                pass
        # SSO identity outranks both last-command.json and git: it is the
        # account the user signed into, bound to a tenant-issued token.
        if _sso:
            user_email = _sso
        elif not user_email:
            user_email = _git_user_email(work_dir)

    # Fall back to env var, then to Claude Code's own session_id from the payload
    if not session_id:
        session_id = os.environ.get("POLARIS_SESSION_ID") or ""
    if not session_id:
        session_id = _session_id_field(payload)
    if not session_id:
        return

    # Fall back to model from payload if transcript didn't have it. Under
    # Grok there is no transcript and no model field on the payload either,
    # so its own session store is the only source.
    if not model_id and is_grok:
        model_id = str(
            _grok_session_summary(claude_session_id).get("current_model_id") or ""
        )
    if not model_id:
        model_id = str(payload.get("model") or "")

    # Compute agent working time as the ENTIRE time this prompt ran: from the
    # moment the user submitted it (prompt-start breadcrumb, written by the
    # UserPromptSubmit hook) until now (the Stop hook firing). This covers
    # polaris-command turns and direct prompts alike.
    agent_working_ms = None
    if prompt_start_dt is not None:
        agent_working_ms = max(
            0,
            int(
                (datetime.now(timezone.utc) - prompt_start_dt).total_seconds()
                * 1000
            ),
        )

    # Fallback (older installs without the UserPromptSubmit hook, or any turn
    # where the prompt-start breadcrumb is unavailable): use the
    # session-start breadcrumb written at polaris CLI startup (spec 091).
    #
    # This must report the DELTA since the last time this fallback fired,
    # not the wall-clock time since the session's original start. Production
    # telemetry showed the previous "read but never refresh" behavior meant
    # a session revisited after a real idle gap - hours, days, or weeks with
    # no activity at all - reported that entire idle span as active agent
    # time on its next turn (the reported value tracked wall-clock elapsed
    # time almost exactly, regardless of whether any work happened in
    # between). Refreshing started_at after each read turns this into a
    # genuine per-gap delta, the same semantics prompt_start_hook.py's
    # per-turn breadcrumb already gets right. cleanup_stale_agent_start_breadcrumbs()
    # still reaps any breadcrumb that goes 24h+ without being refreshed at all.
    if agent_working_ms is None and session_id:
        try:
            breadcrumb_path = (
                Path.home()
                / ".polaris"
                / "telemetry"
                / f"session-start-{session_id}-{session_seq}.json"
            )
            if breadcrumb_path.exists():
                # Guard the read-then-refresh with a short-lived lock file:
                # without it, two Stop hooks racing on the same session/seq
                # (e.g. an auto-compaction Stop overlapping the next turn's
                # Stop) could both read the same started_at and each report
                # the same elapsed window as active time, double-counting
                # it. An abandoned lock (the owner crashed before removing
                # it) is treated as stale after a few seconds and cleared
                # rather than blocking this fallback forever.
                lock_path = breadcrumb_path.with_suffix(".lock")
                got_lock = False
                try:
                    try:
                        if time.time() - lock_path.stat().st_mtime > 5:
                            lock_path.unlink()
                    except Exception:
                        pass
                    fd = os.open(str(lock_path), os.O_CREAT | os.O_EXCL | os.O_WRONLY)
                    os.close(fd)
                    got_lock = True
                except Exception:
                    got_lock = False

                if got_lock:
                    try:
                        bc_data = json.loads(breadcrumb_path.read_text(encoding="utf-8"))
                        started_at_str = bc_data.get("started_at", "")
                        if started_at_str:
                            started_dt = datetime.fromisoformat(
                                started_at_str.replace("Z", "+00:00")
                            )
                            now_dt = datetime.now(timezone.utc)
                            agent_working_ms = max(
                                0, int((now_dt - started_dt).total_seconds() * 1000)
                            )
                            bc_data["started_at"] = now_dt.isoformat()
                            tmp = breadcrumb_path.with_suffix(f".json.tmp-{os.getpid()}")
                            _refreshed = False
                            try:
                                tmp.write_text(json.dumps(bc_data), encoding="utf-8")
                                os.replace(tmp, breadcrumb_path)
                                _refreshed = True
                            except Exception:
                                pass
                            if not _refreshed:
                                # A stale started_at reproduces the exact
                                # over-counting bug this refresh fixes on
                                # every later turn for this session, so
                                # retry once with a direct write rather than
                                # silently giving up.
                                try:
                                    breadcrumb_path.write_text(
                                        json.dumps(bc_data), encoding="utf-8"
                                    )
                                except Exception:
                                    pass
                            try:
                                tmp.unlink(missing_ok=True)
                            except Exception:
                                pass
                    finally:
                        try:
                            lock_path.unlink()
                        except Exception:
                            pass
        except Exception:
            agent_working_ms = None

    # Subtract any subagent time already recorded as its own separate event
    # during this turn - it is included in the wall-clock span above (the
    # parent was blocked on the subagent for that span), so leaving it in
    # would double-count that time on top of the subagent's own event.
    _subagent_ms = _consume_subagent_time(claude_session_id)
    if _subagent_ms and agent_working_ms is not None:
        agent_working_ms = max(0, agent_working_ms - _subagent_ms)

    # Format agent_working_ms as a human-readable duration the same way
    # telemetry.py formats duration_ms for command events (e.g. "3m 43s").
    if agent_working_ms:
        _total_s = agent_working_ms // 1000
        _mins = _total_s // 60
        _secs = _total_s % 60
        event_time = f"{_mins}m {_secs}s" if _mins > 0 else f"{_secs}s"
    else:
        event_time = "0s"

    token_data = {
        "session_id": session_id,
        "session_seq": session_seq,
        "command": command,
        # Normalized here too: user_email may have come from last-command.json
        # (git_user_email), written by a CLI version predating this fix.
        "user_email": (user_email or "").strip().lower(),
        "user_name": _git_config_value(work_dir, "user.name"),
        "agent_email": _detect_agent_email(),
        "project": project,
        "feature_slug": feature_slug,
        "tokens_in": tokens_in_value,
        "tokens_out": tokens_out_value,
        "tokens_total": tokens_total_value,
        "model": model_id,
        "provider": _detect_provider(model_id),
        "occurred_at": datetime.now(timezone.utc).isoformat(),
        "agent_working_ms": agent_working_ms,
        "event_time": event_time,
    }

    # started_at for the event: the prompt submit time when known, else
    # derived from the session-start fallback duration.
    started_at_iso = prompt_start_raw
    if not started_at_iso and agent_working_ms:
        try:
            started_at_iso = (
                datetime.now(timezone.utc)
                - timedelta(milliseconds=agent_working_ms)
            ).isoformat()
        except Exception:
            started_at_iso = ""

    try:
        # Primary path: record through the shared telemetry pipeline
        # (events.jsonl append + service flush + archive on success).
        if _record_via_telemetry(
            token_data,
            started_at_iso,
            cache_read_value,
            cache_creation_value,
            "grok" if is_grok else "",
            _sso,
            _git_user_email(work_dir),
        ):
            return

        # Legacy fallback (specify_cli not importable): push directly to the
        # service, else leave a token file for the next CLI sweep.
        service_url, api_key = _read_service_config()
        if service_url and api_key and _push_to_service(
            token_data, service_url, api_key
        ):
            return
        _write_fallback_file(token_data, session_id, session_seq)
    finally:
        # The turn is over: consume its breadcrumbs so they can never
        # mislabel or mis-time a later turn.
        _consume_turn_state(claude_session_id)


if __name__ == "__main__":
    main()
