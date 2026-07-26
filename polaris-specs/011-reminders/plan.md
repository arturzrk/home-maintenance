# Implementation Plan: 011 - Reminders (due/overdue job email digest)

**Branch**: per-WP branches (`011-reminders-WP##`) | **Date**: 2026-07-26 | **Spec**: [spec.md](spec.md)
**Tracker**: GitHub issue #112

## Summary

Introduce the smallest mechanism that makes the app proactive: a daily
background pass identifies every active job due within 3 days or
overdue, groups the results by owner then by property, and emails one
digest per affected owner via Resend. A new lightweight per-owner
"notification preference" record (email + on/off flag) is the only new
persisted concept; it is populated automatically from the identity
already present on every authenticated request, so no signup step or
frontend capture is needed. A minimal settings page + one new system-menu
link give owners the on/off toggle.

## Technical Context

**Verified in code before planning**

- `backend/src/HomeMaintenance.Infrastructure/Scheduling/JobGeneratorService.cs`
  is the exact background-service shape to mirror: `BackgroundService` +
  `IServiceScopeFactory` (dependencies are scoped, the service is a
  singleton), an initial startup pass, then a 24h `PeriodicTimer`, and a
  **public** `RunXAsync(ct)` method so tests can trigger a pass
  deterministically instead of waiting on the timer.
- `IJobDefinitionRepository.ListAllActiveAsync(ct)` (no `OwnerId` param)
  is precedent for an owner-less, system-wide query used only by a
  background process - `IJobRepository` gets an analogous
  `ListDueOrOverdueAsync(DateOnly onOrBefore, ct)`.
- `OwnerId` (`Domain/Identity/OwnerId.cs`) is deliberately bare: "no user
  metadata is stored - identity metadata lives with the provider."
  `HttpContextIdentityProvider` reads only the `sub`/`NameIdentifier`
  claim today; the `email` claim on the already-validated ID token is
  never read or persisted anywhere. Reminders is the first feature that
  needs to store anything about an owner beyond their id - a new,
  intentionally minimal record is justified (not a rich aggregate, just
  email + a toggle).
- appsettings pattern: `appsettings.json` (dev defaults) +
  `appsettings.Staging.json` (placeholders/comments) + env var overrides
  via `Section__Key`. `services.AddHostedService<X>()` is registered in
  `Infrastructure/DependencyInjection.cs`.
- Feature 009 already made `/jobs/:path*` a protected middleware route
  with `callbackUrl` deep-link preservation, verified end-to-end by
  `wp09-system-menu.spec.ts` (WP09-5). **Flow 3 (email click-through)
  needs zero new code** - it already works; only needs a regression
  mention, not new plumbing.
- `system-menu.tsx` (009) is the pattern for the new settings link:
  closes on click, same list-item styling.

## Architecture

### Notification preference (new, minimal)

- Domain: `OwnerProfile` (Owner, Email, RemindersEnabled), shaped like
  `Asset` - small aggregate, `Create`/`Hydrate` + `UpdateEmail(string)` +
  `SetRemindersEnabled(bool)`. No validation ceremony beyond non-empty
  email.
- `IOwnerProfileRepository`: `GetAsync(OwnerId, ct)`,
  `UpsertEmailAsync(OwnerId, string email, ct)` (no-op if unchanged),
  `UpdateRemindersEnabledAsync(OwnerId, bool, ct)`. Mongo collection
  `owner-profiles`, unique index on `ownerId` (registered in
  `MongoIndexInitializer`, following the Assets precedent).
- **Capture, not a new frontend call**: a small piece in
  `Infrastructure/Auth` (middleware run after authentication, alongside
  where `HttpContextIdentityProvider` resolves the principal) reads the
  `email` claim already present on the validated token and calls
  `UpsertEmailAsync` if the stored profile is missing or the email
  differs. This runs on authenticated requests; at this app's personal
  scale the extra upsert-if-changed check is negligible, and it means
  **zero frontend changes** are needed to get an owner's email
  captured. No claim -> no upsert -> that owner simply never receives
  digests (silently correct: no email means nothing to send to).
