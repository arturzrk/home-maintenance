# E2E Test Plan: 008-assets

## Overview

Automated E2E tests for 4 work packages.

## Test Files

| Work Package | Title | Test File |
|---|---|---|
| WP01 | Backend: Asset aggregate end-to-end | `WP01-backend-asset-aggregate-end-to-end.e2e.js` |
| WP02 | Backend: assetId on Jobs and JobDefinitions | `WP02-backend-assetid-on-jobs-and-jobdefinitions.e2e.js` |
| WP03 | Frontend: assets UI | `WP03-frontend-assets-ui.e2e.js` |
| WP04 | E2E: assets suite | `WP04-e2e-assets-suite.e2e.js` |

## Running Tests

```bash
# Run all E2E tests for this feature
polaris runtests --feature 008-assets

# Run with Playwright directly
npx playwright test tests/e2e/

# Run a specific work package test
npx playwright test tests/e2e/WP01-backend-asset-aggregate-end-to-end.e2e.js
```
