# E2E Test Plan: 006-e2e-step-mutations

## Overview

Automated E2E tests for 1 work packages.

## Test Files

| Work Package | Title | Test File |
|---|---|---|
| WP01 | E2E: Step mutations & job rename test suite | `WP01-e2e-step-mutations-job-rename-test-suite.e2e.js` |

## Running Tests

```bash
# Run all E2E tests for this feature
polaris runtests --feature 006-e2e-step-mutations

# Run with Playwright directly
npx playwright test tests/e2e/

# Run a specific work package test
npx playwright test tests/e2e/WP01-e2e-step-mutations-job-rename-test-suite.e2e.js
```
