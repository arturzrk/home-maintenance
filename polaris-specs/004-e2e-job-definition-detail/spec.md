---
feature: 004-e2e-job-definition-detail
title: "E2E: JobDefinition detail page"
status: draft
created_at: "2026-06-10"
---

# E2E: JobDefinition detail page

## Objective

Add Playwright end-to-end coverage for the JobDefinition detail page
(`/job-definitions/{id}`). Six tests in
`frontend/e2e/wp06-job-definition-detail.spec.ts` verify that a homeowner
can view, rename, and edit a recurring job definition and trigger the
generate-next flow. A `createJobDefinitionViaApi` helper is added to
`e2e/helpers/setup.ts`.

## Actors

- **Homeowner** -- an authenticated user who owns the job definition.

## User Scenarios

### US1 -- View definition detail
A homeowner navigating to a definition's detail page sees the definition
name, its schedule label, and any existing step templates.

### US2 -- Rename definition inline
The homeowner clicks the definition name, types a new name, and presses
Enter. The heading reflects the new name without a page reload.

### US3 -- Add a step template
The homeowner types a description into the step-template input and clicks
"Add". The new step appears in the list.

### US4 -- Remove a step template
The homeowner clicks the "Remove" button next to a step template. The step
disappears from the list.

### US5 -- Generate next occurrence
The homeowner clicks "Generate next". The browser navigates to the newly
created job's detail page at `/jobs/{id}`.

### US6 -- Exhausted schedule shows error on generate-next
When the schedule has no further occurrences (its end date has passed the
last one), clicking "Generate next" shows "The schedule has no future
occurrences." inline without navigating.

> Amended during implementation: the originally specified duplicate error
> ("The next occurrence is already scheduled.") is only reachable under
> concurrent requests -- sequential clicks always advance to a later
> occurrence, so it cannot be triggered deterministically end-to-end. The
> duplicate branch remains covered by the GenerateNextButton component test.

## Functional Requirements

| ID | Requirement |
|----|-------------|
| FR-01 | The detail page renders the definition name as an inline-editable heading. |
| FR-02 | The schedule label is displayed (e.g. "Every month from Jun 2026"). |
| FR-03 | Existing step templates are listed by description. |
| FR-04 | Pressing Enter after editing the name saves the change; the heading updates. |
| FR-05 | Submitting the add-step form appends a new step to the list. |
| FR-06 | Clicking Remove on a step removes it from the list. |
| FR-07 | Clicking "Generate next" navigates to `/jobs/{newJobId}` on success. |
| FR-08 | Clicking "Generate next" when the schedule is exhausted shows the error message inline without navigating. |

## Success Criteria

| ID | Criterion |
|----|-----------|
| SC-01 | All 6 tests in `wp06-job-definition-detail.spec.ts` pass (`npx playwright test`). |
| SC-02 | Each test is fully isolated -- unique user + definition per test. |
| SC-03 | `createJobDefinitionViaApi` helper added to `e2e/helpers/setup.ts`. |

## Key Entities

- **JobDefinitionDto** -- `{ id, propertyId, name, schedule, stepTemplates[] }`
- **ScheduleDefinitionDto** -- `{ unit, multiplier, startDate, endDate }`

## Assumptions

- Full local stack running (Next.js :3000, .NET API :5000, MongoDB).
- `NEXTAUTH_DEV_STUB=true` in `.env.local`.
- When a definition is created with `startDate = today`, the backend
  generates occurrences inline -- the first "generate next" call produces
  the occurrence after the last already-generated one.
- For US6, a definition whose end date allows exactly one occurrence,
  with a start date beyond the 3-month inline-generation horizon, makes
  the second generate-next click deterministically return the
  "no future occurrences" error. (The original duplicate-error assumption
  was disproved during implementation -- see US6.)

## Out of Scope

- Step reorder UI (up/down buttons).
- Schedule edit panel (collapsible form in DefinitionHeader).
- CI pipeline integration.
