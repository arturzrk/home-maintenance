---
work_package_id: WP03
title: 'E2E: system menu suite'
lane: "for_review"
dependencies: ["WP02"]
subtasks: [T010, T011, T012, T013, T014, T015]
test_status: required
test_file: frontend/e2e/wp09-system-menu.spec.ts
domain: testing-specialist
---

# WP03 - E2E: system menu suite

## Objective

`frontend/e2e/wp09-system-menu.spec.ts` - one describe "WP09: System
menu", 6 tests locking in the feature. Runs in the CI e2e job with the
rest of the suite (39 total after this WP).

## Context

- Helpers: `uniqueUser`, `signInAs` (now lands on `/`),
  `createPropertyViaApi`.
- Locators: `#system-menu-trigger`, `#dashboard-properties-link`,
  role-based menu items. Verify every string against the WP01/WP02
  implementations before asserting - do not trust this prompt over code.
- Dev-stub identity: the sub passed to `signInAs` surfaces as the
  trigger's identity text (verify exact rendering in WP01 code).

## Subtasks

### T010 - WP09-1: landing on dashboard

`signInAs` (no deep link) -> URL is `/`; dashboard heading and
`#dashboard-properties-link` visible (SC-02).

### T011 - WP09-2: identity + navigation

Trigger shows the stub identity; open menu -> click "My properties" ->
URL `/properties`; menu closed after navigation (US1/US2).

### T012 - WP09-3: system info

Open menu -> system-info block shows a version string and the Connected
state (API healthy in the test stack) (US4).

### T013 - WP09-4: sign out + protection

Open menu -> "Sign out" -> land on `/signin`; then `page.goto`
`/properties` -> redirected back to `/signin` (US5/SC-03).

### T014 - WP09-5: deep link preserved

Create property via API; WITHOUT signing in first, `page.goto`
`/properties/{id}` -> redirected to signin with callbackUrl; complete
stub sign-in via the form (not the helper - the helper asserts `/`) ->
land back on `/properties/{id}` (FR-08).

### T015 - WP09-6: dashboard CTA

From the dashboard, click `#dashboard-properties-link` -> `/properties`
(US6).

## Test Strategy

- `uniqueUser()` per test; no shared state.
- T014 hand-rolls the sign-in steps (fill OwnerId, submit) because
  `signInAs` waits for the dashboard URL.

## Definition of Done

- [ ] `npx playwright test e2e/wp09-system-menu.spec.ts` -> 6/6
- [ ] Full suite passes locally (39) and in the CI e2e job
- [ ] No production code changes

## Risks

- Identity rendering may include decoration (e.g. email-style suffix) -
  assert with a substring/regex on the sub, not equality, unless code
  shows exact text.
- Escape/outside-click behavior is jest-covered in WP01; do not
  duplicate here.

## Run Command

```bash
polaris implement WP03 --base WP02
```

## Activity Log

- 2026-07-15T04:50:48Z – unknown – lane=for_review – Implemented on branch 009-system-menu-WP03; PR #101
