# Tasks - 011 Reminders (due/overdue job email digest)

Daily digest email per owner for jobs due within 3 days or overdue,
grouped by property, with a toggle to turn reminders on/off.
Spec: [spec.md](spec.md), plan: [plan.md](plan.md). Issue #112.

## Work Packages

### WP01 - Owner profile + preferences API (backend-logic)

- [x] T001 Domain: `OwnerProfile` (Owner, Email, RemindersEnabled) - `Create`/`Hydrate`, `UpdateEmail`, `SetRemindersEnabled`
- [x] T002 `IOwnerProfileRepository` + Mongo document/repository + unique index on `ownerId`
- [x] T003 Auth-pipeline email capture (reads the `email` claim, upserts if missing/changed) - zero frontend involvement
- [x] T004 Application: `GetNotificationPreferencesQuery` / `UpdateNotificationPreferencesCommand` (default `RemindersEnabled: true` when no profile exists)
- [x] T005 `AccountEndpoints`: `GET`/`PATCH /api/account/notification-preferences`
- [x] T006 Unit tests: `OwnerProfile` domain behavior + handlers
- [x] T007 Integration tests: endpoints (auth/default/persist) + auto-capture, Mongo Testcontainers

Dependencies: none - foundational.

### WP02 - Email delivery (backend-logic)

- [x] T008 `IEmailSender` port
- [x] T009 `ResendEmailSender` (HttpClient POST to Resend API)
- [x] T010 `LoggingEmailSender` (Development/CI default, no-op)
- [x] T011 Config (`Email:Provider`, `Email:Resend:ApiKey`, `Email:FromAddress`) + DI + startup fail-fast assertion
- [x] T012 Unit tests: request shape, logging sender, startup assertion

Dependencies: none - parallel-safe with WP01.

### WP03 - Reminder digest scheduler (backend-logic, deps: WP01, WP02)

- [x] T013 `IJobRepository.ListDueOrOverdueAsync(DateOnly onOrBefore, ct)` - system-wide, active + due-or-overdue
- [x] T014 `ReminderDigestService : BackgroundService` (mirrors `JobGeneratorService`: startup pass + 24h `PeriodicTimer` + public `RunDigestPassAsync`)
- [x] T015 Digest assembly: group by owner then property, skip disabled/no-email owners
- [x] T016 Email content build (subject/HTML, job links, settings footer link) + `Frontend:BaseUrl` config
- [x] T017 Send per owner with failure isolation (FR-09) + DI registration
- [x] T018 Unit tests: qualification rules against fakes
- [x] T019 Integration tests: `ListDueOrOverdueAsync` against Mongo Testcontainers

Dependencies: WP01, WP02.

### WP04 - Frontend settings + menu link (frontend-craft, deps: WP01)

- [x] T020 api-client: `notificationPreferences.get/update`
- [x] T021 Server actions (`settings/notifications/actions.ts`)
- [x] T022 Settings page + toggle component
- [x] T023 System menu: "Notification settings" link
- [x] T024 Middleware: add `/settings/:path*` to the protected matcher
- [x] T025 User manual: new "Reminders" section (FR-10)
- [x] T026 Jest: toggle component

Dependencies: WP01 (not WP02/WP03).

### WP05 - E2E: notification settings suite (testing-specialist, deps: WP04)

- [x] T027 WP11-1: menu link navigates to settings
- [x] T028 WP11-2: toggle off persists across reload
- [x] T029 WP11-3: toggle on persists across reload
- [x] T030 Full-suite regression (existing + new) local + CI

Dependencies: WP04.

## Sequencing

WP01 and WP02 have no dependency on each other - implement in either
order or in parallel. WP03 needs both. WP04 only needs WP01. WP05 needs
WP04.

MVP = WP01 + WP03 (digests actually go out); WP04/WP05 give owners the
toggle and lock the UI behavior.
