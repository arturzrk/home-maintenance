---
work_package_id: WP01
title: "E2E: Properties page test suite"
lane: "for_review"
dependencies: []
base_branch: main
subtasks: [T001, T002, T003, T004, T005, T006]
domain: testing-specialist
test_status: required
test_file: frontend/e2e/wp04-properties.spec.ts
---

# WP01 - E2E: Properties page test suite

## Objective

Create `frontend/e2e/wp04-properties.spec.ts` containing all 6 Playwright
tests for the Properties list page and Property detail page. All tests must
pass against the running local stack (Next.js :3000 + .NET API :5000 +
MongoDB).

## Context

- **Playwright config**: `frontend/playwright.config.ts` --- Chromium, `baseURL: http://localhost:3000`
- **Helpers** (all in `frontend/e2e/helpers/setup.ts`):
  - `uniqueUser()` → `{ sub: string; token: string }` --- unique per-test identity
  - `signInAs(page, sub)` --- fills dev-stub form, waits for `/properties` redirect
  - `createPropertyViaApi(token, name)` → `Promise<string>` --- returns property id
  - `todayIso()` → `string` --- today as "yyyy-MM-dd"
- **Auth**: `NEXTAUTH_DEV_STUB=true` in `.env.local` --- dev-stub provider active
- **Existing tests for reference**: `frontend/e2e/wp05-property-recurring-jobs.spec.ts`

## Subtasks

### T001 --- WP04-1: Empty state

```
Scenario: Fresh user sees the empty properties list
- Sign in as a unique dev user via signInAs()
- Assert: page.getByRole('heading', { name: 'My properties' }) is visible
- Assert: page.getByText('No properties yet.') is visible (partial match ok)
- Assert: page.getByPlaceholder('Property name') is visible
- Assert: page.getByRole('button', { name: 'Create' }) is visible
```

No property creation needed --- the user is fresh with an empty list.

### T002 --- WP04-2: Create property → appears in list

```
Scenario: User fills the form and the new property card appears
- Sign in as a unique dev user
- Fill page.getByPlaceholder('Property name') with "My House"
- Click page.getByRole('button', { name: 'Create' })
- Assert: page.getByRole('link', { name: 'My House' }) is visible
- Assert: page.getByPlaceholder('Property name') has value "" (input cleared)
```

Note: `CreatePropertyForm` clears the input via `input.value = ""` after success
(direct DOM mutation, not React state). Playwright's `toHaveValue('')` checks
the live DOM value.

### T003 --- WP04-3: Click card → property detail

```
Scenario: Clicking a property card navigates to the detail page
- Create property "My House" via createPropertyViaApi(token, "My House")
- Sign in as that user via signInAs(page, sub)
- Click page.getByRole('link', { name: 'My House' })
- Wait: page.waitForURL(/\/properties\/.+/)
- Assert: page.url() contains `/properties/${propertyId}`
- Assert: page.getByRole('button', { name: 'Edit property name' }) is visible
- Assert: page.getByRole('heading', { name: 'Jobs' }) is visible
- Assert: page.getByRole('heading', { name: 'Recurring jobs' }) is visible
```

The property name button has `aria-label="Edit property name"` (from
`InlineEditableText`). The "Jobs" and "Recurring jobs" sections use `<h2>`.

### T004 --- WP04-4: Inline rename

```
Scenario: User renames a property via the inline-edit heading
- Create property "Old Name" via createPropertyViaApi(token, "Old Name")
- Sign in and navigate to /properties/{propertyId}
- Click page.getByRole('button', { name: 'Edit property name' })
- Assert: input with aria-label "Edit property name" is visible
- Fill the input with "New Name" (clear first, then type)
- Press Enter: page.keyboard.press('Enter')
- Assert: page.getByRole('button', { name: 'Edit property name' }) has text "New Name"
- Assert: no error text visible
```