- New Application handlers: `GetNotificationPreferencesQuery` /
  `UpdateNotificationPreferencesCommand` (toggle only), same
  `Result<T>` + handler shape as everything else. If no profile exists
  yet when read (shouldn't happen post-capture, but defensively),
  return `RemindersEnabled: true` (the spec's default) rather than 404.
- New endpoint group `AccountEndpoints`:
  `GET /api/account/notification-preferences`,
  `PATCH /api/account/notification-preferences` - same
  `MiniValidator`/`ToHttp` conventions as every other endpoint group.

### Email delivery (new port + two implementations)

- `IEmailSender` in `Application.Common.Interfaces`:
  `Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken ct = default)`.
- `ResendEmailSender` (Infrastructure): `HttpClient` POST to Resend's
  email API with `Authorization: Bearer {ApiKey}` and a configured
  `From` address. Options bound from `Email:Resend:ApiKey`,
  `Email:FromAddress`.
- `LoggingEmailSender` (Infrastructure): logs the recipient/subject/body
  instead of sending. Selected whenever `Email:Provider` is `Log` (the
  Development/CI default) so **no Resend account or key is ever
  required to build, run, or test locally or in CI** - mirrors the
  `Auth:UseStub` dev-only pattern already established.
- Startup assertion mirroring `Auth:UseStub`'s: if `Email:Provider` is
  `Resend` and `Email:Resend:ApiKey` is missing, fail fast at startup
  rather than silently dropping mail.

### Digest computation + send

- `ReminderDigestService : BackgroundService`, structurally identical
  to `JobGeneratorService`: startup pass, 24h `PeriodicTimer`, public
  `RunDigestPassAsync(ct)` for deterministic tests.
- Per pass: `today = dateTimeProvider.UtcToday`;
  `horizon = today.AddDays(3)`;
  `dueJobs = await jobRepository.ListDueOrOverdueAsync(horizon, ct)`
  (Active status, `DueDate != null && DueDate <= horizon`, system-wide).
- Group `dueJobs` by `Owner`. For each group: look up the
  `OwnerProfile`; skip (log, continue) if missing, no email, or
  `RemindersEnabled == false`. Otherwise group that owner's jobs by
  `PropertyId`, resolve each property's name via
  `IPropertyRepository.GetAsync(propertyId, owner, ct)` (owner already
  known and correct - no ownership check needed beyond what already
  exists; N+1 lookups are fine at this app's scale, no batch query
  needed).
- Build subject + HTML body: grouped by property, each job a link to
  `{Frontend:BaseUrl}/jobs/{jobId}`; footer links to
  `{Frontend:BaseUrl}/settings/notifications` (FR-05/US5).
- `await emailSender.SendAsync(...)` per owner inside a try/catch that
  logs and continues on failure (FR-09), mirroring
  `JobGeneratorService`'s per-definition try/catch exactly.
- New config: `Frontend:BaseUrl` (dev `http://localhost:3000`, staging
  `https://staging.maintained.house`, prod `https://maintained.house`) -
  same shape as `Cors:AllowedOrigins`'s existing per-environment value.
- No audit-log entries for the scheduler pass itself (structured
  logging only), consistent with `JobGeneratorService`, which also only
  logs rather than emitting audit events for its own scheduled runs.

### Frontend

- `frontend/src/lib/api-client.ts`: `notificationPreferences.get/update`.
- `frontend/src/app/settings/notifications/actions.ts`: server actions
  following the `ActionResult` convention used throughout.
- `frontend/src/app/settings/notifications/page.tsx` (server component,
  `requireSession`) + a small client toggle component mirroring
  `AssetHeader`'s obsolete-toggle pattern (button, pending state, inline
  error, `router.refresh()`).
- `system-menu.tsx`: one new link, "Notification settings", closes the
  menu on click like every other item.
- `middleware.ts`: add `/settings/:path*` to the protected matcher
  (same pattern as `/assets`, `/job-definitions` added in feature 009).

### Tests

- **Backend unit**: `OwnerProfile` domain behavior; digest qualification
  rules against fakes (due-in-3-days included, due-in-4-days excluded,
  overdue included, completed excluded, `RemindersEnabled=false`
  skipped, no stored email skipped); `ResendEmailSender` request-shape
  test against a fake `HttpMessageHandler`; startup-assertion test for
  a missing Resend key.
- **Backend integration**: `notification-preferences` endpoints
  (auth/ownership, default value); `ListDueOrOverdueAsync` against
  Mongo Testcontainers; profile auto-capture on an authenticated
  request.
- **Frontend jest**: notification-settings toggle component.
- **E2e**: sign-in -> system menu -> "Notification settings" link ->
  toggle off -> reload shows persisted state -> toggle on. Email
  delivery itself is backend-only and not exercised by Playwright (no
  fake-mailbox infrastructure in this pass) - the existing 009
  deep-link e2e test already covers flow 3's mechanics, so WP e2e work
  here does not duplicate it, only references it as regression.

## Constitution Check

- Clean Architecture layering preserved: `OwnerProfile` is a Domain
  entity, `IOwnerProfileRepository`/`IEmailSender` are Application
  ports, Mongo/Resend/Logging implementations live in Infrastructure.
- "Start minimal, grow intentionally": `OwnerProfile` is the smallest
  record that unblocks the feature (email + one flag), not a full user
  profile; email capture is folded into the existing auth pipeline
  rather than adding a new frontend round-trip.
- Test-driven: every FR has unit and/or integration coverage; CI e2e
  job gates the PR as with every prior feature.
- Security baseline: the idToken is still never exposed to any client
  component; the email claim is read only server-side, from a token
  already validated by the existing auth pipeline.
- No auto-merge, Copilot review requested on every implementation PR,
  kanban moves via branch+PR - unchanged from established workflow.

## Work Package Sketch (input to /polaris.tasks)

- **WP01 - Owner profile + preferences API** (backend-logic): domain
  entity, repository/Mongo document + index, auth-pipeline email
  capture, preferences query/command + endpoints, unit + integration
  tests. No dependencies - foundational.
- **WP02 - Email delivery** (backend-logic): `IEmailSender` port,
  `ResendEmailSender`, `LoggingEmailSender`, config + DI + startup
  assertion, tests. No dependencies - parallel-safe with WP01.
- **WP03 - Reminder digest scheduler** (backend-logic, deps WP01+WP02):
  `IJobRepository.ListDueOrOverdueAsync`, `ReminderDigestService`,
  digest assembly + send, tests.
- **WP04 - Frontend settings + menu link** (frontend-craft, deps WP01):
  api-client, server actions, settings page + toggle component, system
  menu link, middleware matcher, jest tests, and a user-manual update
  (new "Reminders" section: what triggers a digest, what it contains,
  how to turn it on/off - same treatment assets and the system menu
  got in features 008/009) (FR-10).
- **WP05 - E2E suite** (testing-specialist, deps WP04): preference
  toggle flow + menu-link coverage, full-suite regression.

WP01 and WP02 have no dependency on each other and can be implemented
in either order (or in parallel); WP03 needs both. WP04 only needs
WP01 (the API it calls), not WP02/WP03.

## Risks

- **Auth-pipeline capture cost**: an upsert-check on every authenticated
  request is a small amount of extra work per request; acceptable at
  personal scale, called out explicitly rather than hidden - a future
  optimization (skip if recently synced) is not needed now.
- **CI must never hit real Resend**: the Development/CI default
  (`Email:Provider=Log`) must remain the default so tests can't
  accidentally send real mail; the startup assertion protects
  production from silently not sending, not CI from accidentally
  sending.
- **Digest email deliverability**: no SPF/DKIM/domain setup is in this
  feature's scope - that is a go-live-runbook-style operational step
  (buying/verifying the sending domain with Resend), not application
  code; note it as a follow-up runbook addition rather than blocking
  this feature.
- **Middleware matcher**: adding `/settings/:path*` is easy to forget
  (same class of miss as feature 009's `/assets` gap) - explicit
  subtask in WP04.

## Research / Data model / Contracts

No unknowns requiring a separate research.md. Data model is the single
new `OwnerProfile`/`owner-profiles` record described above; no changes
to existing aggregates. No public API contract changes beyond the two
new `account/notification-preferences` endpoints.
