# E2E Test Plan: 005-e2e-job-lifecycle

## Overview

Automated E2E tests for 2 work packages.

## Test Files

| Work Package | Title | Test File |
|---|---|---|
| WP01 | JobStatus enum serialization fix | `WP01-jobstatus-enum-serialization-fix.e2e.js` |
| WP02 | E2E: Job lifecycle test suite | `WP02-e2e-job-lifecycle-test-suite.e2e.js` |

## Running Tests

```bash
# Run all E2E tests for this feature
polaris runtests --feature 005-e2e-job-lifecycle

# Run with Playwright directly
npx playwright test tests/e2e/

# Run a specific work package test
npx playwright test tests/e2e/WP01-jobstatus-enum-serialization-fix.e2e.js
```
