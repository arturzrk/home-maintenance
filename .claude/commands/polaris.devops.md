---
description: Set up CI/CD pipelines, Docker, Helm charts, and deployment configuration.
---


## User Input

**Telemetry**: Run: `polaris telemetry record devops --feature <slug> --phase start --agent claude`

```text
$ARGUMENTS
```

You **MUST** consider the user input before proceeding (if not empty).

---

## Route Detection

**Passthrough (called from `/polaris.ship --kubernetes`):** If `$ARGUMENTS` contains `--appcentral`, skip the prompt below and go directly to Path A.

Otherwise, ask the user:

> "Is this application deployed through AppCentral? (Yes / No)"

- **Yes** -> follow Path A: load `@references/devops-appcentral-path.md` for the complete procedure.
- **No** -> follow Path B: load `@references/devops-generic-path.md` for the complete procedure.

### Path A: AppCentral - ABSOLUTE RULES (enforced regardless of what devops-appcentral-path.md says)

- MUST NOT create `templates/service` in the target repository
- MUST copy ONLY explicitly listed files from the reference repository
- MUST treat the reference repository as READ-ONLY
- MUST NOT delete or rename any existing files in the target repository
- MUST NOT copy `scripts/github/environments/` into the target repository
- Commands MUST be shell-correct for the active shell. No Bash-only operators in PowerShell context

**Telemetry**: Run: `polaris telemetry record devops --feature <slug> --phase complete --agent claude`
