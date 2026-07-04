---
feature: 005-e2e-job-lifecycle
title: "E2E: Job lifecycle -- Implementation Plan"
created_at: "2026-07-04"
---

# Implementation Plan: E2E -- Job lifecycle

**Branch**: `005-e2e-job-lifecycle-WP##` | **Spec**: [spec.md](spec.md)

## Summary

Fix the JobStatus enum serialization (numbers -> strings), add
`createJobViaApi` to the shared helpers, then write 6 Playwright tests for
the job lifecycle.

## Technical Context

**Language/Version**: C# / .NET 9 (API fix), TypeScript 5.8+ (tests)
**Primary Dependencies**: `@playwright/test` 1.60+ (installed)
**Testing**: Playwright / Chromium, baseURL http://localhost:3000
**Scale/Scope**: 1 backend config line + 1 helper + 1 test file (6 tests)

## Constitution Check

No violations.

## Discovered Bug (prerequisite)

`POST /api/jobs` returns `"status": 0` (verified against the live API).
The frontend (`api-client.ts`) types `JobStatus = "Active" | "Completed"`
and compares strings in `JobCard`, `JobHeader`, `CompleteJobButton`, and
`jobs/[id]/page.tsx`. Numeric status means:

- Job card badge renders "0"/"1" and always uses the blue "active" style.
- Completed jobs never show "Completed on ..." nor lock the checklist.

Fix (backend, API-wide):

```csharp
// Program.cs
builder.Services.ConfigureHttpJsonOptions(o =>
    o.SerializerOptions.Converters.Add(
        new System.Text.Json.Serialization.JsonStringEnumConverter()));
```

`JobStatus` is the only enum in response DTOs (schedule `Unit` is already a
string in `ScheduleDefinitionDto`). Check backend integration tests for
numeric-status assertions and update them if any.

## Project Structure

```
backend/src/HomeMaintenance.API/Program.cs        <- enum serialization fix
frontend/e2e/helpers/setup.ts                     <- add createJobViaApi
frontend/e2e/wp06-job-lifecycle.spec.ts           <- NEW (6 tests)
```

## Work Packages

### WP01 -- Status serialization fix + helper + 6 e2e tests

New helper (add to e2e/helpers/setup.ts):

```typescript
export async function createJobViaApi(
  token: string,
  propertyId: string,
  name: string,
  steps: string[] = [],
): Promise<string>
// POST /api/jobs with { propertyId, name, steps: steps.map(d => ({ description: d })) }
// NOTE: `steps` is [Required] on CreateJobRequest -- always send an array.
// Returns 201 Created with the JobDetailDto body including `id`.
```

Tests (file `frontend/e2e/wp06-job-lifecycle.spec.ts`, describe
"WP06: Job lifecycle"):

| ID | Name | Key assertions |
|----|------|----------------|
| WP06-1 | Create job (no steps) navigates to detail | URL /jobs/{id}, name heading, "No steps on this job." |
| WP06-2 | Create job with steps | 3 unchecked checkboxes, "Complete job" disabled |
| WP06-3 | Tick a step | strikethrough on step 1, step 2 unchecked, button disabled |
| WP06-4 | Tick all + complete | "Completed on" visible, add form + Remove buttons gone |
| WP06-5 | Job card summary | "0 of 2 steps" and "Active" badge on property page |
| WP06-6 | Back to property link | URL /properties/{propertyId} |

Key locators (verified against components):

- Create form name input: `page.locator('#job-name')` (getByLabel('Name') is ambiguous)
- Create form steps textarea: `page.locator('#job-steps')`
- Create job button: `page.getByRole('button', { name: 'Create job' })`
- Job name heading: `page.getByRole('button', { name: 'Edit job name' })` (InlineEditableText in JobHeader)
- Empty checklist: `page.getByText('No steps on this job.')`
- Step checkbox: `page.getByRole('checkbox', { name: 'Toggle "<desc>"' })` (aria-label `Toggle "<description>"`)
- Step description span: strikethrough = parent span gains `line-through` class; assert via `expect(locator).toHaveClass(/line-through/)` on `span` wrapping the InlineEditableText, or `getByText('<desc>')` CSS check
- Complete button: `page.getByRole('button', { name: 'Complete job' })` -- `toBeDisabled()` while steps open
- Completed banner: `page.getByText(/Completed on/)`
- Add-step form input: `page.getByPlaceholder('Add a step')` -- hidden when job locked
- Remove step button: `page.getByRole('button', { name: 'Remove step "<desc>"' })`
- Job card: `page.getByRole('link', { name: '<job name>' })`; card text `0 of 2 steps`; badge `page.getByText('Active', { exact: true })` scoped inside the card link
- Back link: `page.getByRole('link', { name: 'Back to property' })`

Notes:

- WP06-3 strikethrough: `StepRow` wraps the description in a `span` whose
  class toggles `line-through` optimistically. Scope:
  `page.locator('li', { hasText: '<desc>' }).locator('span.line-through')`
  or assert the checkbox is checked + span has class.
- WP06-4: after Complete, `JobChecklist` hides the add form (`jobLocked`)
  and `StepRow` disables Remove -- issue says affordances "gone"; the add
  form unmounts (assert hidden) while Remove buttons render disabled --
  assert `page.getByPlaceholder('Add a step')` hidden and Remove button
  disabled (not absent).
- WP06-5 status badge depends on the serialization fix (FR-00).
- Property detail page URL: `/properties/{propertyId}`; job cards render in
  the "Jobs" section.

## Test Strategy

- `uniqueUser()` per test; property via `createPropertyViaApi`; jobs seeded
  via `createJobViaApi` except WP06-1/WP06-2 which exercise the create form.
- Backend fix verified by: existing backend test suite + the new e2e tests
  (WP06-4/WP06-5 fail without it).

## Definition of Done

- [ ] API returns `"status": "Active"` / `"Completed"` (curl check)
- [ ] Backend tests pass (`dotnet test`)
- [ ] npx playwright test -> all suites pass (21 tests: 15 existing + 6 new)
- [ ] Helper added to e2e/helpers/setup.ts
- [ ] Each test isolated (unique user + property + job per test)
- [ ] PR merged to main
