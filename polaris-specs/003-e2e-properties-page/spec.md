---
feature: 003-e2e-properties-page
title: "E2E: Properties page"
status: draft
created_at: "2026-06-10"
github_issue: 46
---

# E2E: Properties page

## Objective

Add Playwright end-to-end coverage for the Properties list page and the
Property detail page. Six tests live in a single file
(`frontend/e2e/wp04-properties.spec.ts`) and use the existing dev-stub auth
and API helpers. The feature is complete when all 6 tests pass against a
running local stack.

## Actors

- **Homeowner** --- an authenticated user who manages properties.
- **Unauthenticated visitor** --- a browser session with no NextAuth cookie.

## User Scenarios

### US1 --- Empty properties list
A freshly signed-in user sees a "My properties" heading, an empty-state
message, and a visible create form.

### US2 --- Create a property and see it in the list
The user fills the property name input, submits the form, and the new property
appears as a clickable card. The input is cleared after a successful create.

### US3 --- Navigate from the list to a property detail page
Clicking a property card navigates to `/properties/{id}`. The detail page
shows the property name heading, the "Jobs" section, and the "Recurring jobs"
section.

### US4 --- Rename a property via inline edit
On the property detail page, clicking the property name opens an editable
input. Typing a new name and pressing Enter persists the rename; the heading
reflects the new name.

### US5 --- Jobs empty state on a new property
A property with no jobs shows "No jobs yet. Create one above." in the Jobs
section.

### US6 --- Unauthenticated redirect
Visiting `/properties` without a session redirects the browser to a URL
containing `/signin`.

## Functional Requirements

| ID | Requirement |
|----|-------------|
| FR-01 | `GET /properties` redirects unauthenticated visitors to `/signin`. |
| FR-02 | Authenticated users with no properties see "No properties yet." text on the list page. |
| FR-03 | Submitting the create form with a valid name adds a property card to the list and clears the input. |
| FR-04 | Clicking a property card navigates to `/properties/{id}`. |
| FR-05 | The property detail page renders the property name as an inline-editable heading. |
| FR-06 | Pressing Enter after editing the property name saves the change without an error. |
| FR-07 | A property with no jobs shows the "No jobs yet." empty-state message. |

## Success Criteria

| ID | Criterion |
|----|-----------|
| SC-01 | All 6 tests in `wp04-properties.spec.ts` pass (`npx playwright test`). |
| SC-02 | Each test is fully isolated --- a unique user + property per test, no shared server state. |
| SC-03 | Tests do not rely on test ordering or residual data from prior runs. |
| SC-04 | The test file uses only helpers from `e2e/helpers/setup.ts`; no new helper code required. |

## Key Entities

- **Property** --- `{ id: string; name: string }` (from `/api/properties`).
- **dev-stub session** --- `Authorization: Bearer dev-{sub}` accepted by the backend in development mode.

## Assumptions

- `NEXTAUTH_DEV_STUB=true` is set in `.env.local` (already confirmed).
- The full local stack (Next.js on :3000, backend API on :5000, MongoDB) is running when tests execute.
- The existing `signInAs`, `createPropertyViaApi`, and `uniqueUser` helpers in `e2e/helpers/setup.ts` are already merged before this feature is implemented (they ship with WP05/PR #45).
- The inline-edit component saves on Enter and on blur; tests use Enter to trigger save.

## Out of Scope

- Job creation, step management, and recurring-job definition forms (covered by WP05 tests).
- CI pipeline integration (can be added separately).
