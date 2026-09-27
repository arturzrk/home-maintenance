---
description: Fix a bug from an Azure DevOps work item with full traceability, kanban progress tracking, and status write-back.
---

## Model Guidance

Model: impl tier (see references/model-selection.md); routing is enforced by the launcher.

---


# /polaris.fix - Bug Fix from Azure DevOps Work Item

## User Input

```text
$ARGUMENTS
```

You **MUST** consider the user input before proceeding (if not empty).

## Autopilot Context

Under `AUTOPILOT_RUN` with an approved `autopilot:` spec, do NOT emit `WAITING_FOR_FIX_INPUT`. Missing bug ID / tracker token / tracker config: escalate via `polaris agent escalate --feature <slug> --stage fix` (see `references/escalation.md`) and STOP. Root-cause approval (Step 3.7): when `autopilot.fix.auto_rca` is true, auto-proceed and RECORD the RCA into the WP file instead of waiting; if the item lacks detail to diagnose, escalate. Human invocation is unchanged.

## Quick Mode

If user passes `--quick` or arguments contain "quick":
1. Parse the bug ID from arguments (same formats as below)
2. Fetch the ADO/Jira work item details (title, description, repro steps, acceptance criteria)
3. If the work item has enough detail (title + description or repro steps): skip discovery and implement directly from work item fields.
4. If the work item is missing key fields (no description AND no repro steps): fall back to normal discovery flow
5. All other phases (implementation, testing, kanban tracking, write-back) run normally

## Bug ID Parsing

Parse the bug ID from arguments. Supported formats:
- Numeric: `12345`
- Hash prefix: `#12345`
- AB prefix: `AB#12345`
- Full ADO URL: `https://dev.azure.com/{org}/{project}/_workitems/edit/12345`

Extract the numeric work item ID from any of these formats.

If no bug ID is provided, ask the user:
> "Please provide a bug ID. Accepted formats: `12345`, `#12345`, `AB#12345`, or a full Azure DevOps URL."

End with `WAITING_FOR_FIX_INPUT` and wait for a response.

## Failure Awareness (only if memory file exists)

Skip if `.polaris/memory/failures-summary.md` does not exist. If it exists, check for the error signature before debugging and apply any known fix first. After resolving, record:
```bash
polaris memory resolve-failure --signature <first-8-chars> --resolution "<what fixed it>"
```

---

## Prerequisites

### Issue Tracker Token Pre-flight

Check `.polaris/memory/constitution.md` for the configured issue tracker and validate credentials:

**Azure DevOps**: `AZURE_DEVOPS_PAT` env var with Work Items (Read) scope.
```python
python -c "import os, sys; pat=os.environ.get('AZURE_DEVOPS_PAT','').strip(); sys.exit(0) if pat else (print('AZURE_DEVOPS_PAT not set. Create a PAT at https://dev.azure.com with Work Items (Read) scope:\n  export AZURE_DEVOPS_PAT=your-token-here'), sys.exit(1))"
```

**Jira**: `JIRA_API_TOKEN` and `JIRA_EMAIL` env vars.
```python
python -c "import os, sys; t=os.environ.get('JIRA_API_TOKEN','').strip(); e=os.environ.get('JIRA_EMAIL','').strip(); sys.exit(0) if (t and e) else (print('Jira credentials missing. Set both:\n  export JIRA_API_TOKEN=your-api-token\n  export JIRA_EMAIL=your-email@company.com\nGenerate token at: https://id.atlassian.net/manage-profile/security/api-tokens'), sys.exit(1))"
```

If the token check fails, print the guidance, end with `WAITING_FOR_FIX_INPUT`, and wait.

### Issue Tracker Configuration

Read `.polaris/memory/constitution.md` for: ADO - `## Azure DevOps` (org URL + project); Jira - `## Jira` (base URL + project key). If missing, ask the user to run `/polaris.setup` or provide the details directly. End with `WAITING_FOR_FIX_INPUT` and wait.

## Workflow

Load `@references/fix-ado-workitem-integration.md` for the Azure DevOps fetch, mark-active,
and mark-resolved operations used in Steps 1, 3.5, and 10.

