---
description: Create an isolated workspace (worktree) for implementing a specific work package.
---

## Advanced Command Gate

Load `references/advanced-gate.md` and apply the `advanced_commands` gate (bypassed for autopilot or NL routing).

## Model Guidance

Model: impl tier (see references/model-selection.md); routing is enforced by the launcher.

## Output Style

One line per action (`Written: <path>` or the command result); no preamble, narration, or code explanation; two lines max on failure; no extended thinking on code/test/commit steps.

---

## Domain Expert and Subagent Check

Read WP frontmatter before starting. Load `@references/implement-domain-subagent.md` for the full domain expert and subagent protocols.

---

## Working Directory

**Telemetry**: Run: `polaris telemetry record implement --feature <slug> --phase start --agent {{AGENT_NAME}} --wp <WP_ID>`

Two modes:

- **Default (worktree)**: `polaris implement WP##` creates `.worktrees/###-feature-WP##/`; `cd` into it for all file operations.
- **In-place**: `polaris implement WP## --in-place` skips the worktree (use when worktrees cause issues or only one WP is in flight).

---

Run the workflow command to get the WP prompt:

```bash
polaris agent workflow implement $ARGUMENTS --agent <your-name>
```

The output ends with the completion command for moving to `for_review` - read to the end.

---

## Decision Memory and Failure Awareness (only if memory files exist)

Skip if neither `.polaris/memory/decisions-summary.md` nor `.polaris/memory/failures-summary.md` exists.

- **decisions-summary.md**: consult before non-trivial architectural choices; follow prior decisions; record new ones via `polaris memory record-decision`
- **failures-summary.md**: apply recorded fixes for matching error signatures; resolve new ones via `polaris memory resolve-failure`

---

## Pre-Implementation Context

If NOT WP01 and `control-map.md` exists, read it and the relevant shared dependency files for consistency.

## Code Intelligence Graph

Query the local code graph instead of grepping blind (each verb prints JSON, exit 2 if unavailable): `polaris graph symbol <name>` (definition), `who-imports <file>` (callers), `impact <file>` (blast radius), `tests-for <file>` (covering tests).

## Commit Workflow

**BEFORE moving to for_review**, commit your implementation. Then run:

```bash
polaris runtests --feature <slug>
```

The CLI owns the transition: a passing run moves the WP to `for_review`; a failing run keeps it in `doing`. Do not edit lane status by hand or pass human-override flags.

**Docs-only WP** (`test_status: "skipped"`) - submit with the verdict emitted as JSON:
```bash
polaris agent tasks move-task WP## --to for_review --no-test-reason "docs-only WP" --json
```

On `allowed: false`, follow each result's `remediation`, fix the cause, and retry once.

---

**Telemetry**: Run: `polaris telemetry record implement --feature <slug> --phase complete --agent {{AGENT_NAME}} --wp <WP_ID>`
