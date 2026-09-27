---
description: Run the full Aptean application pipeline autonomously with retry logic.
---

## Model Guidance

Model: impl tier, with delegated spec/plan/tasks stages routed to the plan tier (see references/model-selection.md); routing is enforced by the launcher.

## Output Style

One line per action: `Written: <path>`, `Run: <cmd> -> <result>`, `WP## done`, `Stage <N> done`. On failure: two lines max. No preamble, no narration, no mid-pipeline summaries. No extended thinking for implementation steps.

---

## User Input

**Telemetry**: Run: `polaris telemetry record autopilot --feature <slug> --phase start --agent {{AGENT_NAME}}`

```text
$ARGUMENTS
```

You **MUST** consider the user input before proceeding (if not empty).

## Quick Mode

If `--quick` is passed: use quick mode for the specify stage (2 questions max, combined spec+plan). All downstream stages run normally with full quality gates.

## Discovery (read the approved spec - do NOT re-interview)

Read `spec.md` `autopilot:` frontmatter for retry_limit, on_failure, mode, scope, and accept/qa/fix/ship policy. Existing specs lacking the block: apply documented defaults (retry 3, on-failure skip-and-continue, mode standard) and NEVER re-interview. Detect the feature slug from directory/arguments; `--resume`/`--abort` load saved state.

Emit `WAITING_FOR_AUTOPILOT_INPUT` ONLY when no approved spec exists and one cannot be created here. Otherwise run Stage 2a (`/polaris.specify`), which carries the single approval gate.

When delegating to ANY sub-command, prepend an `AUTOPILOT_RUN` context line (with the feature slug) so gated commands use recorded defaults and never emit a `WAITING_FOR_*` sentinel.

## Pipeline Stages

### Stage 1: Setup (new apps only)

Run `/polaris.setup` with Aptean defaults. Skip if `.polaris/` exists.

### Stage 2: Build

**2a. Specify + Plan** (`/polaris.specify`): Create spec with auto-review, generate plan. Skip if both exist.

**2b. Tasks** (`/polaris.tasks`): Generate WPs. Run `polaris agent feature finalize-tasks --json`. Skip if `tasks.md` exists.

**2c. Test Plan**: Create `polaris-specs/<slug>/test-plan.md` with Unit Tests (min 2/WP), Integration Tests, Acceptance Tests, Edge Cases. All names start `test_`, all unique. Skip if exists.

**2d. Implement**: For each WP in dependency order:
1. `polaris implement <WP_ID> --feature <slug>` (add `--base <dep>` if deps). Verify `doing`.
2. **Worktree handoff**: parse `cd` from output, execute, verify `pwd` contains `.worktrees/` and branch is NOT `main`. If verification fails, STOP.
3. Load `references/autopilot-wp-delegation.md`; delegate the WP (implement + test/fix loop + self-review) to one subagent in the worktree per that protocol, or follow its inline fallback when no subagent tool is available.
4. **Return to main repo root** before next WP
5. On failure: retry up to limit, then mark failed and skip dependents

### Stage 3: Quality Assurance and Ship

**Delegating agents**: 3a (test/fix loop) and 3b (self-review) already ran inside the Stage 2d subagent; the main session only records the lane transitions and runs the gates.

**3a. Test Execution** (per WP):
1. `polaris agent tasks move-task <WP_ID> --to testing`
2. `python .polaris/scripts/tasks/run_tests.py --project-root . --wp <WP_ID> --json` - gate: `success: true, failed: 0`
3. Run E2E if `.e2e.js` files exist: `polaris runtests --wp <WP_ID> --feature <slug>`
4. On failure: move back to doing, fix, retry (max 3)
5. Verified completion: `run_tests.py` (project suite) and `polaris runtests` (E2E) write test-evidence artifacts under `.polaris/test-evidence/` and update the `verification.json` ledger. The WS1-B lane gate blocks the move to `for_review` for code-touching WPs without fresh, passing evidence; hollow (assertion-free) test files report as skipped and hard-fail the gate.

**3b. Self-Review** (after tests pass):
Check: every acceptance criterion implemented, no unintended changes, no hardcoded values/secrets, follows constitution standards. Fix issues, re-run tests if needed. Move to `for_review` with self-review note.

**Kanban flow**: planned -> doing -> testing -> (doing if fail) -> for_review -> done

**3c. Review** (`/polaris.review`): For each WP in for_review. Reject -> doing, fix, restart 3a. Approve -> continue to Merge-then-verify below (do not move the WP to done directly - the WS1-B gate needs merge-before-evidence, not evidence-before-merge).

**Merge-then-verify** (per approved WP, gate-consistent order - the WS1-B freshness check needs the evidence `head_sha` to be an ancestor of current HEAD, which only holds once merge and rebase land first):
1. Merge the WP branch into the feature branch: from the feature branch, `git merge --no-ff <feature>-<WP_ID> -m "Merge <WP_ID> into <feature>"`.
2. Rebase the WP worktree onto the updated feature branch: `git rebase <feature>` (collapse-to-tip expected - the WP's commits are already in the feature branch, so the worktree HEAD lands on the same commit as the feature branch tip).
3. Record FRESH verified-completion evidence on the rebased HEAD from the worktree: `polaris runtests --wp <WP_ID> --feature <slug>`.
4. Commit gate outputs: stage and commit any newly written `.polaris/test-evidence/` and `verification.json` changes so the passing state is captured in the branch.
5. `polaris agent tasks move-task <WP_ID> --to done --note "Review passed: <summary>"`.

**Remedies**:
- Empty integration marker: if the WP changed no non-exempt files (docs-only) and a pre-gate step demands a commit beyond base to merge/rebase, create an empty commit on the WP branch as the integration marker, run steps 1-5 above, then `git reset --hard <feature-ancestor>` in the WP worktree afterward to drop the local marker (the feature branch keeps its merge commit; only the now-unneeded WP worktree branch is reset).
- Manual lane commit on `UnicodeDecodeError`: if `move-task --to done` crashes with `UnicodeDecodeError` after applying the lane change, commit the lane-state change manually (never `--no-verify`) so the transition is not lost. WP01 of feature 095 fixes the encoding root cause; treat this as a fallback until that ships.

**3d. Accept** (`/polaris.accept --revalidate`): Once all WPs pass review. Autopilot passes `--revalidate` (WS1-B) by default (per `autopilot.accept` frontmatter) so the E2E suite and `--test` commands are re-run.

**3e. Merge** (`/polaris.merge`): Preflight, finalize the feature branch (WPs already merged into it above), clean up worktrees.

## State Persistence

Load `@references/autopilot-state.md` for the full state schema. Resume: `--resume`. Abort: `--abort`.

## Final Summary

Display: pipeline stages, succeeded/failed/skipped WP counts, per-WP test results (run/passed/failed), failed WP details with error and impact, next steps.

## Error Handling - route ALL failures through escalation

Never halt silently. Never lose work. State updated atomically. All commands cross-platform.

Load `@references/escalation.md`. On ANY stage failing more than `retry_limit`, or ANY gate that cannot auto-resolve, run `polaris agent escalate --feature <slug> --stage <stage> --reason "<why>" --attempts <n>` then STOP. Escalation records the state block, report, console block, and optional webhook and exits cleanly so `--resume` continues from the recorded point.

**Telemetry**: Run: `polaris telemetry record autopilot --feature <slug> --phase complete --agent {{AGENT_NAME}}`
