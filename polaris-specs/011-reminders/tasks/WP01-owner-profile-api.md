---
work_package_id: WP01
title: "Owner profile + preferences API"
lane: "done"
dependencies: []
subtasks: [T001, T002, T003, T004, T005, T006, T007]
test_status: required
test_file: backend/tests/HomeMaintenance.UnitTests/Domain/OwnerProfileTests.cs
domain: backend-logic
reviewed_by: "Artur Żurek"
review_status: "approved"
---

# WP01 - Owner profile + preferences API

## Objective

New minimal `OwnerProfile` (email + reminders-enabled flag), auto-captured
from the already-validated auth token with zero frontend involvement, plus
the query/command + endpoints the settings page (WP04) will call.

## Context

- `OwnerId` (`Domain/Identity/OwnerId.cs`) intentionally stores no user
  metadata - this is the first feature that needs to persist anything
  about an owner beyond their id. Keep `OwnerProfile` as small as the
  `Asset` aggregate: state + a couple of mutators, no ceremony.
- `HttpContextIdentityProvider` (`Infrastructure/Auth`) currently reads
  only the `sub`/`ClaimTypes.NameIdentifier` claim. The `email` claim is
  already present on the validated Google ID token but never read.
  Capture it there (or in comparable middleware run right after
  authentication) - no new frontend endpoint.
- Mirror the `Asset` aggregate's Mongo/repository pattern: document +
  repository + unique index registered in `MongoIndexInitializer`
  (collection `owner-profiles`, unique index on `ownerId`).
- Application handlers follow the existing `Result<T>` + handler shape
  used throughout (see any existing query/command pair for the pattern).
- Endpoint group follows the existing `MiniValidator`/`ToHttp`/
  `ToHttpCreated` conventions (see any existing `*Endpoints` class).

## Subtasks

### T001 - Domain: `OwnerProfile`

`Domain/Identity/OwnerProfile.cs` (or similar): `Owner` (OwnerId), `Email`
(string), `RemindersEnabled` (bool). `Create`/`Hydrate` factory methods,
`UpdateEmail(string)` (no-op if unchanged), `SetRemindersEnabled(bool)`.
Only validation: non-empty email.

### T002 - Repository + Mongo document + index

`IOwnerProfileRepository`: `GetAsync(OwnerId, ct)`,
`UpsertEmailAsync(OwnerId, string email, ct)` (no-op if unchanged),
`UpdateRemindersEnabledAsync(OwnerId, bool, ct)`. Mongo document +
repository implementation; unique index on `ownerId` registered in
`MongoIndexInitializer` alongside the existing Assets index.

### T003 - Auth-pipeline email capture

Read the `email` claim on the validated principal; call
`UpsertEmailAsync` when the stored profile is missing or the email
differs. No claim present -> no upsert (that owner simply never
receives digests later - this is correct, not an error).

### T004 - Application handlers

`GetNotificationPreferencesQuery` / `UpdateNotificationPreferencesCommand`
(toggle only), same `Result<T>` shape as existing handlers. If no
profile exists yet on read, return `RemindersEnabled: true` (spec
default) rather than a 404.

### T005 - `AccountEndpoints`

`GET /api/account/notification-preferences`,
`PATCH /api/account/notification-preferences` - same
`MiniValidator`/`ToHttp` conventions as every other endpoint group.

### T006 - Unit tests

`OwnerProfile` domain behavior (create/hydrate/update-email no-op/toggle);
handler tests against fakes (default-true-when-missing case included).

### T007 - Integration tests

`notification-preferences` endpoints (auth required, default value,
persists toggle) against Mongo Testcontainers; profile auto-capture
verified on an authenticated request.

## Definition of Done

- [ ] `OwnerProfile` domain + repository + Mongo index implemented
- [ ] Email captured automatically on authenticated requests, no frontend change required
- [ ] `GET`/`PATCH /api/account/notification-preferences` implemented and tested
- [ ] Unit + integration tests green
- [ ] No production code outside backend touched

## Risks

- Auth-pipeline capture adds a small amount of work per authenticated
  request (an upsert-if-changed check) - acceptable at this app's scale,
  not a blocker.

## Run Command

```bash
polaris implement WP01
```

## Activity Log

- 2026-07-26T10:01:49Z -- unknown -- lane=doing -- Moved to doing
- 2026-07-26T10:01:55Z -- unknown -- lane=testing -- dotnet test green: 180/180 Unit.Tests + 203/203 Integration.Tests (6 new notification-preferences integration tests, 12 new domain/handler unit tests)
- 2026-07-26T10:02:01Z -- unknown -- lane=for_review -- Owner profile + notification-preferences API implemented per plan: OwnerProfile domain entity, IOwnerProfileRepository + Mongo (unique index on ownerId), OwnerProfileSyncMiddleware (auth-pipeline email capture, no frontend change), GET/PATCH /api/account/notification-preferences, DevStub dev-<sub>:<email> extension for tests. dotnet test green: 180/180 Unit + 203/203 Integration. polaris runtests CLI bug (get_specs_dir undefined, same as WP02/WP03/WP04 in prior features) - ran dotnet test directly.
- 2026-07-26T11:36:26Z -- unknown -- lane=done -- Merged via PR #117 (code) - Copilot findings addressed. Kanban tracking merged via PR #116.
