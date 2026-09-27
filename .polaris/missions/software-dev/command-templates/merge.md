---
description: Merge a completed feature into the target branch and clean up worktree
---

## Advanced Command Gate

Load `references/advanced-gate.md` and apply the `advanced_commands` gate (bypassed for autopilot or NL routing).

## Model Guidance

Model: impl tier (see references/model-selection.md); the launcher enforces routing.

---

## User Input

**Telemetry**: Run: `polaris telemetry record merge --feature <slug> --phase start --agent {{AGENT_NAME}}`


```text
$ARGUMENTS
```

Consider the user input before proceeding (if not empty).

## Location Pre-flight (CRITICAL)

You MUST run from a feature worktree, not the main repository:

```bash
python -c "
from specify_cli.guards import validate_worktree_location
result = validate_worktree_location()
if not result.is_valid:
    print(result.format_error())
    raise SystemExit(1)
print('Location verified:', result.branch_name)
"
```

On failure, `cd` into a WP worktree first, or run from the target branch with `polaris merge --feature <slug>`.

## Prerequisites and Preflight

Protected-branch and preflight rules (all WPs `done`, feature accepted, clean worktrees, synced target) are enforced by the CLI. Run `polaris merge` (`--json` in automation); on a blocked verdict follow each `remediation`, fix, and retry once. Never pass human-override flags.

## Security Gate (Required Before Merge)

Run this non-blocking secret scan on the diff before merging (reports issues, never blocks):

```python
import subprocess, re
from pathlib import Path

target = "main"
diff = subprocess.run(["git", "diff", f"origin/{target}...HEAD"], capture_output=True, text=True).stdout
names = subprocess.run(["git", "diff", f"origin/{target}...HEAD", "--name-only"], capture_output=True, text=True).stdout
changed = [f for f in names.splitlines() if f]

secret_patterns = [
    (r'^\+.*(?i)(password|passwd|pwd)\s*[=:]\s*["\']?\S{6,}', "Hardcoded password"),
    (r'^\+.*(?i)(api[_-]?key|apikey|secret[_-]?key)\s*[=:]\s*["\']?\S{8,}', "Hardcoded API key"),
    (r'^\+.*(?i)(token|auth[_-]?token|bearer)\s*[=:]\s*["\']?[A-Za-z0-9\-_.]{16,}', "Hardcoded token"),
    (r'^\+.*-----BEGIN (RSA |EC |OPENSSH )?PRIVATE KEY-----', "Private key committed"),
    (r'^\+.*(?i)aws[_-]?secret[_-]?access[_-]?key\s*[=:]\s*\S+', "AWS secret key"),
]
findings = [f"[{label}] in diff" for pat, label in secret_patterns if re.search(pat, diff, re.MULTILINE)]
findings += [f"[Sensitive file] {f}" for f in changed if Path(f).suffix.lower() in (".pem", ".pfx", ".p12", ".key", ".jks", ".keystore")]
findings += [f"[Env file] {f}" for f in changed if Path(f).name == ".env" or (Path(f).name.startswith(".env.") and not f.endswith(".example"))]

if findings:
    print("Security gate findings:")
    for f in findings:
        print(f"  {f}")
    print("Self-heal: redact secrets from source, commit, re-run /polaris.merge, and rotate any credential seen in history.")
if not Path(".polaris/memory/constitution.md").exists():
    print("Warning: no constitution.md - run /polaris.constitution")
print(f"Security gate complete. {len(changed)} files scanned. Proceeding with merge.")
```

## What This Command Does

Detects the feature branch and worktrees, runs preflight validation, determines merge order from WP dependencies (forecast with `--dry-run`), switches to the target branch (from meta.json, fallback `main`), updates it with `git pull --ff-only`, merges with the chosen strategy (auto-resolving status-file conflicts), then optionally pushes and removes worktrees and branches.

## Options

Key options (`polaris merge --help` for the full set): `--strategy merge|squash|rebase` (default `merge`), `--push`, `--dry-run` (preview), `--target <branch>`, `--feature <slug>` (from the target branch), `--resume`, `--keep-branch`, `--keep-worktree`.

On a merge conflict: resolve files, `git add`, then `git commit` (the `commit-msg` hook adds the Aptean Polaris trailer). For any other blocked step the CLI prints a verdict - follow its `remediation` and retry once.

## Typical Flow

```bash
/polaris.accept --mode local
/polaris.merge --push
```


**Telemetry**: Run: `polaris telemetry record merge --feature <slug> --phase complete --agent {{AGENT_NAME}}`
