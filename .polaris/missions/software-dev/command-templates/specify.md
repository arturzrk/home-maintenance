---
description: Create or update the feature specification from a natural language feature description.
---

## Model Guidance

Model: plan tier (see references/model-selection.md); routing is enforced by the launcher.

---

## User Input

**Telemetry**: Run: `polaris telemetry record specify --feature <slug> --phase start --agent {{AGENT_NAME}}`

```text
$ARGUMENTS
```

You **MUST** consider the user input before proceeding (if not empty).

## Amend Mode

If `$ARGUMENTS` contains `--amend`, skip Discovery Gate and load `@references/specify-amend-mode.md`. Mutually exclusive with `--quick`.

## Quick Mode

If `--quick` or "quick" in arguments: ask only (1) What is the feature? (2) Key acceptance criteria. Generate spec.md and plan.md together directly. No discovery rounds.

## Working Directory

Run from the **main repository** root (planning repo). Artifacts go to `polaris-specs/###-feature/`. Worktrees created later during `/polaris.implement`. `meta.json` records explicit `"target_branch": "<branch>"` and `"vcs": "git"` fields, plus an optional graph-derived `scope_hint` (module/file COUNTS only) nested in the `estimation` block (see `@references/specify-workflow.md` step 3); commit planning artifacts to main once spec.md is finalized. **Guard (inviolable)**: a graph-derived file path, symbol name, or module list must NEVER appear in `spec.md`; the code-graph hint feeds `meta.json` estimation metadata ONLY.

## Discovery Gate

Conduct a structured discovery interview scaled to complexity:

- **Trivial**: 1-2 questions | **Simple**: 2-3 | **Complex**: 3-5 | **Critical**: 5+

Rules: present ALL questions as a numbered list in one message. End with `WAITING_FOR_DISCOVERY_INPUT`. Use informed defaults for unanswered questions.

**Always include**: tracker item question (ADO: AB#12345, GitHub: #42, Jira: PROJ-123, or 'skip') and estimation question ("team estimate without AI, e.g. '3 days'").

**Also capture autopilot parameters** (retry, on-failure, mode, scope, accept mode, qa/fix policy, ship env) as Intent Summary fields WITH DEFAULTS - see `@references/specify-workflow.md` (Autopilot Parameters). Ask a field only if the description leaves it ambiguous. Persisted to `spec.md` `autopilot:` frontmatter on approval; this is the SINGLE gate. Under `AUTOPILOT_RUN` with an approved spec, skip re-interview.

## Mission Selection

After discovery: **software-dev** (features, APIs, apps) or **research** (investigations, analysis). Confirm unless explicit.

---

Load `@references/specify-workflow.md` and execute the Workflow through On Completion.

**Telemetry**: Run: `polaris telemetry record specify --feature <slug> --phase complete --agent {{AGENT_NAME}}`
