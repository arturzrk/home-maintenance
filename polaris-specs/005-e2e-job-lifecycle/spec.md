---
feature: 005-e2e-job-lifecycle
title: "E2E: Job lifecycle"
status: draft
created_at: "2026-07-04"
---

# E2E: Job lifecycle

## Objective

Add Playwright end-to-end coverage for the one-shot job lifecycle: creating
a job (with and without steps), ticking steps, completing the job, and the
job card summary on the property page. Six tests in
`frontend/e2e/wp06-job-lifecycle.spec.ts` (GitHub issue #47). A
`createJobViaApi` helper is added to `e2e/helpers/setup.ts`.

Discovered prerequisite: the API serializes the job `status` enum as a
number (`0`/`1`) while the frontend contract expects the strings
`"Active"`/`"Completed"`. As a result the job card badge renders "0" and a
completed job never shows its completed state or locks its checklist. The
API must serialize enums as strings before the lifecycle tests can pass.

## Actors

- **Homeowner** -- an authenticated user who owns the property and jobs.

## User Scenarios

### US0 -- Job status reads as text everywhere (prerequisite fix)
Anywhere a job's status is shown (card badge, job detail), the homeowner
sees "Active" or "Completed" -- never a numeric code. Completing a job
switches the detail page to its read-only completed state.

### US1 -- Create a job without steps
From the property page, the homeowner fills the job name "Paint fence" and
clicks "Create job". The browser navigates to the new job's detail page,
which shows the job name and "No steps on this job."

### US2 -- Create a job with steps
The homeowner fills the name "Service boiler" and enters three lines in the
steps textarea. After creation, the detail page lists 3 unchecked steps and
the "Complete job" button is disabled.

### US3 -- Tick a step
On a job with 2 steps, the homeowner ticks the first checkbox. The step's
description gains a strikethrough, the second step stays unchecked, and
"Complete job" remains disabled.

### US4 -- Complete a job
On a job with 1 step, the homeowner ticks the step and clicks
"Complete job". The page shows "Completed on ..." and the editing
affordances (add-step form, remove buttons) disappear.

### US5 -- Job card summarises steps and status
On the property page, a job with 2 open steps shows "0 of 2 steps" and an
"Active" badge on its card.

### US6 -- Back to property
From a job's detail page, clicking "Back to property" navigates to the
owning property's detail page.

## Functional Requirements

| ID | Requirement |
|----|-------------|
| FR-00 | The API returns job status as the text values "Active"/"Completed" (prerequisite fix). |
| FR-01 | Submitting the create-job form navigates to `/jobs/{newJobId}`. |
| FR-02 | A job created without steps shows "No steps on this job." |
| FR-03 | Steps entered one-per-line in the create form appear as unchecked checklist rows. |
| FR-04 | "Complete job" is disabled while any step is unticked. |
| FR-05 | Ticking a step applies a strikethrough to its description without a page reload. |
| FR-06 | Completing a job (all steps ticked) shows "Completed on ..." and removes editing affordances. |
| FR-07 | The property-page job card shows "{completed} of {total} steps" and the status badge text. |
| FR-08 | The "Back to property" link on the job detail page navigates to `/properties/{propertyId}`. |

## Success Criteria

| ID | Criterion |
|----|-----------|
| SC-01 | All 6 tests in `wp06-job-lifecycle.spec.ts` pass (`npx playwright test`). |
| SC-02 | Each test is fully isolated -- unique user + property + job per test. |
| SC-03 | `createJobViaApi` helper added to `e2e/helpers/setup.ts`. |
| SC-04 | All existing e2e suites and backend tests still pass after the serialization fix. |

## Key Entities

- **JobDetailDto** -- `{ id, propertyId, name, dueDate, status, completedAt, steps[], jobDefinitionId }`
- **StepDto** -- `{ id, order, description, isCompleted, completedAt }`
- **JobSummaryDto** -- adds `stepCount`, `completedStepCount` for cards.

## Assumptions

- Full local stack running (Next.js :3000, .NET API :5000, MongoDB).
- `NEXTAUTH_DEV_STUB=true` in `.env.local`.
- Serializing enums as strings API-wide affects only `JobStatus` in
  responses (schedule units are already plain strings in the DTOs); request
  deserialization keeps accepting existing payloads.
- Estimation skipped (consistent with features 003/004).

## Out of Scope

- Step add/remove/edit/reorder on the job detail page (issue #48).
- Job rename / due-date inline edit (issue #48).
- Unticking steps or reopening completed jobs.
- CI pipeline integration.
