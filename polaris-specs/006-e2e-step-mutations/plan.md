---
feature: 006-e2e-step-mutations
title: "E2E: Step mutations & job rename -- Implementation Plan"
created_at: "2026-07-06"
---

# Implementation Plan: E2E -- Step mutations & job rename

**Branch**: `006-e2e-step-mutations-WP01` | **Spec**: [spec.md](spec.md)

## Summary

Add `createAndCompleteJobViaApi` to the shared helpers, then write 6
Playwright tests for step mutations and job rename. Amended during
implementation: includes a one-line JobChecklist state-resync fix for a
bug the tests exposed.

## Technical Context

**Language/Version**: TypeScript 5.8+
**Primary Dependencies**: `@playwright/test` 1.60+ (installed)
**Testing**: Playwright / Chromium, baseURL http://localhost:3000
**Scale/Scope**: 1 helper addition + 1 new test file, 6 tests

## Constitution Check

No violations. Additive only.

## Project Structure

```
frontend/e2e/helpers/setup.ts              <- add createAndCompleteJobViaApi
frontend/e2e/wp07-step-mutations.spec.ts   <- NEW (6 tests)
```

## Work Packages

### WP01 -- Helper + 6 e2e tests

New helper (add to e2e/helpers/setup.ts):

```typescript
export async function createAndCompleteJobViaApi(
  token: string,
  propertyId: string,
  name: string,
  steps: string[],
): Promise<string>
// 1. POST /api/jobs (reuse createJobViaApi body shape) -> JobDetailDto with steps[]
// 2. for each step: POST /api/jobs/{jobId}/steps/{stepId}/tick
// 3. POST /api/jobs/{jobId}/complete
// Returns the job id.
```

Implementation note: the create response contains the step ids needed for
ticking, so the helper should do the POST itself (not call createJobViaApi,
which returns only the id) or refactor createJobViaApi to expose the full
body internally.

Tests (describe "WP07: Step mutations & job rename"):

| ID | Name | Key assertions |
|----|------|----------------|
| WP07-1 | Add a step | new row visible, input cleared |
| WP07-2 | Remove a step | removed step gone, other remains |
| WP07-3 | Edit step description inline | row shows new description |
| WP07-4 | Reorder with down button | first listitem now "Second" |
| WP07-5 | Completed job read-only | add form absent, edit affordances absent, controls disabled |
| WP07-6 | Rename job inline | heading shows new name |

Key locators (verified against components):

- Add-step input: page.getByPlaceholder('Add a step'); Add button: getByRole('button', { name: 'Add', exact: true })
- Remove: page.getByRole('button', { name: 'Remove step "<desc>"' })
- Step edit (view): page.getByRole('button', { name: 'Edit description for step 1' }); (editing): getByRole('textbox', same name)
- Reorder: page.getByRole('button', { name: 'Move "<desc>" down' }) / 'Move "<desc>" up'
- Order assertion: page.getByRole('listitem').first() toContainText('Second')
- Job rename (view): page.getByRole('button', { name: 'Edit job name' }); (editing): getByRole('textbox', same name)
- Completed job: disabled InlineEditableText renders a plain <span> -- assert
  getByRole('button', { name: 'Edit job name' }) has count 0 while
  getByText(name) is visible; add-step: getByPlaceholder('Add a step') hidden;
  Remove/checkbox: toBeDisabled()

WP07-3/WP07-6 inline-edit flow (proven in WP04-4 and 004 WP06-2): click the
view button, fill the textbox with the same aria-label, press Enter, assert
the view button text updated.

WP07-4: StepRow applies the move optimistically then refreshes from the
server; the first-listitem assertion auto-waits through both.

## Test Strategy

- uniqueUser() + fresh property per test; jobs seeded via createJobViaApi /
  createAndCompleteJobViaApi.
- WP07-5 uses the new helper; all others use existing helpers.

## Definition of Done

- [ ] npx playwright test e2e/wp07-step-mutations.spec.ts -> 6/6 pass
- [ ] Full Playwright suite passes (21 existing + 6 new = 27)
- [ ] Helper added to e2e/helpers/setup.ts
- [ ] Each test isolated (unique user + property + job)
- [ ] No production code changes beyond the JobChecklist resync fix
      (amendment: bug exposed by WP07-2/WP07-3, see spec assumptions)
- [ ] PR merged to main
