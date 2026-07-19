---
work_package_id: WP02
title: E2E launch suite
lane: "doing"
dependencies: [WP01]
base_branch: 010-launch-readiness-WP01
base_commit: c9bff4bc034defce58f684ae791b2abb69d430a1
created_at: '2026-07-19T12:20:12.331430+00:00'
subtasks: [T008, T009, T010, T011, T012]
test_status: required
test_file: frontend/e2e/wp10-launch.spec.ts
domain: testing-specialist
shell_pid: "87481"
---

# WP02 - E2E launch suite

## Objective

`frontend/e2e/wp10-launch.spec.ts` - one describe "WP10: Launch pages",
4 tests locking the public-page behavior. Full suite green.

## Context

- Locators from WP01: `#landing-signin-cta`; landing h1 "Maintained
  House"; legal pages h1s. Verify against implemented code first.
- `signInAs` lands on `/` (dashboard branch) - unchanged.
- Anonymous `/` must NOT redirect (WP01 removed the middleware guard).

## Subtasks

### T008 - WP10-1: anonymous landing

`page.goto("/")` without a session: URL stays `/`, h1 "Maintained
House" visible, `#landing-signin-cta` visible, footer links to
/privacy, /terms and the user guide present.

### T009 - WP10-2: CTA to sign-in

Click `#landing-signin-cta` -> `/signin`, sign-in heading visible.

### T010 - WP10-3: legal pages public

`page.goto("/privacy")`: h1 visible, contact@maintained.house text
present. Same for `/terms` (h1 + "Poland" in governing-law section).
No redirect to /signin.

### T011 - WP10-4: signed-in / is the dashboard

`signInAs(page, sub)` -> "Welcome back" heading +
`#dashboard-properties-link` visible, no landing CTA present
(`#landing-signin-cta` count 0).

### T012 - Regression

Full local Playwright run (43 = 39 + 4) and CI e2e job green.

## Definition of Done

- [ ] `npx playwright test e2e/wp10-launch.spec.ts` -> 4/4
- [ ] Full suite passes locally and in CI
- [ ] No production code changes

## Risks

- Header brand link also says "Maintained House" after WP01 - scope
  landing assertions to `main` content or the h1 role to avoid strict
  mode collisions with the header.

## Run Command

```bash
polaris implement WP02 --base WP01
```
