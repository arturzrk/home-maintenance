# E2E Test Plan: 011-reminders

## Overview

Automated E2E tests for 5 work packages.

## Test Files

| Work Package | Title | Test File |
|---|---|---|
| WP01 | Owner profile + preferences API | `WP01-owner-profile-preferences-api.e2e.js` |
| WP02 | Email delivery | `WP02-email-delivery.e2e.js` |
| WP03 | Reminder digest scheduler | `WP03-reminder-digest-scheduler.e2e.js` |
| WP04 | Frontend settings + menu link | `WP04-frontend-settings-menu-link.e2e.js` |
| WP05 | E2E: notification settings suite | `WP05-e2e-notification-settings-suite.e2e.js` |

## Running Tests

```bash
# Run all E2E tests for this feature
polaris runtests --feature 011-reminders

# Run with Playwright directly
npx playwright test tests/e2e/

# Run a specific work package test
npx playwright test tests/e2e/WP01-owner-profile-preferences-api.e2e.js
```
