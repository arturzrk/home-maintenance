# Tasks: 006-e2e-step-mutations

## Subtask List

| ID | WP | Description | Parallel |
|----|----|-------------|----------|
| T001 | WP01 | Add `createAndCompleteJobViaApi` helper to `e2e/helpers/setup.ts` | |
| T002 | WP01 | Write WP07-1: add a step -> row visible, input cleared | [P] |
| T003 | WP01 | Write WP07-2: remove a step -> only that step gone | [P] |
| T004 | WP01 | Write WP07-3: edit step description inline via Enter | [P] |
| T005 | WP01 | Write WP07-4: reorder with down button -> order swapped | [P] |
| T006 | WP01 | Write WP07-5: completed job read-only state | [P] |
| T007 | WP01 | Write WP07-6: rename job inline via Enter | [P] |

## Work Packages

### WP01 --- `frontend/e2e/wp07-step-mutations.spec.ts` (helper + 6 tests)

**Goal**: Add the `createAndCompleteJobViaApi` helper, then create the test
file with all 6 step-mutation/rename scenarios passing against the live
stack.

**Subtasks**: T001 first (helper used by WP07-5), then T002-T007
(one file, one pass).

**Dependencies**: none (features 003-005 delivered all shared helpers).

**Implementation sketch**:
- Helper: POST /api/jobs -> tick each step id -> POST /{id}/complete
- WP07-1/2/3/4/6 seed with createJobViaApi; WP07-5 with the new helper
- Locator table in plan.md (all verified against components)
- Reorder assertion: page.getByRole('listitem').first() contains "Second"

**DoD**: `npx playwright test` -> 27/27 (21 existing + 6 new)

## Parallelization

Single WP --- implement straight through.

## MVP Scope

All 6 tests are MVP. No phasing required.

## Next Command

```bash
polaris implement WP01
```
