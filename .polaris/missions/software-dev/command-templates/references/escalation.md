# Escalation Contract (autonomous-pipeline safety valve)

Loaded on demand by autopilot and the gated commands (specify, accept, qa, fix,
clarify, ship). It defines what to do when the unattended pipeline gets stuck so
it never waits silently for a human who is not there.

## When to escalate

Escalate when EITHER:

- a stage has failed more than the approved retry limit (`autopilot.retry_limit`
  from spec frontmatter, default 3), OR
- a gate cannot auto-resolve under `AUTOPILOT_RUN` (missing work-item ID, missing
  token/credential, missing tracker config, a clarification that would change
  correctness and cannot be assumed, an acceptance blocker, a PR-merge timeout).

Do NOT escalate for conditions the recorded defaults cover - use the default and
continue (see each command's Autopilot Context section).

## How to escalate (one command)

```bash
polaris agent escalate --feature <slug> --stage <stage> --reason "<why>" \
  --attempts <n> --recommended-action "<one-line next step>"
```

`polaris agent escalate` does all of the following in code (deterministic):

1. **State** - writes an `escalation` block into `.polaris/autopilot-state.json`
   (stage, attempts, reason, evidence paths from `.polaris/test-evidence/`,
   report path, timestamp) alongside the existing autopilot state, so
   `polaris.autopilot --resume` continues from the recorded point.
2. **Report** - writes `.polaris/reports/escalation-<feature>-<ts>.md` (what
   failed, what was tried, links to the WS-1 test-evidence files, recommended
   next action).
3. **Console** - prints a prominent escalation block.
4. **Webhook** - when `.polaris/config.yaml` sets `escalation.webhook_url`, POSTs
   a compact JSON payload (best-effort; a failed POST never fails escalation).

The command exits 0 (clean). After calling it, STOP the pipeline - do not retry
further and do not emit any `WAITING_FOR_*` sentinel.

## Config keys

```yaml
escalation:
  webhook_url: ""            # optional; POST target for escalation payloads
ship:
  auto_merge: false          # gh pr merge --auto when true
  merge_timeout_minutes: 30  # bounded PR-merge poll before escalating
```
