---
description: Perform structured code review and kanban transitions for completed task prompt files
---

## Advanced Command Gate

Load `references/advanced-gate.md` and apply the `advanced_commands` gate (bypassed for autopilot or NL routing).

## Model Guidance

Model: plan tier (see references/model-selection.md); routing is enforced by the launcher.

---

## Step 1: Claim the WP for review

```bash
polaris agent workflow review $ARGUMENTS --agent <your-name>
```

No WP ID: auto-finds the first `for_review` WP and moves it to doing.

## Step 2: Separation of Duties Check

**Telemetry**: Run: `polaris telemetry record review --feature <slug> --phase start --agent {{AGENT_NAME}} --wp <WP_ID>`

Compare `git config user.email` against this WP's implementer in `.polaris/audit-trail/<feature-slug>.jsonl`. If they match: STOP when `quality.sod_enforcement` is enabled, otherwise warn and proceed. Override: `--self-review "<justification>"` (recorded in the audit trail).

## Step 3: Dependency checks

<!-- dependency_check -->
- Confirm each dependency WP is merged into the feature branch first (deps merge into the feature branch; it merges to main later via `/polaris.merge` or a PR).
<!-- dependent_check -->
- Note any WPs depending on this one and their lanes.
<!-- rebase_warning -->
- If requesting changes with dependents, warn them to rebase.
<!-- verify_instruction -->
- Verify dep declarations match actual code coupling.

## Step 4: Read implementation

Switch to the WP branch and read ALL changed files via `git diff`. Assess blast radius with the local code graph (JSON output, exit 2 if unavailable): `polaris graph impact <file>` (dependents), `who-imports <file>` (callers), `tests-for <file>` (covering tests).

## Step 5: Multi-Persona Review Passes

Load `@references/review-passes.md` for full criteria. Perform all 4 passes in order, each with its own heading and verdict; do NOT skip or combine them. The `review_passes` config gates which passes are required (default: all 4).

## Step 6: Complete the review

Run move-task with `--json` and read the verdict; on `allowed: false` follow each `remediation`, fix, and retry once. Never pass human-override flags.

- **APPROVED**: `polaris agent tasks move-task WP## --to done --note "Review passed: <summary>" --json`. The CLI runs the done-gate (subtasks, clean and synced worktree, an implementation commit, review and tests gates); an unmerged WP without fresh passing evidence is denied. This mirrors autopilot Stage 3 ordering: the WP branch is merged into the feature branch and evidence is recorded on the rebased HEAD before the move to done.
- **REJECTED**: write feedback to a temp file, then `polaris agent tasks move-task WP## --to planned --review-feedback-file <path> --json`.

**Telemetry**: Run: `polaris telemetry record review --feature <slug> --phase complete --agent {{AGENT_NAME}} --wp <WP_ID>`

## Step 7: Emit the machine-readable verdict

End with EXACTLY ONE fenced JSON block and no prose after it - the only signal the quality gate reads; a missing, malformed, or duplicated block counts as REJECTED.

```json
{"verdict": "APPROVED", "reasons": ["all required passes PASS"], "evidence_checked": true}
```

Use `"verdict": "REJECTED"` with blocking items in `reasons` when any required pass fails. Set `evidence_checked` true only if you ran the tests and inspected the diff.
