## Separation of Duties Check

Before proceeding with acceptance, verify SoD compliance:
1. Resolve your identity: run `git config user.email`.
2. Check the audit trail at `.polaris/audit-trail/<feature-slug>.jsonl` for this feature's implementer and reviewer.
3. The acceptor must be different from BOTH the implementer and the reviewer.
4. If your email matches either:
   - If `quality.sod_enforcement` is enabled in `.polaris/config.yaml`: STOP and reject.
   - If disabled (default): warn "SoD: acceptor overlaps with implementer/reviewer" but allow acceptance.
   - Override: use `--self-review` flag with a justification string (recorded in audit trail with `override: true`).
5. The acceptance action is automatically recorded in the audit trail.

## Discovery (mandatory)

Before running the acceptance workflow, gather:

1. **Feature slug** (e.g., `005-awesome-thing`). If omitted, detect automatically.
2. **Acceptance mode**: `pr` (merge via PR), `local` (local merge), or `checklist` (readiness check only).
3. **Validation commands executed** (tests/builds). Collect each command verbatim; omit if none.
4. **Acceptance actor** (optional, defaults to current agent name).

Ask one focused question per item and confirm the summary before continuing. End with `WAITING_FOR_ACCEPTANCE_INPUT` until all answers are provided.

**Autopilot Context**: under `AUTOPILOT_RUN` with an approved `autopilot:` spec block, SKIP this discovery and do NOT emit `WAITING_FOR_ACCEPTANCE_INPUT`. Use `autopilot.accept.mode` (default `local`) as the mode, pass `--revalidate` when `autopilot.accept.revalidate` is true, and auto-detect the feature slug. If acceptance returns outstanding blockers that autopilot cannot fix, load `@references/escalation.md` and run `polaris agent escalate --feature <slug> --stage accept --reason "<blockers>"`, then STOP.

## Execution Plan

1. Compile the acceptance options into an argument list:
   - Always include `--actor "claude"`.
   - Append `--feature "<slug>"` when the user supplied a slug.
   - Append `--mode <mode>` (`pr`, `local`, or `checklist`).
   - Append `--test "<command>"` for each validation command provided.
2. Run `polaris accept` (the CLI wrapper) with the assembled arguments **and** `--json`.
3. Parse the JSON response:
   - `summary.ok` (boolean) and readiness details.
   - `summary.outstanding` categories when issues remain.
   - `instructions` (merge steps) and `cleanup_instructions`.
   - `notes` (e.g., acceptance commit hash).
4. Present the outcome:
   - If `summary.ok` is `false`: list each outstanding category and advise the user to resolve them.
   - If `summary.ok` is `true`: display acceptance timestamp, actor, commit hash, merge instructions, cleanup instructions, and validation commands executed.
5. When the mode is `checklist`: make it clear no commits or merge instructions were produced.

Acceptance re-verifies each done/for_review WP's test evidence and the feature's `verification.json` ledger (every acceptance criterion must be verified or waived, and the ledger must be untampered) before it can pass; features predating the ledger fall back to evidence-only mode with a warning.

## Output Requirements

- Summaries in plain text (no tables). Short bullet lists for instructions.
- Surface outstanding issues before any success messages.
- If the JSON payload includes warnings, surface them under an explicit **Warnings** section.
- Never fabricate results; only report what the JSON contains.

## Error Handling

- If the command fails or returns invalid JSON: report the failure and request user guidance (do not retry automatically).
- When outstanding issues exist: do NOT force acceptance - return the checklist and prompt the user to fix blockers.
