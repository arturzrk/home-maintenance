## Autopilot Context

If the invoking context contains `AUTOPILOT_RUN` AND `spec.md` already carries an approved `autopilot:` frontmatter block, this specify stage is already satisfied - do NOT re-interview and do NOT emit `WAITING_FOR_DISCOVERY_INPUT` / `WAITING_FOR_PLANNING_INPUT`. Otherwise run discovery and the approval handoff below exactly as today: this discovery + approval is the SINGLE human gate of the pipeline. If discovery cannot proceed at all under `AUTOPILOT_RUN` (no spec and no human), load `@references/escalation.md` and escalate instead of waiting.

## Autopilot Parameters (Intent Summary)

The discovery interview also captures the execution parameters for the unattended pipeline as Intent Summary fields WITH DEFAULTS. Ask a field ONLY when the user's description leaves it ambiguous; otherwise use the default silently.

| Field | Default | Notes |
|---|---|---|
| retry_limit | 3 | attempts per stage before escalation |
| on_failure | skip-and-continue | or `halt` |
| mode | standard | or `quick` (2-question specify, combined spec+plan) |
| scope | all | `all` pending WPs, or a named WP list / description |
| accept.mode | local | `local` / `pr` / `checklist` |
| accept.revalidate | true | autopilot passes `--revalidate` to accept |
| qa.base_url | (from `qa:` config) | E2E base URL default |
| qa.auth_required | false | E2E login default |
| fix.auto_rca | true | auto-proceed root-cause analysis under autopilot |
| ship.env_progression | dev | `dev` / `tst` / `all` |

On approval (the `y` handoff below), persist these into `spec.md` frontmatter:

```yaml
autopilot:
  approved: true
  approved_at: "<ISO8601>"
  retry_limit: 3
  on_failure: skip-and-continue
  mode: standard
  scope: all
  accept:
    mode: local
    revalidate: true
  qa:
    base_url: ""
    auth_required: false
  fix:
    auto_rca: true
  ship:
    env_progression: dev
```

Use `polaris agent feature ...` frontmatter helpers or write the block directly under the spec frontmatter. This block is the authorization record autopilot reads; NEVER re-interview a feature that already has it (existing specs lacking the block get the documented defaults above).

## Workflow

**Write early, write often.** Context windows can drop mid-conversation. Every file write is a checkpoint.

1. **Check discovery status** - stay in question loop until Intent Summary confirmed

2. **Create feature** (once discovery complete):
   ```bash
   polaris agent feature create-feature "<slug>" --json
   ```
   Parse JSON for `feature`, `feature_dir`, `target_branch`. Run ONCE only.

3. **Create meta.json** in feature dir (required fields):
   ```json
   {
     "feature_number": "<number>", "slug": "<full-slug>",
     "friendly_name": "<Title>", "mission": "<mission>",
     "source_description": "$ARGUMENTS",
     "created_at": "<ISO>", "target_branch": "<current-branch>", "vcs": "git"
   }
   ```
   Add only the detected tracker field (`ado_work_item`, `github_issue`, or `jira_issue`).
   When an estimate was captured, add: `"estimation": {"baseline_raw": "3 days", "baseline_hours": 24, "source": "developer|ado", "captured_at": "<ISO>"}` (normalize to hours; used by polaris estimation reporting).

   **Scope hint (US3, FR4-FR6, optional)**: when the code graph is enabled, call
   `estimation.graph_scope_hint(repo_root, description)`. If it returns a dict
   (not `None`), nest it under the same `estimation` block as counts only:
   `"estimation": {..., "scope_hint": {"modules": 3, "files": 12, "source": "code-graph"}}`.
   Omit `scope_hint` entirely when the helper returns `None` (kill-switch off,
   or graph absent/empty/broken) - never write a placeholder value. **Guard
   (inviolable)**: the scope hint is COUNTS ONLY; no graph-derived file path,
   symbol name, or module list may ever appear in `meta.json` or in `spec.md` -
   the hint feeds estimation metadata alone, never the spec body.

   **Write discovery notes now:** Save `<feature_dir>/discovery-notes.md` with Intent Summary and Q&A. Delete after spec.md is finalized.

4. **Generate spec** from discovery answers:
   - Identify actors, actions, data, constraints, success metrics
   - Fill: User Scenarios, Functional Requirements (testable), Success Criteria (measurable, tech-agnostic), Key Entities
   - **Write to `<feature_dir>/spec.md` immediately.**

5. **Control map** (if 2+ interrelated flows/forms/screens): Create `<feature_dir>/control-map.md`. Skip if single-flow feature.

6. **Validate spec** against quality checklist:
   - Requirements testable, success criteria measurable and tech-agnostic
   - If items fail: fix spec.md and re-validate (max 3 iterations)
   - Save checklist to `<feature_dir>/checklists/requirements.md`

7. **Auto-review**: Re-read spec end-to-end, identify gaps, update spec.md. Delete `discovery-notes.md` once finalized.

## Phase 2: Implementation Planning

Proceed directly to planning (eliminates separate `/polaris.plan` step).

**Planning interrogation** - scaled to complexity: suggest options and confirm rather than open-ended questions. End each question with `WAITING_FOR_PLANNING_INPUT`. Summarize into **Engineering Alignment** note and confirm. (Under `AUTOPILOT_RUN` with an approved `autopilot:` block, skip the interrogation entirely: use informed defaults and do NOT emit `WAITING_FOR_PLANNING_INPUT`.)

**Plan generation**:
1. Run `polaris agent feature setup-plan --feature <feature-slug> --json`
2. Read spec and `.polaris/memory/constitution.md` (if exists)
3. Update Technical Context, Constitution Check, generate research.md (if unknowns), data-model.md, contracts/, quickstart.md
4. Commit planning artifacts

## Spec Guidelines

- Focus on **WHAT** and **WHY**, never HOW
- Written for business stakeholders; mandatory sections completed
- Make informed guesses using industry standards; document in Assumptions
- Success criteria: measurable, tech-agnostic, user-focused, verifiable

## On Completion - the single approval gate

This handoff IS the one human gate of the pipeline. Before asking, persist the approved `autopilot:` frontmatter block (see Autopilot Parameters) into `spec.md`.

- If `--no-continue`: STOP and report spec path (no autopilot).
- Default: present the confirmed Intent Summary (including the autopilot parameters) and ask plainly:

  "Spec, plan, and autopilot parameters are ready. Replying `y` AUTHORIZES the full unattended pipeline: autopilot will implement every work package, run and verify tests, self-review, review, accept (with `--revalidate`), and prepare a merge-ready branch WITHOUT any further prompts, escalating only if it gets stuck. Proceed with autopilot? (y/n)"

  - **y**: this is the authorization. Set `autopilot.approved: true` (with `approved_at`) in `spec.md` frontmatter, then launch `/polaris.autopilot` for the current feature (autopilot delegates with the `AUTOPILOT_RUN` marker so downstream stages run unattended).
  - **n**: stop and report the spec path (frontmatter still records the parameters but `approved: false`, so a later autopilot run asks once more).
