# E2E Test Plan: 009-system-menu

## Overview

Automated E2E tests for 3 work packages.

## Test Files

| Work Package | Title | Test File |
|---|---|---|
| WP01 | System menu + header rework | `WP01-system-menu-header-rework.e2e.js` |
| WP02 | Landing switch + dashboard page | `WP02-landing-switch-dashboard-page.e2e.js` |
| WP03 | E2E: system menu suite | `WP03-e2e-system-menu-suite.e2e.js` |

## Running Tests

```bash
# Run all E2E tests for this feature
polaris runtests --feature 009-system-menu

# Run with Playwright directly
npx playwright test tests/e2e/

# Run a specific work package test
npx playwright test tests/e2e/WP01-system-menu-header-rework.e2e.js
```
