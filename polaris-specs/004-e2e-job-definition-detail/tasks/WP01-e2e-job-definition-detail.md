---
work_package_id: WP01
title: 'E2E: JobDefinition detail page test suite'
lane: "done"
dependencies: []
base_branch: main
base_commit: 161701eaef4b5fe1de8c9d47a0cb75ab4d919e52
created_at: '2026-07-04T12:19:20.753827+00:00'
subtasks: [T001, T002, T003, T004, T005, T006, T007]
test_status: required
test_file: frontend/e2e/wp06-job-definition-detail.spec.ts
domain: testing-specialist
shell_pid: "69665"
reviewed_by: "Artur Żurek"
review_status: "approved"
---

# WP01 - E2E: JobDefinition detail page test suite

## Objective

Add a `createJobDefinitionViaApi` helper to `frontend/e2e/helpers/setup.ts`,
then create `frontend/e2e/wp06-job-definition-detail.spec.ts` containing all
6 Playwright tests for the JobDefinition detail page
(`/job-definitions/{id}`). All tests must pass against the running local
stack (Next.js :3000 + .NET API :5000 + MongoDB).

## Context

- **Playwright config**: `frontend/playwright.config.ts` --- Chromium, `baseURL: http://localhost:3000`
- **Existing helpers** (all in `frontend/e2e/helpers/setup.ts`):
  - `uniqueUser()` -> `{ sub: string; token: string }` --- unique per-test identity
  - `signInAs(page, sub)` --- fills dev-stub form, waits for `/properties` redirect
  - `createPropertyViaApi(token, name)` -> `Promise<string>` --- returns property id
  - `todayIso()` -> `string` --- today as "yyyy-MM-dd"
- **Auth**: `NEXTAUTH_DEV_STUB=true` in `.env.local` --- dev-stub provider active
- **Existing tests for reference**: `frontend/e2e/wp05-property-recurring-jobs.spec.ts`, `frontend/e2e/wp04-properties.spec.ts`
- **Page composition** (`frontend/src/app/job-definitions/[id]/page.tsx`):
  `DefinitionHeader` (inline-editable name + schedule label), "Generated jobs"
  section, "Step templates" section (`StepTemplateList`), `GenerateNextButton`.

## Subtasks

### T001 --- `createJobDefinitionViaApi` helper

Add to `frontend/e2e/helpers/setup.ts`:

```typescript
export interface CreateJobDefinitionBody {
  name: string;
  schedule: {
    unit: "Day" | "Week" | "Month" | "Year";
    multiplier: number;
    startDate: string; // yyyy-MM-dd
    endDate?: string | null;
  };
  stepTemplates?: { description: string }[];
}

export async function createJobDefinitionViaApi(
  token: string,
  propertyId: string,
  body: CreateJobDefinitionBody,
): Promise<string> {
  const resp = await fetch(`${API_BASE}/api/job-definitions`, {
    method: "POST",
    headers: { "Content-Type": "application/json", Authorization: `Bearer ${token}` },
    body: JSON.stringify({ propertyId, ...body }),
  });
  if (!resp.ok) throw new Error(`createJobDefinition failed: ${resp.status}`);
  const data = (await resp.json()) as { id: string };
  return data.id;
}
```

The API endpoint is `POST /api/job-definitions`
(`CreateJobDefinitionApiRequest`: `propertyId`, `name`, `schedule`,
`stepTemplates?`). Returns 201 Created with the full `JobDefinitionDto`
body including `id`.

### T002 --- WP06-1: Detail page shows definition

```
Scenario: Detail page renders name, schedule label, and step templates
- const { sub, token } = uniqueUser()
- propertyId = await createPropertyViaApi(token, "My House")
- defId = await createJobDefinitionViaApi(token, propertyId, {
    name: "Service boiler",
    schedule: { unit: "Month", multiplier: 1, startDate: todayIso() },
    stepTemplates: [{ description: "Check pressure" }],
  })
- signInAs(page, sub); page.goto(`/job-definitions/${defId}`)
- Assert: page.getByRole('button', { name: 'Edit definition name' }) contains "Service boiler"
- Assert: page.getByText(/Every month from/) is visible
- Assert: page.getByText('Check pressure') is visible
```

