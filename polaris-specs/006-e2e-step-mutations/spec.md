---
feature: 006-e2e-step-mutations
title: "E2E: Step mutations & job rename"
status: draft
created_at: "2026-07-06"
---

# E2E: Step mutations & job rename

## Objective

Add Playwright end-to-end coverage for the step mutation UI (add, remove,
edit, reorder) and the inline job rename on the job detail page. Six tests
in `frontend/e2e/wp07-step-mutations.spec.ts` (GitHub issue #48). A
`createAndCompleteJobViaApi` helper is added to `e2e/helpers/setup.ts`
(`createJobViaApi` already exists from feature 005).

## Actors

- **Homeowner** -- an authenticated user who owns the property and jobs.

## User Scenarios

### US1 -- Add a step to an active job
On a job with no steps, the homeowner types into the "Add a step" input and
clicks "Add". The new step row appears and the input clears.

### US2 -- Remove a step
On a job with 2 steps, the homeowner clicks Remove on the first step. That
step disappears; the second remains.

### US3 -- Edit a step description inline
The homeowner clicks a step's description, types a new one, presses Enter.
The row shows the new description.

### US4 -- Reorder steps
On a job with steps "First", "Second", the homeowner clicks the down button
on "First". The list now shows "Second" first and "First" second.

### US5 -- Completed job is read-only
On a completed job: the add-step form is not rendered, the step description
and job name render as plain text (no edit affordance), and the Remove
buttons, reorder buttons and checkboxes are disabled.

### US6 -- Rename job inline
On an active job named "Old Job Name", the homeowner clicks the heading,
types "New Job Name", presses Enter. The heading shows the new name.

## Functional Requirements

| ID | Requirement |
|----|-------------|
| FR-01 | Submitting the add-step form appends a new step row and clears the input. |
| FR-02 | Clicking Remove on a step removes only that step. |
| FR-03 | A step description can be edited inline; Enter saves and the row updates. |
| FR-04 | The up/down buttons swap a step with its neighbour; the new order renders. |
| FR-05 | On a completed job the add-step form is absent, inline-edit affordances are absent, and remove/reorder/checkbox controls are disabled. |
| FR-06 | The job name can be edited inline on an active job; Enter saves and the heading updates. |

## Success Criteria

| ID | Criterion |
|----|-----------|
| SC-01 | All 6 tests in `wp07-step-mutations.spec.ts` pass (`npx playwright test`). |
| SC-02 | Each test is fully isolated -- unique user + property + job per test. |
| SC-03 | `createAndCompleteJobViaApi` helper added to `e2e/helpers/setup.ts`. |

## Key Entities

- **JobDetailDto** -- `{ id, propertyId, name, dueDate, status, completedAt, steps[] }`
- **StepDto** -- `{ id, order, description, isCompleted, completedAt }`

## Assumptions

- Full local stack running (Next.js :3000, .NET API :5000, MongoDB).
- `NEXTAUTH_DEV_STUB=true` in `.env.local`.
- Issue #48 says Remove buttons should be "absent" on a completed job; the
  implemented (and intended) behavior keeps them rendered but disabled, with
  steps visible as a record (settled during PR #66 review). US5/FR-05
  follow the implemented behavior.
- Estimation skipped (consistent with features 003-005).

## Out of Scope

- Due-date inline edit (covered implicitly by component tests).
- Unticking steps / reopening completed jobs.
- CI pipeline integration.
