# E2E Test Plan: 001-property-job-step

## Overview

Automated E2E tests for 8 work packages.

## Test Files

| Work Package | Title | Test File |
|---|---|---|
| WP01 | WP01-authentication-foundation | `WP01-wp01-authentication-foundation.e2e.js` |
| WP02 | WP02-cross-cutting-infrastructure | `WP02-wp02-cross-cutting-infrastructure.e2e.js` |
| WP03 | WP03-property-aggregate-backend | `WP03-wp03-property-aggregate-backend.e2e.js` |
| WP04 | WP04-property-frontend | `WP04-wp04-property-frontend.e2e.js` |
| WP05 | WP05-job-aggregate-backend | `WP05-wp05-job-aggregate-backend.e2e.js` |
| WP06 | WP06-job-frontend | `WP06-wp06-job-frontend.e2e.js` |
| WP07 | WP07-step-mutation-and-rename | `WP07-wp07-step-mutation-and-rename.e2e.js` |
| WP08 | WP08-hardening-and-acceptance | `WP08-wp08-hardening-and-acceptance.e2e.js` |

## Running Tests

```bash
# Run all E2E tests for this feature
polaris runtests --feature 001-property-job-step

# Run with Playwright directly
npx playwright test tests/e2e/

# Run a specific work package test
npx playwright test tests/e2e/WP01-wp01-authentication-foundation.e2e.js
```
