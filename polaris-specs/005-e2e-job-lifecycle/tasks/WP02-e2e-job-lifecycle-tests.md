---
work_package_id: WP02
title: 'E2E: Job lifecycle test suite'
lane: planned
dependencies: ["WP01"]
base_branch: main
subtasks: [T004, T005, T006, T007, T008, T009, T010]
test_status: required
test_file: frontend/e2e/wp06-job-lifecycle.spec.ts
domain: testing-specialist
---

# WP02 - E2E: Job lifecycle test suite

## Objective

Add a `createJobViaApi` helper to `frontend/e2e/helpers/setup.ts`, then
create `frontend/e2e/wp06-job-lifecycle.spec.ts` with the 6 tests from
GitHub issue #47. All tests must pass against the running local stack.

**Requires WP01 merged** (WP06-4 asserts the completed read-only state and
WP06-5 asserts the "Active" badge -- both depend on string status).

## Context

- **Helpers** (`frontend/e2e/helpers/setup.ts`): `uniqueUser()`,
  `signInAs(page, sub)`, `createPropertyViaApi(token, name)`, `todayIso()`,
  `createJobDefinitionViaApi(...)`.
- **Reference suites**: `wp04-properties.spec.ts`,
  `wp06-job-definition-detail.spec.ts`.
- **Pages**: property detail (`/properties/{id}`) hosts `CreateJobForm` and
  `JobCard` list; job detail (`/jobs/{id}`) hosts `JobHeader`,
  `JobChecklist` (with `StepRow`s), `CompleteJobButton`, and a
  "Back to property" link.

## Subtasks

### T004 --- `createJobViaApi` helper

```typescript
/** Create a job directly via the backend API and return its id. */
export async function createJobViaApi(
  token: string,
  propertyId: string,
  name: string,
  steps: string[] = [],
): Promise<string> {
  const resp = await fetch(`${API_BASE}/api/jobs`, {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
      Authorization: `Bearer ${token}`,
    },
    body: JSON.stringify({
      propertyId,
      name,
      steps: steps.map((description) => ({ description })),
    }),
  });
  if (!resp.ok) throw new Error(`createJob failed: ${resp.status}`);
  const data = (await resp.json()) as { id: string };
  return data.id;
}
```

`CreateJobRequest.Steps` is `[Required]` -- always send an array, even when
empty. Returns 201 Created with the `JobDetailDto` body.

### T005 --- WP06-1: Create job (no steps) navigates to detail

```
- uniqueUser(); createPropertyViaApi(token, "My House"); signInAs
- goto /properties/{propertyId}
- Fill page.locator('#job-name') with "Paint fence"
- Click page.getByRole('button', { name: 'Create job' })
- page.waitForURL(/\/jobs\/.+/)
- Assert: page.getByRole('button', { name: 'Edit job name' }) contains "Paint fence"
- Assert: page.getByText('No steps on this job.') visible
```

Use `#job-name` -- `getByLabel('Name')` is ambiguous on the property page
(job form + definition form both have Name labels).

### T006 --- WP06-2: Create job with steps

```
- Same setup; goto /properties/{propertyId}
- Fill #job-name with "Service boiler"
- Fill page.locator('#job-steps') textarea with "Shut off gas\nDrain system\nReplace filter"
- Click "Create job"; waitForURL(/\/jobs\/.+/)
- Assert: page.getByRole('checkbox') has count 3, none checked
- Assert: page.getByRole('button', { name: 'Complete job' }) is disabled
```

Checkboxes have `aria-label` `Toggle "<description>"`; for the count assert
`page.getByRole('checkbox')` suffices (only step checkboxes exist on the page).

### T007 --- WP06-3: Tick a step

```
- Seed: createJobViaApi(token, propertyId, "Service boiler", ["Shut off gas", "Drain system"])
- signInAs; goto /jobs/{jobId}
- Check page.getByRole('checkbox', { name: 'Toggle "Shut off gas"' })
- Assert: the "Shut off gas" row span gains line-through:
  page.locator('li', { hasText: 'Shut off gas' }).locator('span.line-through') visible
- Assert: page.getByRole('checkbox', { name: 'Toggle "Drain system"' }) not checked
- Assert: Complete job button disabled
```

