# Tasks: 004-e2e-job-definition-detail

## Subtask List

| ID | WP | Description | Parallel |
|----|----|-------------|----------|
| T001 | WP01 | Add `createJobDefinitionViaApi` helper to `e2e/helpers/setup.ts` | |
| T002 | WP01 | Write WP06-1: detail page shows name, schedule label, step templates | [P] |
| T003 | WP01 | Write WP06-2: inline name rename via Enter updates heading | [P] |
| T004 | WP01 | Write WP06-3: add step template via input + Add button | [P] |
| T005 | WP01 | Write WP06-4: remove step template via Remove button | [P] |
| T006 | WP01 | Write WP06-5: Generate next navigates to /jobs/{id} | [P] |
| T007 | WP01 | Write WP06-6: duplicate generate-next shows inline error | [P] |

## Work Packages

### WP01 --- `frontend/e2e/wp06-job-definition-detail.spec.ts` (helper + 6 tests)

**Goal**: Add the `createJobDefinitionViaApi` helper, then create the test
file with all 6 JobDefinition detail page scenarios passing against the
live stack (Next.js :3000 + .NET API :5000 + MongoDB).

**Subtasks**: T001 first (helper is used by every test), then T002--T007
(all in one file; write together then verify all pass).

**Dependencies**: none (Playwright config and existing helpers are on main).

**Parallel opportunities**: T002--T007 are logically independent tests;
write them in one pass after T001.

**Implementation sketch**:
- `uniqueUser()` + `createPropertyViaApi()` + new `createJobDefinitionViaApi()` for per-test isolation
- Name heading: `page.getByRole('button', { name: 'Edit definition name' })` (InlineEditableText)
- Schedule label text: `Every month from <Mon YYYY>` (from `scheduleLabel` in DefinitionHeader)
- Add step: `page.getByPlaceholder('Add a step template')` + `page.getByRole('button', { name: 'Add' })`
- Remove step: `page.getByRole('button', { name: 'Remove step template "<desc>"' })`
- WP06-5: `page.waitForURL(/\/jobs\/.+/)` after clicking Generate next
- WP06-6: definition with far-future `startDate` (no inline generation), click Generate next twice

**DoD**: `npx playwright test e2e/wp06-job-definition-detail.spec.ts` -> 6/6 pass

## Parallelization

Single WP --- no parallelization needed. Implement straight through.

## MVP Scope

All 6 tests are MVP. No phasing required.

## Next Command

```bash
polaris implement WP01
```
