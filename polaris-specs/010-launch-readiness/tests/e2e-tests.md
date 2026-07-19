# E2E Test Plan: 010-launch-readiness

## Overview

Automated E2E tests for 3 work packages.

## Test Files

| Work Package | Title | Test File |
|---|---|---|
| WP01 | Public pages + branding | `WP01-public-pages-branding.e2e.js` |
| WP02 | E2E launch suite | `WP02-e2e-launch-suite.e2e.js` |
| WP03 | Go-live runbook + docs | `WP03-go-live-runbook-docs.e2e.js` |

## Running Tests

```bash
# Run all E2E tests for this feature
polaris runtests --feature 010-launch-readiness

# Run with Playwright directly
npx playwright test tests/e2e/

# Run a specific work package test
npx playwright test tests/e2e/WP01-public-pages-branding.e2e.js
```
