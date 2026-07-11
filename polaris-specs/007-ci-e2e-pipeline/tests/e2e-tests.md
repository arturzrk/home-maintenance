# E2E Test Plan: 007-ci-e2e-pipeline

## Overview

Automated E2E tests for 1 work packages.

## Test Files

| Work Package | Title | Test File |
|---|---|---|
| WP01 | CI e2e job in ci.yml | `WP01-ci-e2e-job-in-ciyml.e2e.js` |

## Running Tests

```bash
# Run all E2E tests for this feature
polaris runtests --feature 007-ci-e2e-pipeline

# Run with Playwright directly
npx playwright test tests/e2e/

# Run a specific work package test
npx playwright test tests/e2e/WP01-ci-e2e-job-in-ciyml.e2e.js
```