After Enter, `InlineEditableText` calls the server action and sets `editing=false`,
returning to the button display. The button's text content becomes the new name.
Use `expect(page.getByRole('button', { name: 'Edit property name' })).toContainText('New Name')`.

### T005 --- WP04-5: Jobs empty state

```
Scenario: Property with no jobs shows the empty-state message
- Create property via createPropertyViaApi(token, "My House")
- Sign in and navigate to /properties/{propertyId}
- Assert: page.getByText('No jobs yet. Create one above.') is visible
```

### T006 --- WP04-6: Unauthenticated redirect

```
Scenario: Visiting /properties without a session redirects to /signin
- Do NOT call signInAs --- use a fresh page with no session cookie
- page.goto('/properties')
- Assert: page.url() contains '/signin'
```

No `uniqueUser()` needed --- this test doesn't authenticate at all.

## Test File Structure

```typescript
import { test, expect } from "@playwright/test";
import { signInAs, createPropertyViaApi, uniqueUser } from "./helpers/setup";

test.describe("WP04: Properties page", () => {
  test("WP04-1: shows empty state for fresh user", async ({ page }) => { ... });
  test("WP04-2: create property → appears in list, input cleared", async ({ page }) => { ... });
  test("WP04-3: clicking card navigates to property detail", async ({ page }) => { ... });
  test("WP04-4: rename property via inline edit", async ({ page }) => { ... });
  test("WP04-5: property detail shows jobs empty state", async ({ page }) => { ... });
  test("WP04-6: unauthenticated visitor redirected to signin", async ({ page }) => { ... });
});
```

## Locator Reference

| Element | Locator |
|---------|---------|
| Page heading | `page.getByRole('heading', { name: 'My properties' })` |
| Empty state text | `page.getByText('No properties yet.')` |
| Name input | `page.getByPlaceholder('Property name')` |
| Create button | `page.getByRole('button', { name: 'Create' })` |
| Property card link | `page.getByRole('link', { name: '<property-name>' })` |
| Edit name button | `page.getByRole('button', { name: 'Edit property name' })` |
| Edit name input | `page.getByRole('textbox', { name: 'Edit property name' })` (when editing) |
| Jobs heading | `page.getByRole('heading', { name: 'Jobs' })` |
| Recurring jobs heading | `page.getByRole('heading', { name: 'Recurring jobs' })` |
| Jobs empty text | `page.getByText('No jobs yet. Create one above.')` |

## Test Strategy

- Each test uses `uniqueUser()` for complete backend isolation.
- Properties created via API (`createPropertyViaApi`) to avoid UI setup noise.
- WP04-1 and WP04-2 navigate to `/properties` after `signInAs` (redirect lands there).
- WP04-3--5 navigate directly to `/properties/${propertyId}`.
- WP04-6 uses a cold page with no auth.

## Definition of Done

- [ ] `npx playwright test e2e/wp04-properties.spec.ts` → 6/6 pass
- [ ] Each test is independent (no shared state, no ordering dependency)
- [ ] No new helpers added (uses only existing `setup.ts` exports)
- [ ] No production code changes
- [ ] PR reviewed and merged to main

## Risks

- **Input clear detection**: `CreatePropertyForm` clears the input via direct DOM
  mutation (`input.value = ""`). Playwright's `toHaveValue('')` reads the DOM
  value directly, so this should work. If flaky, use `toHaveAttribute('value', '')`.
- **Inline edit timing**: after pressing Enter, the server action runs async.
  Playwright's `expect(...).toContainText('New Name')` will auto-wait up to
  10 s (configured timeout). No explicit wait needed.

## Run Command

```bash
polaris implement WP01
```

## Activity Log

- 2026-07-04T11:00:06Z -- unknown -- lane=doing -- Moved to doing
- 2026-07-04T11:00:07Z -- unknown -- lane=testing -- Moved to testing
- 2026-07-04T11:00:09Z -- unknown -- lane=for_review -- 6/6 Playwright e2e tests pass
