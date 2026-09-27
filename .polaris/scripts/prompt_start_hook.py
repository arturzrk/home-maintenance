"""Claude Code / Grok CLI UserPromptSubmit hook - marks each turn's start.

Called every time the user submits a prompt, BEFORE the agent starts
working. Writes a small breadcrumb file keyed by the session id so the Stop
hook (telemetry_hook.py) can:

1. Compute the turn duration (agent working time) even for prompts given
   directly to the agent without any polaris command being invoked.
2. Detect whether last-command.json was written during THIS turn or is
   stale context left over from an earlier command, so a direct prompt
   is never mis-attributed to a previous polaris command.

Grok CLI loads ~/.claude/settings.json too, so this same script runs there
and the breadcrumb it writes matters MORE under Grok than under Claude: it
is the only clock either Grok hook has. Grok's Stop payload carries no
transcript and no timestamps, and it fires this hook inside subagent
sessions too (each of which has its own session id), which is what lets
subagent_stop_hook.py time a Grok subagent at all.

That extra firing is also why the two side effects below are conditional.
Claude Task subagents never fire UserPromptSubmit, so under Claude every
firing is a genuine new top-level turn. Under Grok a firing may instead be
a CHILD starting mid-turn, inside a parent turn that is still running - and
a child must not clear the parent's state. A child firing therefore writes
its breadcrumb marked "subagent": true (so telemetry._latest_prompt_start
keeps stamping the PARENT's start time onto turn_started_at) and leaves
last-command.json alone (so the parent's Stop hook still finds the polaris
command its turn was routed to, rather than falling back to "implement").

Never raises or prints errors - silent failure is intentional.
The agent CLI is never blocked by this script.
"""
import json
import os
import re
import sys
import time
from datetime import datetime, timezone
from pathlib import Path

_STALE_HOURS = 24


def _safe_session_id(session_id: str) -> str:
    """Restrict the session id to filename-safe characters."""
    return re.sub(r"[^A-Za-z0-9_-]", "", session_id)[:64]


def _session_id_field(payload: dict) -> str:
    """Read the session id, accepting Grok's camelCase key alongside Claude's.

    Grok CLI auto-loads ~/.claude/settings.json for "Claude Code compatibility"
    (its own hooks doc says so), so this same hook fires there too - but Grok's
    stdin envelope uses camelCase (sessionId), not Claude's snake_case
    (session_id). Without this fallback, session_id resolves empty under
    Grok and the breadcrumb below is silently never written.

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
    the same scripts run under both. Here it only labels the breadcrumb (the
    two Stop-side hooks branch on their own payloads); readers ignore keys
    they do not know, so the extra field is safe for older installs.

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

    SYNC: duplicated verbatim in subagent_stop_hook.py / telemetry_hook.py -
    guarded by tests/specify_cli/scripts/test_grok_hook_payloads.py.
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

    SYNC: duplicated verbatim in subagent_stop_hook.py / telemetry_hook.py -
    guarded by tests/specify_cli/scripts/test_grok_hook_payloads.py.
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

    Read here only for session_kind ("main" / "subagent"), the fallback
    signal for a prompt starting inside a subagent session.

    SYNC: duplicated verbatim in subagent_stop_hook.py / telemetry_hook.py -
    guarded by tests/specify_cli/scripts/test_grok_hook_payloads.py.
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


def _grok_subagent_session(payload: dict, session_id: str) -> bool:
    """True when this prompt is starting inside a Grok SUBAGENT session.

    Such a firing is NOT a new top-level turn: it happens partway through a
    parent turn that is still running, so it must not clear that turn's
    state. Claude never reaches this - its Task subagents fire no
    UserPromptSubmit at all - so the Claude path is unchanged by definition.

    subagentType on the payload is the primary signal because it is there
    from the very first prompt of a child session, before Grok has
    necessarily flushed that session's summary.json. session_kind in the
    session store is the fallback for any Grok build that omits it.
    """
    if payload.get("subagentType"):
        return True
    if not _is_grok_payload(payload):
        return False
    kind = str(_grok_session_summary(session_id).get("session_kind") or "")
    return kind == "subagent"


def main() -> None:
    try:
        raw = sys.stdin.buffer.read().decode("utf-8-sig")
        payload = json.loads(raw)
    except Exception:
        return

    # The store is keyed by the id exactly as Grok wrote it; the filename
    # uses the sanitized form.
    raw_session_id = _session_id_field(payload)
    session_id = _safe_session_id(raw_session_id)
    if not session_id:
        return

    is_subagent = _grok_subagent_session(payload, raw_session_id)
    telemetry_dir = Path.home() / ".polaris" / "telemetry"

    try:
        telemetry_dir.mkdir(parents=True, exist_ok=True)
        data = {
            "session_id": session_id,
            "started_at": datetime.now(timezone.utc).isoformat(),
            "cwd": str(payload.get("cwd") or payload.get("workspaceRoot") or ""),
            # Which CLI opened this turn. Purely diagnostic - both Stop-side
            # readers decide from their own payload and only ever read
            # started_at, cwd and subagent from here.
            "agent": "grok" if _is_grok_payload(payload) else "",
            # A Grok child session starting mid-turn, not a top-level turn.
            # telemetry._latest_prompt_start() skips these so the turn label
            # a polaris CLI invocation stamps stays tied to the PARENT's
            # start time; subagent_stop_hook.py still reads this file
            # directly by session id to time the child.
            "subagent": is_subagent,
        }
        path = telemetry_dir / f"prompt-start-{session_id}.json"
        # Atomic write so the Stop hook never reads a half-written record
        tmp = path.with_suffix(f".json.tmp-{os.getpid()}")
        tmp.write_text(json.dumps(data), encoding="utf-8")
        os.replace(tmp, path)
    except Exception:
        pass

    # A new turn starts with a clean slate: any last-command.json still on
    # disk belongs to a PREVIOUS turn (the Stop hook consumes it at turn
    # end). Deleting it here guarantees the Stop hook only ever attributes
    # this turn to a polaris command that actually ran during it.
    #
    # A Grok subagent firing is the one case where that is wrong: it is not
    # a new turn but a child starting INSIDE a parent turn still in flight,
    # and this file is machine-global. Deleting it there would strip the
    # parent's own Stop hook of the command its turn was routed to, which
    # would then be recorded as a bare "implement" instead of e.g.
    # "polaris.autopilot".
    if not is_subagent:
        try:
            (telemetry_dir / "last-command.json").unlink()
        except Exception:
            pass

    # Opportunistic cleanup: drop prompt-start breadcrumbs older than 24h so
    # they never accumulate on machines that only use Claude directly and
    # never run the polaris CLI (whose startup does the equivalent sweep).
    try:
        cutoff = time.time() - _STALE_HOURS * 3600
        for f in telemetry_dir.glob("prompt-start-*.json"):
            try:
                if f.stat().st_mtime < cutoff:
                    f.unlink()
            except Exception:
                pass
    except Exception:
        pass


if __name__ == "__main__":
    main()
