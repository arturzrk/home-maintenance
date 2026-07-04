# Tasks: 003-e2e-properties-page

## Subtask List

| ID | WP | Description | Parallel |
|----|----|-------------|----------|
| T001 | WP01 | Write WP04-1: empty state (heading, empty text, form visible) | [P] |
| T002 | WP01 | Write WP04-2: create property → card appears, input cleared | [P] |
| T003 | WP01 | Write WP04-3: click card → navigates to detail, sections visible | [P] |
| T004 | WP01 | Write WP04-4: inline rename → Enter saves, heading updates | [P] |
| T005 | WP01 | Write WP04-5: jobs empty state on fresh property | [P] |
| T006 | WP01 | Write WP04-6: unauthenticated redirect to /signin | [P] |

## Work Packages

### WP01 --- `frontend/e2e/wp04-properties.spec.ts` (6 tests)

**Goal**: Create the test file with all 6 property-page scenarios passing against the live stack.

**Subtasks**: T001--T006 (all in one file; write together then verify all pass)

**Dependencies**: none (PR #45 already merged --- helpers and Playwright config are on main)

**Parallel opportunities**: all 6 tests are logically independent; write them in one pass.

**Implementation sketch**:
- `uniqueUser()` + `createPropertyViaApi()` for per-test isolation
- Locate name input by `page.getByPlaceholder('Property name')` (no id)
- WP04-3: `page.waitForURL(/\/properties\/.+/)` after clicking card
- WP04-4: click button `aria-label="Edit property name"`, fill input, `keyboard.press('Enter')`, assert button text updated
- WP04-6: `page.goto('/properties')` with no prior `signInAs`

**DoD**: `npx playwright test e2e/wp04-properties.spec.ts` → 6/6 pass

## Parallelization

Single WP --- no parallelization needed. Implement straight through.

## MVP Scope

All 6 tests are MVP. No phasing required.

## Next Command

```bash
polaris implement WP01
```
