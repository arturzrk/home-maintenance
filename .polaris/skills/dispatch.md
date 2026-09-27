# Polaris Native Dispatch & Memory Guardrail

This guardrail adds native parallel dispatch and local cross-session memory to every
polaris command. All sections are **additive** - they run **before and after** the
normal command workflow. They never replace or override existing command instructions.

Everything here runs with the agent's own tools only. There is NO external process,
NO package manager, NO network call, and NO background daemon. Parallel work is
dispatched with the agent's native Task tool; memory lives in local files under
`.polaris/memory/`.

---

## Section 0: Command Router

**WHEN**: At the very start of every polaris command invocation, before Section 1.

Identify the active command and look it up in the routing table below. Set two variables:
- `DISPATCH_CLASS`: The resolved class (A / B / C / D)
- `PRIMARY_ROLE`: The primary agent role for dispatch (Class A/C only; `-` for Class B and D)

If the command is not found in the table, default to **Class A** with
`PRIMARY_ROLE = architect` and log:
`[polaris-dispatch] Unknown command - defaulting to Class A.`

### Routing Table

| Command | Class | Primary Role |
|---------|-------|-------------|
| `/polaris.specify` | A | architect |
| `/polaris.clarify` | A | architect |
| `/polaris.plan` | A | architect |
| `/polaris.tasks` | A | architect |
| `/polaris.restructure` | A | architect |
| `/polaris.constitution` | A | architect |
| `/polaris.research` | A | researcher |
| `/polaris.analyze` | A | analyst |
| `/polaris.assess` | A | analyst |
| `/polaris.standards` | A | reviewer |
| `/polaris.devops` | A | coder |
| `/polaris.integrate` | A | coder |
| `/polaris.newapp` | A | coder |
| `/polaris.newmcp` | A | coder |
| `/polaris.scaffold` | A | coder |
| `/polaris.healthcheck` | A | coder |
| `/polaris.migrate` | A | coder |
| `/polaris.skill` | A | coder |
| `/polaris.implement` | B | - |
| `/polaris.review` | C | reviewer |
| `/polaris.accept` | C | reviewer |
| `/polaris.checklist` | C | reviewer |
| `/polaris.merge` | D | - |
| `/polaris.onboard` | D | - |
| `/polaris.runtests` | D | - |
| `/polaris.status` | D | - |
| `/polaris.dashboard` | D | - |

### Class D Passthrough

If `DISPATCH_CLASS == D`:

1. Print: `[polaris-dispatch] Passthrough - single-agent for this command.`
2. Proceed to Section 1 (memory recall) - run normally.
3. Execute the command's normal workflow immediately.
4. **Skip** Sections 2 and 3 (all parallel dispatch sections).
5. Run Section 5 (memory write) as normal.

For Class A, B, and C: proceed to Section 1.

---

## Section 1: Memory Recall (Command Start)

**WHEN**: At the start of every command, before any creative work begins.

Read the two local memory summary files if they exist. These are plain markdown
maintained by the `polaris memory` CLI - no network, no external tools:

- `.polaris/memory/decisions-summary.md`
- `.polaris/memory/failures-summary.md`

Rules:
- Read at most the **most recent 40 lines** of each file (bounded excerpt). If a file
  is shorter, read all of it. If a file is absent, skip it silently and print:
  `[polaris-memory] Recall skipped - no summary file.`
- Incorporate recalled decisions and prior failures into working context before
  producing output.
- If a recalled entry conflicts with the current spec: surface the conflict to the
  developer, then treat the current spec as authoritative unless the developer says
  otherwise.
- Never fail the command because memory recall failed. Recall is best-effort.

---

## Section 2: Command-Level Specialist Panel (OPT-IN)

**APPLIES TO**: All **Class A** and **Class C** commands (see Section 0 routing table).

**NOT USED BY**: Class B (`/polaris.implement`) - uses Section 3 instead.
Class D commands - use passthrough (Section 0).

### Config gate (default OFF)

Read `.polaris/config.yaml`. The specialist panel triples token cost, so it is
**opt-in and OFF by default**:

```yaml
dispatch:
  specialist_panel: true    # default is false / absent
```

- If `dispatch.specialist_panel` is missing, `false`, `no`, or `0`:
  **run the command as a single agent** (the normal command workflow). Print:
  `[polaris-dispatch] Specialist panel off - single-agent (set dispatch.specialist_panel: true to enable).`
  Then execute the normal workflow and continue to Section 4.
- Only if `dispatch.specialist_panel` is explicitly `true` (or `yes` / `1`):
  run the panel below.

### Panel (only when enabled)

Spawn 3 specialist sub-agents in **ONE Task tool message** (all parallel - do not split
across multiple messages):

