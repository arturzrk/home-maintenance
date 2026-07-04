---
feature: 004-e2e-job-definition-detail
title: "E2E: JobDefinition detail page -- Implementation Plan"
created_at: "2026-06-10"
---

# Implementation Plan: E2E -- JobDefinition detail page

**Branch**: `004-e2e-job-definition-detail-WP01` | **Spec**: [spec.md](spec.md)

## Summary

Add `createJobDefinitionViaApi` to the shared helpers, then write 6
Playwright tests for the JobDefinition detail page. No production code changes.

## Technical Context

**Language/Version**: TypeScript 5.8+
**Primary Dependencies**: `@playwright/test` 1.60+ (installed)
**Testing**: Playwright / Chromium, baseURL http://localhost:3000
**Scale/Scope**: 1 helper addition + 1 new test file, 6 tests

## Constitution Check

No violations. Additive only.

## Project Structure

```
frontend/
└── e2e/
    ├── helpers/
    │   └── setup.ts                          <- add createJobDefinitionViaApi
    └── wp06-job-definition-detail.spec.ts    <- NEW
```

## Work Packages

### WP01 -- Helper + 6 e2e tests

New helper (add to e2e/helpers/setup.ts):
  createJobDefinitionViaApi(token, propertyId, body) => Promise<string>
  POST /api/job-definitions, returns definition id
  body: { name, schedule: { unit, multiplier, startDate }, stepTemplates[] }

Tests:

| ID | Name | Key assertions |
|----|------|----------------|
| WP06-1 | Detail page shows definition | name heading button, schedule label, step description visible |
| WP06-2 | Inline name rename | click "Edit definition name", fill, Enter, heading shows new name |
| WP06-3 | Add step template | fill placeholder "Add a step template", click "Add", step visible |
| WP06-4 | Remove step template | click Remove button, step no longer visible |
| WP06-5 | Generate next navigates to job | click "Generate next", URL matches /jobs/.+ |
| WP06-6 | Duplicate generate-next shows error | generate-next twice, "already scheduled" error appears |

Key locators:
- Name heading: page.getByRole('button', { name: 'Edit definition name' })
- Name input: page.getByRole('textbox', { name: 'Edit definition name' })
- Add step input: page.getByPlaceholder('Add a step template')
- Add step button: page.getByRole('button', { name: 'Add' })
- Remove step: page.getByRole('button', { name: /Remove step template/ })
- Generate next: page.getByRole('button', { name: 'Generate next' })
- Duplicate error: page.getByText('The next occurrence is already scheduled.')

WP06-6 approach: create definition with startDate far in the future so
inline generation produces no jobs, then click Generate next once
(creates first occurrence), then click again (duplicate error).

## Definition of Done

- [ ] npx playwright test e2e/wp06-job-definition-detail.spec.ts -> 6/6 pass
- [ ] Helper added to e2e/helpers/setup.ts
- [ ] Each test isolated (unique user + property + definition)
- [ ] No production code changes
- [ ] PR merged to main