### Step 1: Fetch Work Item from Azure DevOps

Call the ADO REST API using the fetch operation (Operation 1 in the loaded reference),
including its 404/401/403/network-error handling.

**Non-Bug work item type**: If the work item type is not "Bug", warn:
> "Work item {id} is a '{type}', not a Bug. Proceeding anyway."

After the fetch, seed fix context: pipe the body to `polaris graph fix-context`; paste the `## Fix Context` section into working context (exit 2 = graph unavailable/disabled, proceed to RCA).

### Step 2: Create Branch

Capture the base branch first (used as `target_branch` in meta.json):

```bash
git branch --show-current
```

Store the output as `{base_branch}` (e.g., `main`).

Then create and switch to the fix branch:

```bash
git checkout -b fix/{id}-{kebab-title}
```

Verify branch: run `git branch --show-current`. Must show `fix/{id}-{kebab-title}`, NOT `main`/`master`. If still on main/master: STOP - branch creation failed. Do not proceed until confirmed on the fix branch.

### Step 3: Create Mini Polaris-Specs Entry

Now on the fix branch, create a polaris-specs entry for traceability:

```bash
polaris agent feature create-feature "fix-{id}-{kebab-title}" --json
```

Where `{kebab-title}` is the work item title converted to kebab-case (max 50 chars, truncated at word boundary).

Parse the JSON output for `feature` and `feature_dir`.

**Telemetry**: Run: `polaris telemetry record fix --feature <slug> --phase start --agent claude`

Create `spec.md` in the feature directory with the bug details:

```markdown
# Fix: {title}

**Source**: Azure DevOps Work Item [AB#{id}]({org_url}/{project}/_workitems/edit/{id})
**Type**: {type}
**Severity**: {severity}
**Priority**: {priority}
**State**: {state}

## Description

{description from ADO}

## Reproduction Steps

{repro_steps from ADO, or "Not provided" if empty}

## Acceptance Criteria

{acceptance_criteria from ADO, or "Bug is resolved and verified" if empty}

## Scope

- Fix the reported bug
- Add or update tests to prevent regression
- No unrelated changes
```

Create a single-WP `tasks.md`:

```markdown
# Tasks: fix-{id}-{kebab-title}

## Work Packages

### WP01 - Fix Bug AB#{id}

Fix the reported bug, add regression tests, verify the fix.
```

Create the WP01 work package file at `tasks/WP01-{kebab-title}.md` with real YAML frontmatter (schema: `polaris-specs/*/tasks/README.md`), making WP01 gate-eligible:

```markdown
---
work_package_id: "WP01"
title: "Fix Bug AB#{id}"
phase: "Fix"
lane: "planned"
test_status: ""
history:
  - timestamp: "<ISO-8601 UTC timestamp>"
    lane: "planned"
    agent: "system"
    action: "WP created via /polaris.fix"
---

# WP01 - Fix Bug AB#{id}

Fix the reported bug, add regression tests, verify the fix.
```

Create `meta.json` using `{base_branch}` captured before the branch switch:

```json
{
  "feature_number": "<number>",
  "slug": "fix-{id}-{kebab-title}",
  "friendly_name": "Fix: {title}",
  "mission": "software-dev",
  "source_description": "Azure DevOps Bug AB#{id}: {title}",
  "created_at": "<ISO timestamp>",
  "target_branch": "{base_branch}",
  "vcs": "git",
  "ado_work_item": {
    "id": {id},
    "type": "{type}",
    "url": "{org_url}/{project}/_workitems/edit/{id}"
  }
}
```

Commit the feature scaffold now, so files `create-feature` leaves untracked (e.g. `tasks/.gitkeep`) do not later block the `planning_clean` guard:

```bash
git add polaris-specs/fix-{id}-{kebab-title}
git commit -m "chore: scaffold fix-{id}-{kebab-title} planning artifacts"
```

### Step 3.5: Update Work Item Status

Update the ADO work item to "Active" to signal investigation has started, using the
mark-active operation (Operation 2 in the loaded reference).

If the update fails, log a warning and continue.