| Role | Agent Role | Responsibility |
|------|-----------|----------------|
| Researcher | `researcher` | Use Section 1 recall, gather prior artifacts, surface relevant prior decisions |
| Primary | `PRIMARY_ROLE` (from Section 0 routing table) | Produce the main output artifact (spec.md, plan.md, tasks.md, review findings, etc.) |
| Reviewer | `reviewer` | Validate output against spec FRs, prior decisions, and template requirements |

**Collecting results** (wait - do NOT poll mid-way):
1. Wait for all 3 agents to complete.
2. Synthesise their outputs into the final artifact (see Section 4).
3. If a specialist fails: complete using the remaining agents; note the missing
   specialist in the artifact's Assumptions section.

### Named subagent types (model-pinned)

Polaris ships pinned subagent definitions in `.claude/agents/` (Claude Code). Each
pins its model via routing so dispatched work runs at the right tier:

- `polaris-planner` - plan model (architecture, spec, tradeoffs)
- `polaris-researcher`, `polaris-implementer`, `polaris-reviewer` - implementation model

When you dispatch with the Task tool, prefer these named subagent types: map the
Researcher to `polaris-researcher`, the Reviewer to `polaris-reviewer`, and the
PRIMARY_ROLE to `polaris-planner` (planning classes) or `polaris-implementer`
(coding classes). This keeps model routing mechanical instead of prose.

---

## Section 3: WP-Level Parallel Dispatch

**APPLIES TO**: `/polaris.implement` ONLY

**WHEN**: After Section 1.

### Step A - Parse Dependency Graph

Read all `polaris-specs/<feature>/tasks/WP*.md` files.
Extract `depends_on` from each file's YAML front matter:

```yaml
---
wp: WP02
lane: planned
depends_on: [WP01]   # absent or [] = independent
---
```

Build a directed graph: WP -> depends_on edges.

### Step B - Detect Circular Dependencies

Run a topological sort over the graph. On cycle detected:

```
[polaris-dispatch] Circular dependency: <cycle>. Falling back to sequential.
```

Fall back to sequential single-agent execution for all WPs in the feature.

### Step C - Dispatch Independent WPs

All WPs with empty or absent `depends_on` -> dispatch **simultaneously** in **ONE
Task tool message**. Example:

```
[SINGLE MESSAGE - all three in parallel]
Agent A: polaris implement WP01
Agent B: polaris implement WP03
Agent C: polaris implement WP05
```

**Never call the Task tool multiple times for different independent WPs - always one
message.** Cap concurrent WP agents at **8**.

### Step D - Dispatch Dependent WPs

Monitor lane status. When a WP transitions to `for_review` or `done`:
- Check if any queued WP's full `depends_on` list is now satisfied.
- If yes: dispatch that WP immediately.
- If active agent count would exceed 8: queue and dispatch when a slot frees.

### Step E - Handle WP Agent Failure

If a WP agent fails mid-execution:
- Revert that WP's lane to `planned`.
- Print: `[polaris-dispatch] WP## agent failed - reverted to planned.`
- All other WP agents continue unaffected.

---

## Section 4: Result Synthesis & Conflict Resolution

**WHEN**: After all sub-agents (Section 2) or WP agents (Section 3) complete.

**For command-level panels (Section 2, when enabled)**:
- Merge researcher context + primary artifact + reviewer feedback into the final
  output artifact.
- Conflict resolution: most-recent specialist output wins; the losing output is logged
  in the artifact's Assumptions section.
- The final artifact **must be format-identical** to what single-agent execution
  produces. No new sections, no extra headings - same structure, richer content.

**For WP-level dispatch (Section 3)**:
- Each WP agent writes independently to its own worktree. No synthesis needed.
- Report completion status to the developer.

---

## Section 5: Memory Write (Command End)

**WHEN**: After the final output artifact is written to disk.

Record key decisions locally with the existing Polaris memory CLI. This writes to
`.polaris/memory/` and regenerates the summary files read in Section 1. No network,
no external tools:

```bash
polaris memory record-decision \
  --context "<what this command was doing>" \
  --decision "<what was chosen>" \
  --rationale "<why this option>" \
  --tags "<command>" --tags "<domain>" \
  --feature "<feature-slug>"
```

Rules:
- Record only genuinely reusable decisions - not routine mechanics.
- `--alternatives` may be repeated for each option considered (optional).
- If the command discovered a repeatable failure and its fix, also run
  `polaris memory record-failure` / `polaris memory resolve-failure` as appropriate.
- Memory write is best-effort: never fail the command because the write failed.

---

*End of Polaris Native Dispatch & Memory Guardrail*
