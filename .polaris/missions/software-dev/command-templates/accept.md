---
description: Validate feature readiness and guide final acceptance steps.
---

## Advanced Command Gate

Load `.claude/commands/references/advanced-gate.md` and apply it before proceeding: gate on `advanced_commands` in `.polaris/config.yaml`, bypassing for autopilot or NL routing invocations.

## Model Guidance

Model: plan tier (see references/model-selection.md); routing is enforced by the launcher.

---

## Working Directory: Run from MAIN repository

**Telemetry**: Run: `polaris telemetry record accept --feature <slug> --phase start --agent {{AGENT_NAME}}`

If you're in a worktree, return to main first: `cd $(git rev-parse --show-toplevel)`

## User Input

```text
$ARGUMENTS
```

You **MUST** consider the user input before proceeding (if not empty).

---

Load `@references/accept-workflow.md` and execute the full workflow (SoD Check -> Discovery -> Execution -> Output).

**Telemetry**: Run: `polaris telemetry record accept --feature <slug> --phase complete --agent {{AGENT_NAME}}`