### Step 3.7: Root Cause Analysis - WAIT FOR APPROVAL

Before writing any code, triage the bug and get explicit approval.

**If the work item has no description, no repro steps, and no acceptance criteria**, stop:
> "AB#{id} lacks enough detail to diagnose. Please provide repro steps, a description of the unexpected behaviour, or relevant logs."

End with `WAITING_FOR_FIX_INPUT` and wait.

Locate the fault with the code graph: `polaris graph symbol <name>` (definition), `who-imports <file>` (callers), `impact <file>` (dependents), `tests-for <file>` (tests to extend). JSON output; exit 2 = unavailable/disabled.

**Otherwise**, read the relevant code and present this diagnosis - do NOT change any files yet:

```
Root Cause Analysis - AB#{id}: {title}

Root cause:   {why the bug occurs}
Affected:     {files and functions}
Proposed fix: {what will change and how}
Tests:        {regression test(s) to add}
Risk:         Low / Medium / High - {one sentence}
```

> **Proceed? Reply `yes` to implement, or describe changes to the approach.**

End with `WAITING_FOR_FIX_INPUT` and wait. Do NOT proceed to Step 4 until the user confirms.

### Step 4: Implement Fix

Mark implementation as started:

```bash
polaris agent tasks move-task WP01 --to doing --feature fix-{id}-{kebab-title}
```

1. **Locate relevant code**: Already identified in Step 3.7
2. **Understand the bug**: Root cause already established in Step 3.7
3. **Implement the fix**: Make minimal, focused changes as described in the approved plan
4. **Write/update tests**: Add regression tests as described in the approved plan, naming the file so its path contains `wp01` (e.g. `test_wp01_<bug-slug>.py`) so Step 7's WP-scoped run finds it.

### Step 5: Run Tests

```bash
python .polaris/scripts/tasks/run_tests.py --project-root . --json
```

Parse JSON output. If tests fail:
- Read the `output` field for failure details
- Fix the failing tests or the code causing failures
- Re-run tests (max 3 retry attempts)

If tests pass, proceed.

### Step 6: Commit

```bash
git add <changed-files>
git commit -m "fix: {title} (AB#{id})"
```

The commit message references the ADO work item for traceability.

### Step 7: Post-Fix Regression

Check for cascading breakage and record WP01's test evidence in one call:

```bash
python .polaris/scripts/tasks/run_tests.py --project-root . --feature fix-{id}-{kebab-title} --wp WP01 --json
```

This writes a `WP01-<ts>.json` evidence artifact under `.polaris/test-evidence/fix-{id}-{kebab-title}/`. Parse `success`:

- `true`: the runner never touches WP frontmatter, so record the pass:
  ```bash
  polaris agent tasks set-test-status WP01 --status passed --feature fix-{id}-{kebab-title}
  ```
- `false`: fix the breakage (repeat Step 5) before completing.

### Step 8: Show Progress

Display the current fix progress:

```bash
polaris agent tasks status --feature fix-{id}-{kebab-title}
```

### Step 9: Move to Review

```bash
polaris agent tasks move-task WP01 --to testing --feature fix-{id}-{kebab-title}
polaris agent tasks move-task WP01 --to for_review --feature fix-{id}-{kebab-title}
```

### Step 10: Update Work Item - Fix Complete

Update the ADO work item with fix details, using the mark-resolved operation (Operation 3 in
the loaded reference). Skipped entirely if `AZURE_DEVOPS_PAT` is unset. If the update fails,
log a warning and continue.

### Step 11: Summary

Display a summary:

```
Bug Fix Complete: AB#{id} - {title}
  Branch:  fix/{id}-{kebab-title}
  Files:   {count} files changed
  Tests:   {passed}/{total} passed
  Commit:  {short-hash} fix: {title} (AB#{id})

Next steps:
  1. Push the branch: git push -u origin fix/{id}-{kebab-title}
  2. Create a PR: gh pr create --title "fix: {title} (AB#{id})"
  3. Or use /polaris.ship to review, accept, and merge
```

**Telemetry**: Run: `polaris telemetry record fix --feature <slug> --phase complete --agent claude`