Schedule label format (from `scheduleLabel` in `DefinitionHeader.tsx`):
`Every month from <Mon YYYY>` when multiplier is 1, e.g.
"Every month from Jul 2026". Assert with the regex `/Every month from/` to
avoid locale-date brittleness.

Note: "Check pressure" also renders inside an `InlineEditableText` button
(`aria-label="Edit description for step 1"`). If `getByText` is ambiguous,
use `page.getByRole('button', { name: 'Edit description for step 1' })`
with `toContainText('Check pressure')`.

### T003 --- WP06-2: Inline name rename

```
Scenario: Rename definition via inline edit + Enter
- Create property + definition "Old Definition" (no step templates needed)
- signInAs + goto detail page
- Click page.getByRole('button', { name: 'Edit definition name' })
- Fill page.getByRole('textbox', { name: 'Edit definition name' }) with "New Definition"
- page.keyboard.press('Enter')
- Assert: page.getByRole('button', { name: 'Edit definition name' }) contains "New Definition"
```

`InlineEditableText` shares the same `aria-label` between the button
(viewing) and input (editing) --- same pattern already proven in WP04-4.

### T004 --- WP06-3: Add step template

```
Scenario: Add a step template via the form
- Create property + definition with stepTemplates: [] (empty)
- signInAs + goto detail page
- Assert: page.getByText('No step templates yet.') is visible
- Fill page.getByPlaceholder('Add a step template') with "Bleed radiators"
- Click page.getByRole('button', { name: 'Add', exact: true })
- Assert: page.getByText('Bleed radiators') is visible
- Assert: page.getByPlaceholder('Add a step template') has value "" (cleared)
```

The Add button is disabled while the input is empty; fill first, then click.
`StepTemplateList.handleAdd` clears `draftDescription` state on success, so
`toHaveValue('')` works.

### T005 --- WP06-4: Remove step template

```
Scenario: Remove an existing step template
- Create property + definition with stepTemplates: [{ description: "Old step" }]
- signInAs + goto detail page
- Assert: page.getByText('Old step') is visible
- Click page.getByRole('button', { name: 'Remove step template "Old step"' })
- Assert: page.getByText('Old step') is hidden (use expect(...).toBeHidden())
- Assert: page.getByText('No step templates yet.') is visible
```

The Remove button `aria-label` is `Remove step template "<description>"`
(quotes included in the label). Matching by regex
`/Remove step template/` is also fine since each test has one step.

### T006 --- WP06-5: Generate next navigates to job

```
Scenario: Generate next occurrence navigates to the new job page
- Create property + definition with startDate: todayIso(), unit "Month"
- signInAs + goto detail page
- Click page.getByRole('button', { name: 'Generate next' })
- page.waitForURL(/\/jobs\/.+/)
- Assert: page.url() matches /\/jobs\/.+/
```

On success `GenerateNextButton` calls `router.push('/jobs/{id}')`. With
`startDate = today` the backend generates the first occurrence inline at
create time, so Generate next produces the following occurrence --- still a
success, still navigates.

### T007 --- WP06-6: Exhausted schedule shows inline error (amended)

> Amended during implementation: the duplicate error
> (`next_occurrence_already_exists`) is unreachable via sequential clicks --
> the handler always computes the occurrence strictly after the latest
> generated job's due date, so it only fires under concurrent requests.
> The reachable inline-error path is an exhausted schedule.

```
Scenario: Second generate-next on a single-occurrence schedule shows inline error
- start = today + 200 days (beyond the 3-month inline-generation horizon)
- end = start + 5 days (schedule allows exactly one occurrence)
- Create property + definition with schedule { unit: Month, multiplier: 1, startDate: start, endDate: end }
- signInAs + goto detail page
- Click page.getByRole('button', { name: 'Generate next' })
- page.waitForURL(/\/jobs\/.+/)   // first click succeeds, navigates
- page.goto(`/job-definitions/${defId}`); assert Generate next visible
- Click page.getByRole('button', { name: 'Generate next' }) again
- Assert: page.getByText('The schedule has no future occurrences.') is visible
- Assert: page URL still matches /\/job-definitions\/.+/ (no navigation)
```

