---
work_package_id: WP05
title: 'E2E: notification settings suite'
lane: "testing"
dependencies: ["WP04"]
subtasks: [T027, T028, T029, T030]
test_status: required
test_file: frontend/e2e/wp11-reminders.spec.ts
domain: testing-specialist
---

# WP05 - E2E: notification settings suite

## Objective

`frontend/e2e/wp11-reminders.spec.ts` locking the toggle flow end to
end. Full suite green.

## Context

- Email delivery itself is backend-only and not exercised by
  Playwright (no fake-mailbox infrastructure in this pass).
- Flow 3 (email click-through / deep-link sign-in) already has coverage
  from feature 009's `wp09-system-menu.spec.ts` (WP09-5) - this WP does
  not duplicate it, only relies on it as existing regression coverage.

## Subtasks

### T027 - WP11-1: menu link navigates to settings

`signInAs` -> open system menu -> "Notification settings" link visible
and navigates to `/settings/notifications`.

### T028 - WP11-2: toggle off persists

Toggle reminders off -> reload -> toggle still shows off.

### T029 - WP11-3: toggle back on persists

Toggle reminders on -> reload -> toggle still shows on.

### T030 - Regression

Full local Playwright run (existing suite + new) and CI e2e job green.

## Definition of Done

- [ ] `npx playwright test e2e/wp11-reminders.spec.ts` -> 3/3
- [ ] Full suite passes locally and in CI
- [ ] No production code changes

## Risks

- None beyond the usual strict-mode locator collisions - scope
  assertions to specific roles/containers if the menu link text
  collides with anything else on the page.

## Run Command

```bash
polaris implement WP05 --base WP04
```

## Activity Log

- 2026-07-27T09:35:29Z -- unknown -- lane=doing -- Backfilling missing doing-transition (implement command's earlier transition never landed on main).
- 2026-07-27T09:35:41Z -- unknown -- lane=testing -- Moved to testing
