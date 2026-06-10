---
feature: 003-e2e-properties-page
title: "E2E: Properties page --- Implementation Plan"
created_at: "2026-06-10"
---

# Implementation Plan: E2E --- Properties page

**Branch**: `003-e2e-properties-page-WP01` | **Date**: 2026-06-10 | **Spec**: [spec.md](spec.md)

## Summary

Write 6 Playwright e2e tests covering the Properties list and Property detail
pages. No production code changes --- one new test file only.

## Technical Context

**Language/Version**: TypeScript 5.8+
**Primary Dependencies**: `@playwright/test` 1.60+ (already installed in `frontend/` via PR #45)
**Storage**: N/A
**Testing**: Playwright / Chromium
**Target Platform**: Next.js 15 dev server on localhost:3000 + .NET backend on localhost:5000
**Performance Goals**: Each test completes in < 15s
**Constraints**: Requires full local stack running; dev-stub auth enabled
**Scale/Scope**: 1 file, 6 tests

## Constitution Check

No violations. Purely additive --- new test file, no production code touched.

## Project Structure

```
frontend/
└── e2e/
    ├── helpers/
    │   └── setup.ts          ← existing (no changes needed)
    └── wp04-properties.spec.ts  ← NEW (this feature)
```

## Work Packages

### WP01 --- Write `frontend/e2e/wp04-properties.spec.ts`

Single WP. All 6 tests in one file.

| Test ID | Name | Key assertions |
|---------|------|----------------|
| WP04-1 | Empty state | h1 "My properties", "No properties yet." text, placeholder input + "Create" button visible |
| WP04-2 | Create property → appears in list | "My House" card link visible after submit; input cleared |
| WP04-3 | Click card → property detail | URL matches `/properties/{id}`; property heading, "Jobs" section, "Recurring jobs" section visible |
| WP04-4 | Rename via inline edit | Click `aria-label="Edit property name"` button → input → type "New Name" → Enter → heading shows "New Name" |
| WP04-5 | Jobs empty state | "No jobs yet. Create one above." visible |
| WP04-6 | Unauthenticated redirect | Visit `/properties` without session → URL contains `/signin` |

**Implementation notes:**

- WP04-1/2: locate the name input by `page.getByPlaceholder('Property name')` (no id attribute on the input).
- WP04-3: after clicking the card, use `page.waitForURL(/\/properties\/.+/)` before asserting sections.
- WP04-4: the displayed button has `aria-label="Edit property name"`; once editing, the input shares that aria-label. After pressing Enter, assert the *button* text changed (not the input).
- WP04-6: fresh `page.goto('/properties')` without calling `signInAs` first.

**No new helpers.** All use `uniqueUser`, `signInAs`, `createPropertyViaApi` from `e2e/helpers/setup.ts`.

**Dependency**: PR #45 (`feat/playwright-e2e-wp05`) must be merged first so the Playwright setup and helpers are on main.

## Definition of Done

- [ ] `npx playwright test e2e/wp04-properties.spec.ts` → 6/6 pass
- [ ] Each test uses an isolated unique user
- [ ] No production code changes
- [ ] PR merged to main