The error branch fires on result code `no_future_occurrence`;
`GenerateNextButton` renders the ApiError message (`result.error`).

## Test File Structure

```typescript
import { test, expect } from "@playwright/test";
import {
  signInAs,
  createPropertyViaApi,
  createJobDefinitionViaApi,
  uniqueUser,
  todayIso,
} from "./helpers/setup";

test.describe("WP06: JobDefinition detail page", () => {
  test("WP06-1: shows definition name, schedule label, step templates", async ({ page }) => { ... });
  test("WP06-2: rename definition via inline edit", async ({ page }) => { ... });
  test("WP06-3: add step template", async ({ page }) => { ... });
  test("WP06-4: remove step template", async ({ page }) => { ... });
  test("WP06-5: generate next navigates to new job", async ({ page }) => { ... });
  test("WP06-6: duplicate generate-next shows inline error", async ({ page }) => { ... });
});
```

## Locator Reference

| Element | Locator |
|---------|---------|
| Name heading (view) | `page.getByRole('button', { name: 'Edit definition name' })` |
| Name input (editing) | `page.getByRole('textbox', { name: 'Edit definition name' })` |
| Schedule label | `page.getByText(/Every month from/)` |
| Step templates heading | `page.getByRole('heading', { name: 'Step templates' })` |
| Steps empty state | `page.getByText('No step templates yet.')` |
| Add step input | `page.getByPlaceholder('Add a step template')` |
| Add step button | `page.getByRole('button', { name: 'Add', exact: true })` |
| Remove step button | `page.getByRole('button', { name: 'Remove step template "<desc>"' })` |
| Generate next button | `page.getByRole('button', { name: 'Generate next' })` |
| Inline error (exhausted schedule) | `page.getByText('The schedule has no future occurrences.')` |

## Test Strategy

- Each test uses `uniqueUser()` + fresh property + fresh definition for
  complete backend isolation.
- All setup goes through the API helpers (no UI-driven setup) so each test
  exercises only its own scenario.
- Auto-waiting assertions (`toBeVisible`, `toContainText`, `toHaveValue`)
  cover async server actions --- no explicit sleeps.

## Definition of Done

- [ ] `npx playwright test e2e/wp06-job-definition-detail.spec.ts` -> 6/6 pass
- [ ] `createJobDefinitionViaApi` added to `e2e/helpers/setup.ts`
- [ ] Each test is independent (no shared state, no ordering dependency)
- [ ] Existing suites still pass (`npx playwright test`)
- [ ] No production code changes
- [ ] PR reviewed and merged to main

## Risks

- **Ambiguous text locators**: step descriptions appear inside
  `InlineEditableText` buttons; if `getByText` matches multiple nodes, fall
  back to the role-based locators in the reference table.
- **WP06-6 navigation race**: the first Generate next click navigates away;
  use `waitForURL` before returning to the detail page. Prefer explicit
  `page.goto` over `goBack()` if flaky.
- **Schedule label locale**: month is formatted with `en-GB` short month;
  assert with `/Every month from/` regex, not the full date string.

## Run Command

```bash
polaris implement WP01
```

## Activity Log

- 2026-07-04T12:51:44Z – unknown – shell_pid=69665 – lane=testing – Playwright suite running
- 2026-07-04T12:51:46Z – unknown – shell_pid=69665 – lane=for_review – 15/15 e2e tests pass (6 new WP06 tests); WP06-6 amended to exhausted-schedule error; PR #58
- 2026-07-04T14:12:47Z – unknown – shell_pid=69665 – lane=done – PR #58 (implementation) and PR #59 (kanban/spec amendment) merged
