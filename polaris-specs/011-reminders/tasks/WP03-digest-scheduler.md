---
work_package_id: WP03
title: Reminder digest scheduler
lane: planned
dependencies: ["WP01", "WP02"]
subtasks: [T013, T014, T015, T016, T017, T018, T019]
test_status: required
test_file: backend/tests/HomeMaintenance.UnitTests/Scheduling/ReminderDigestServiceTests.cs
domain: backend-logic
---

# WP03 - Reminder digest scheduler

## Objective

The daily background pass: find every active job due within 3 days or
overdue, group by owner then property, and send one digest email per
affected owner via `IEmailSender` (WP02), using `OwnerProfile` (WP01) to
resolve contact email and the on/off preference.

## Context

- `JobGeneratorService.cs` (`Infrastructure/Scheduling`) is the exact
  shape to mirror: `BackgroundService` + `IServiceScopeFactory` (scoped
  deps, singleton service), an initial startup pass, then a 24h
  `PeriodicTimer`, and a **public** `RunXAsync(ct)` method so tests can
  trigger a pass deterministically instead of waiting on the timer.
- `IJobDefinitionRepository.ListAllActiveAsync(ct)` (no `OwnerId` param)
  is the precedent for an owner-less, system-wide query used only by a
  background process.
- Per-item try/catch + `_logger.LogError` so one owner's failure doesn't
  abort the pass (FR-09) - mirror `JobGeneratorService` exactly.
- Depends on WP01 (`IOwnerProfileRepository`) and WP02 (`IEmailSender`).

## Subtasks

### T013 - `IJobRepository.ListDueOrOverdueAsync`

New method: `ListDueOrOverdueAsync(DateOnly onOrBefore, CancellationToken ct)`.
Active status, `DueDate != null && DueDate <= onOrBefore`, system-wide
(no owner filter).

### T014 - `ReminderDigestService`

`BackgroundService`, structurally identical to `JobGeneratorService`:
startup pass, 24h `PeriodicTimer`, public `RunDigestPassAsync(ct)`.
Register via `services.AddHostedService<ReminderDigestService>()` in
`Infrastructure/DependencyInjection.cs`.

### T015 - Digest assembly

`today = dateTimeProvider.UtcToday`; `horizon = today.AddDays(3)`;
`dueJobs = await jobRepository.ListDueOrOverdueAsync(horizon, ct)`.
Group by `Owner`. For each group: look up `OwnerProfile`; skip (log,
continue) if missing, no email, or `RemindersEnabled == false`.
Otherwise group that owner's jobs by `PropertyId`, resolve each
property's name via `IPropertyRepository.GetAsync(propertyId, owner, ct)`.

### T016 - Email content build

Subject + HTML body: grouped by property, each job a link to
`{Frontend:BaseUrl}/jobs/{jobId}`; footer link to
`{Frontend:BaseUrl}/settings/notifications` (FR-05/US5). New config
`Frontend:BaseUrl` (dev `http://localhost:3000`, staging
`https://staging.maintained.house`, prod `https://maintained.house`).

### T017 - Send + failure isolation

`await emailSender.SendAsync(...)` per owner inside a try/catch that
logs and continues on failure (FR-09), mirroring
`JobGeneratorService`'s per-definition try/catch.

### T018 - Unit tests: qualification rules

Against fakes: due-in-3-days included, due-in-4-days excluded, overdue
included, completed excluded, `RemindersEnabled=false` skipped, no
stored email skipped, one owner's send failure doesn't block others.

### T019 - Integration tests

`ListDueOrOverdueAsync` against Mongo Testcontainers.

## Definition of Done

- [ ] `ListDueOrOverdueAsync` implemented and tested
- [ ] `ReminderDigestService` mirrors `JobGeneratorService`'s shape (startup pass + 24h timer + public run method)
- [ ] One owner's delivery failure never blocks another owner's digest (FR-09)
- [ ] A digest reflects state at send time only (FR-08) - no retroactive updates, nothing stored beyond the pass itself
- [ ] Unit + integration tests green

## Risks

- Digest email deliverability (SPF/DKIM/domain verification with
  Resend) is out of this WP's scope - an operational, go-live-runbook
  step, not application code.

## Run Command

```bash
polaris implement WP03 --base WP01
```
