# E2E Test Plan: 002-recurring-jobs

## Overview

Automated E2E tests for 7 work packages.

## Test Files

| Work Package | Title | Test File |
|---|---|---|
| WP01 | Domain: ScheduleDefinition + JobDefinition + Job extension | `WP01-domain-scheduledefinition-jobdefinition-job-extens.e2e.js` |
| WP02 | Application: interfaces, use cases, generation logic | `WP02-application-interfaces-use-cases-generation-logic.e2e.js` |
| WP03 | Infrastructure: repositories + BackgroundService | `WP03-infrastructure-repositories-backgroundservice.e2e.js` |
| WP04 | API: endpoints + JobDto update | `WP04-api-endpoints-jobdto-update.e2e.js` |
| WP05 | Frontend: JobDefinition create/list on Property page | `WP05-frontend-jobdefinition-createlist-on-property-page.e2e.js` |
| WP06 | Frontend: JobDefinition detail page + Generate Next | `WP06-frontend-jobdefinition-detail-page-generate-next.e2e.js` |
| WP07 | Hardening: acceptance + cross-owner + perf tests | `WP07-hardening-acceptance-cross-owner-perf-tests.e2e.js` |

## Running Tests

```bash
# Run all E2E tests for this feature
polaris runtests --feature 002-recurring-jobs

# Run with Playwright directly
npx playwright test tests/e2e/

# Run a specific work package test
npx playwright test tests/e2e/WP01-domain-scheduledefinition-jobdefinition-job-extens.e2e.js
```
