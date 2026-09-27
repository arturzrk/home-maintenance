# Autopilot: Per-WP Subagent Delegation

Loaded on demand by autopilot Stage 2d. Defines how skill-mode autopilot delegates a
work package's implement + test/fix loop + self-review to ONE subagent operating in
the WP worktree, what that subagent must return, how parallel-group WPs flatten, how
retries are seeded, and what the main session never delegates.

## 1. Capability precondition and degradation (FR8)

If your environment provides a subagent delegation tool (the Agent tool), delegate the
work package to one subagent using the prompt skeleton in section 2. Otherwise, degrade:
If your environment does not provide a subagent delegation tool, execute the work package inline using the fallback steps below.

**Inline fallback steps (the prior Stage 2d execution, preserved verbatim):**

3. Follow implementation prompt from tasks file
4. Implement fully, run tests, commit

Under the fallback path the Stage 3a test/fix loop and the Stage 3b self-review also
run inline in the main session, exactly as they did before this reference existed.

## 2. Per-WP delegation prompt skeleton (main session -> WP subagent)

Build the delegation prompt with these parts, in order:

1. **Workspace binding**: the absolute worktree path from the `polaris implement`
   output. Instruct the subagent to confirm, before any write, that its working
   directory is under `.worktrees/` and the current branch is not `main`, and to STOP
   if either check fails.
2. **Work order**: a pointer to the WP prompt file and its frontmatter; the feature
   slug; the first 30 lines of `polaris-specs/<slug>/spec.md` (the existing
   implement-subagent convention).
3. **Domain context**: when the WP frontmatter names a `domain`/superpower, include
   that prompt following the `references/implement-domain-subagent.md` convention.
4. **Obligations**: implement the WP fully; run
   `python .polaris/scripts/tasks/run_tests.py --project-root . --wp <WP_ID> --json`
   until it reports `success: true, failed: 0` (and E2E via
   `polaris runtests --wp <WP_ID> --feature <slug>` when a `.e2e.js` file exists);
   commit in the worktree (never `--no-verify`); perform the Stage 3b self-review
   checklist; write evidence via the same commands as inline execution.
5. **Depth constraint** (exact wording, do not reword):
   You are a subagent. Do NOT use the Agent tool.
6. **Report instruction**: emit ONLY the report described in section 3; never include
   raw diffs, file contents, or full test logs.

On retry (attempt N+1) the prompt additionally carries the Retry Seed from section 5
and nothing else from the failed attempt.

## 3. Report contract (FR2)

The subagent returns exactly this report: fixed field order, hard cap 40 lines
total, plain text or markdown. Field order:

- `WP: <WP##>`
- `OUTCOME: success | failure`
- `FAILURE_REASON: <1-3 lines, only when failure>`
- `FILES_CHANGED (<count>):` then one repo-relative path per line
- `COMMITS:` then one `<short-sha> <subject>` per line
- `TESTS: passed=<n> failed=<n> skipped=<n>` plus the EVIDENCE path
- `EVIDENCE: <path under .polaris/test-evidence/>`
- `SELF_REVIEW: pass | fixed-issues - <1 line>`
- `NOTES: <max 5 lines, optional>`

Exclusions - the report MUST NOT contain: raw diffs, file contents, full test logs,
transcripts, or tool output dumps.

Contract violation - a report missing a required field, or `OUTCOME: success` paired
with `failed>0`, no EVIDENCE path, or stale evidence, is invalid and consumes one
retry attempt (FR6).

## 4. Flattened-group protocol (FR5/FR7)

When the WP frontmatter carries `subagents: true`: the main session (autopilot) itself
launches one sibling subagent per `subagent_groups` entry, all simultaneously. Each
sibling receives the section 2 skeleton scoped to its group `tasks` and
`permitted_files`, and returns the existing group output format (files modified plus
one sentence per subtask). The main session waits for all siblings, resolves file
conflicts, and records the single consolidated commit itself. Nested delegation is
forbidden - a sibling subagent MUST NOT launch further subagents.

## 5. Retry seed (FR6)

On retry, the fresh subagent receives ONLY the prior report's `outcome`,
`failure_reason`, and `tests` fields. Never pass the failed transcript, accumulated
logs, or any other prior-attempt context.

## 6. Main-session obligations (FR4, never delegated)

The main autopilot session retains and MUST NOT delegate: `polaris implement` worktree
creation; kanban `move-task` transitions; the WS1-B lane gate; merge-then-verify
ordering (merge WP branch -> rebase worktree -> fresh evidence -> commit gate outputs
-> move to done); the review, accept, and merge stages; and escalation
(`polaris agent escalate`) when retry attempts are exhausted.