`StepRow` toggles the wrapper span class optimistically -- the assertion
also covers FR-05 (no reload needed).

### T008 --- WP06-4: Tick all + complete -> read-only

```
- Seed: createJobViaApi(token, propertyId, "Paint fence", ["Buy paint"])
- signInAs; goto /jobs/{jobId}
- Check the "Buy paint" checkbox; expect Complete job to become enabled
- Click page.getByRole('button', { name: 'Complete job' })
- Assert: page.getByText(/Completed on/) visible
- Assert: page.getByPlaceholder('Add a step') hidden (form unmounts when locked)
- Assert: page.getByRole('button', { name: 'Remove step "Buy paint"' }) disabled
```

`CompleteJobButton` replaces itself with the "Completed on ..." paragraph;
`JobChecklist` hides the add form; `StepRow` disables (not removes) the
Remove/reorder buttons and the checkbox.

### T009 --- WP06-5: Job card shows step count + Active badge

```
- Seed: createJobViaApi(token, propertyId, "Service boiler", ["A", "B"])
- signInAs; goto /properties/{propertyId}
- card = page.getByRole('link', { name: /Service boiler/ })
- Assert: card contains text "0 of 2 steps"
- Assert: card contains text "Active" (status badge -- requires WP01)
```

The card is a single `<a>` whose accessible name includes all inner text;
use a substring name match and `toContainText` for the details.

### T010 --- WP06-6: Back to property link

```
- Seed: createJobViaApi(token, propertyId, "Paint fence")
- signInAs; goto /jobs/{jobId}
- Click page.getByRole('link', { name: 'Back to property' })
- page.waitForURL(new RegExp(`/properties/${propertyId}`))
- Assert: URL contains /properties/{propertyId}
```

## Locator Reference

| Element | Locator |
|---------|---------|
| Job name input (create form) | `page.locator('#job-name')` |
| Steps textarea (create form) | `page.locator('#job-steps')` |
| Create job button | `page.getByRole('button', { name: 'Create job' })` |
| Job name heading (detail) | `page.getByRole('button', { name: 'Edit job name' })` |
| Empty checklist | `page.getByText('No steps on this job.')` |
| Step checkbox | `page.getByRole('checkbox', { name: 'Toggle "<desc>"' })` |
| Struck-through step | `page.locator('li', { hasText: '<desc>' }).locator('span.line-through')` |
| Complete job button | `page.getByRole('button', { name: 'Complete job' })` |
| Completed banner | `page.getByText(/Completed on/)` |
| Add-step input (detail) | `page.getByPlaceholder('Add a step')` |
| Remove step button | `page.getByRole('button', { name: 'Remove step "<desc>"' })` |
| Job card | `page.getByRole('link', { name: /<job name>/ })` |
| Back link | `page.getByRole('link', { name: 'Back to property' })` |

## Test Strategy

- `uniqueUser()` + fresh property per test; jobs seeded via API except
  WP06-1/WP06-2, which exercise the create form itself.
- Auto-waiting assertions only; no sleeps. The optimistic strikethrough in
  WP06-3 appears synchronously; the server confirm happens in the background.

## Definition of Done

- [ ] `npx playwright test e2e/wp06-job-lifecycle.spec.ts` -> 6/6 pass
- [ ] Full Playwright suite passes (15 existing + 6 new)
- [ ] `createJobViaApi` added to `e2e/helpers/setup.ts`
- [ ] Each test isolated (unique user + property + job)
- [ ] No production code changes in this WP
- [ ] PR reviewed and merged to main

## Risks

- **Add-step placeholder collision**: the job checklist uses
  `Add a step`, the definition page uses `Add a step template` -- distinct
  pages, no conflict.
- **Checkbox count in WP06-2**: if future UI adds other checkboxes to the
  job page, switch to `aria-label`-scoped locators.
- **"Active" badge (WP06-5)**: fails if run before WP01 merges -- expected
  ordering, enforced via `--base WP01`.

## Run Command

```bash
polaris implement WP02 --base WP01
```
