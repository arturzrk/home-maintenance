# Implementation Plan: Recurring Jobs (Slice 2)

**Branch**: `002-recurring-jobs` | **Date**: 2026-05-29 | **Spec**: [spec.md](spec.md)

## Summary

Introduce `JobDefinition` (template + schedule) as a new aggregate. A `ScheduleDefinition` value object embeds cadence unit + integer multiplier + start/end dates. A .NET `BackgroundService` (injected `IDateTimeProvider`) generates `Job` instances within a rolling 3-month horizon; a manual endpoint generates the next occurrence on demand. Generated jobs extend the Slice 1 `Job` with a nullable `JobDefinitionId` field. The `GET /api/jobs` endpoint already returns both types --- no new list endpoint needed. OccurrenceCalculator logic lives on `ScheduleDefinition` in the Domain layer.

## Technical Context

**Language/Version**: C# 12 / .NET 9 (backend), TypeScript 5.8 / Next.js 15.3 (frontend)
**Primary Dependencies**: MongoDB.Driver, ASP.NET Core Minimal API, Microsoft.Extensions.Hosting (BackgroundService)
**Storage**: MongoDB 7 --- new `job_definitions` collection; `jobs` collection gains `jobDefinitionId` field (sparse index)
**Testing**: xUnit + Shouldly + NSubstitute (unit), xUnit + Testcontainers MongoDB (integration), Jest + Testing Library (frontend)
**Performance Goals**: Generate-next p95 < 500ms; scheduler run completes in < 5s for typical personal-scale definition count (< 50)
**Constraints**: Scheduler must be idempotent; occurrence computation is pure (no I/O); `IDateTimeProvider` is the single source of "now"

## Constitution Check

| Gate | Status | Notes |
|---|---|---|
| Clean Architecture dependency rule | ✅ | `ScheduleDefinition` + OccurrenceCalculator in Domain; `IDateTimeProvider` interface in Application; `BackgroundService` in Infrastructure |
| No leaking abstractions | ✅ | `JobDefinitionDocument` stays in Infrastructure; `JobDefinitionDto` in Application |
| Result<T> for control flow | ✅ | GenerateNext returns `BusinessRuleError` on duplicate |
| Default-deny auth | ✅ | All new endpoints require authentication; scheduler system actor has no public endpoint |
| Audit logging | ✅ | `job_definition.*` and `job.generated` events added to existing sink |
| No speculative abstractions | ✅ | No pause/archive/archive until needed; no per-occurrence override |
| Tests mandatory | ✅ | Unit tests for OccurrenceCalculator + handlers; integration tests for all endpoints + scheduler |

## Project Structure

```
backend/src/
├── HomeMaintenance.Domain/
│   └── JobDefinitions/
│       ├── JobDefinition.cs          # Aggregate root
│       ├── StepTemplate.cs           # Child entity
│       ├── ScheduleDefinition.cs     # Value object + OccurrenceCalculator method
│       └── CadenceUnit.cs            # Day | Week | Month | Year enum
├── HomeMaintenance.Application/
│   ├── Common/Interfaces/
│   │   ├── IJobDefinitionRepository.cs
│   │   └── IDateTimeProvider.cs      # UtcToday abstraction
│   └── JobDefinitions/
│       ├── Dto/JobDefinitionDtos.cs
│       ├── Mapping.cs
│       ├── Commands/
│       │   ├── CreateJobDefinition.cs
│       │   ├── UpdateJobDefinition.cs     # rename + edit steps + change schedule
│       │   └── GenerateNextOccurrence.cs
│       └── Queries/
│           ├── ListJobDefinitions.cs
│           └── GetJobDefinition.cs
├── HomeMaintenance.Infrastructure/
│   ├── Auth/ (no changes)
│   ├── Persistence/
│   │   ├── Documents/JobDefinitionDocument.cs
│   │   └── JobDefinitionRepository.cs
│   ├── Scheduling/
│   │   ├── JobGeneratorService.cs         # BackgroundService
│   │   └── SystemDateTimeProvider.cs      # IDateTimeProvider → DateTime.UtcNow
│   └── DependencyInjection.cs             # register new services
└── HomeMaintenance.API/
    └── Endpoints/JobDefinitionEndpoints.cs

backend/tests/
├── HomeMaintenance.Unit.Tests/
│   ├── Domain/JobDefinitions/
│   │   ├── ScheduleDefinitionTests.cs     # OccurrenceCalculator logic
│   │   └── JobDefinitionTests.cs
│   └── Application/JobDefinitions/
│       ├── CreateJobDefinitionHandlerTests.cs
│       ├── GenerateNextOccurrenceHandlerTests.cs
│       └── UpdateJobDefinitionHandlerTests.cs
└── HomeMaintenance.Integration.Tests/
    ├── JobDefinitions/
    │   ├── JobDefinitionEndpointsTests.cs
    │   └── JobGeneratorServiceTests.cs    # scheduler idempotency, horizon
    └── Acceptance/
        └── FrAcceptanceTests.cs           # FR-113, FR-117 additions

frontend/src/app/
├── properties/[id]/
│   └── page.tsx                           # add JobDefinition list + create form
└── job-definitions/[id]/
    └── page.tsx                           # definition detail: steps, schedule, generate-next
```

## Implementation Strategy

### Phase 0 --- No blocking unknowns

No external research required. All technology choices are established Slice 1 patterns. One open question resolved here:

**Occurrence computation in Domain**: `ScheduleDefinition` gains a method `OccurrencesInRange(DateOnly from, DateOnly to)` returning `IEnumerable<DateOnly>`. Month/year arithmetic uses `DateOnly.AddMonths` / `DateOnly.AddYears` with a day-clamp helper. Day/week cadences use `AddDays`. All pure; no I/O.

**`IDateTimeProvider`**:
```csharp
// Application/Common/Interfaces/IDateTimeProvider.cs
public interface IDateTimeProvider
{
    DateOnly UtcToday { get; }
}

// Infrastructure/Scheduling/SystemDateTimeProvider.cs
public sealed class SystemDateTimeProvider : IDateTimeProvider
{
    public DateOnly UtcToday => DateOnly.FromDateTime(DateTime.UtcNow);
}
```

Registered as `Singleton`. Tests inject a `StubDateTimeProvider(DateOnly fixedDate)`.

### Phase 1 --- Domain

**`CadenceUnit`** enum: `Day | Week | Month | Year`.

**`StepTemplate`** --- child of `JobDefinition`:
- `string Id` (GUID, unique within definition)
- `int Order` (contiguous from 0)
- `string Description` (non-empty, max 500)
- Same add/remove/reorder/edit invariants as Slice 1 `Step`

**`ScheduleDefinition`** value object:
- `CadenceUnit Unit`
- `int Multiplier` (≥ 1)
- `DateOnly StartDate`
- `DateOnly? EndDate` (must be > StartDate if set)
- Method `OccurrencesInRange(DateOnly from, DateOnly to)`:
  - Iterates N = 0, 1, 2, ... computing `Occurrence(N)`
  - Stops when `Occurrence(N) > to` or `> EndDate`
  - Skips occurrences before `from`
  - Month/year clamping: if computed day exceeds month length, use last day

**`JobDefinition`** aggregate root:
- Inherits `Entity`
- `OwnerId Owner` (immutable)
- `string PropertyId` (immutable)
- `string Name` (non-empty, trimmed, max 200)
- `List<StepTemplate> _stepTemplates` (ordered)
- `ScheduleDefinition Schedule`
- `static JobDefinition Create(...)` factory
- `void Rename(string)`, `void UpdateSchedule(ScheduleDefinition)`
- Step template mutations: `AddStepTemplate`, `RemoveStepTemplate`, `ReorderStepTemplates`, `EditStepTemplateDescription` --- same outcome types as Slice 1

**`Job`** --- add to existing aggregate:
- `string? JobDefinitionId` (nullable, immutable after set)
- Extend `Job.Create(...)` to accept optional `jobDefinitionId`
- Extend `Job.Hydrate(...)` likewise

### Phase 2 --- Application

**`IJobDefinitionRepository`**:
```csharp
Task<JobDefinition?> GetAsync(string id, OwnerId owner, CancellationToken ct);
Task<IReadOnlyList<JobDefinition>> ListAsync(OwnerId owner, string? propertyId, CancellationToken ct);
Task AddAsync(JobDefinition definition, CancellationToken ct);
Task UpdateAsync(JobDefinition definition, CancellationToken ct);
Task<bool> HasGeneratedJobForOccurrence(string definitionId, DateOnly dueDate, CancellationToken ct);
Task<DateOnly?> LatestGeneratedJobDueDateAsync(string definitionId, CancellationToken ct);
```

The last two queries delegate to `IJobRepository` in the implementation --- they read from the `jobs` collection filtered by `jobDefinitionId`.

**`IDateTimeProvider`** --- as above.

**Use cases**:

| Handler | Input | Key steps |
|---|---|---|
| `CreateJobDefinitionHandler` | name, propertyId, stepTemplates, schedule | Verify property ownership → create aggregate → persist → trigger inline generation within horizon → audit |
| `UpdateJobDefinitionHandler` | id, optional name/schedule/stepTemplate mutations | Load → mutate → persist → audit |
| `GenerateNextOccurrenceHandler` | definitionId | Load → compute next occurrence → check no duplicate → create Job → persist Job → audit |
| `ListJobDefinitionsHandler` | optional propertyId | Query by owner (+ propertyId filter) → map to DTO |
| `GetJobDefinitionHandler` | id | Load by id+owner → map to DTO |

**Job generation logic** (shared by background service and inline creation):
```
JobGenerationService.GenerateForDefinition(JobDefinition def, DateOnly today):
  horizon = today.AddMonths(3)
  occurrences = def.Schedule.OccurrencesInRange(today, horizon)
  for each occurrence:
    if not HasGeneratedJobForOccurrence(def.Id, occurrence):
      create Job(name=def.Name, steps from templates, dueDate=occurrence, jobDefinitionId=def.Id)
      persist Job
      emit job.generated audit event
```

This service lives in Application, takes `IJobRepository`, `IJobDefinitionRepository`, `IAuditLog` --- no Infrastructure dependency.

### Phase 3 --- Infrastructure

**`JobDefinitionDocument`** --- MongoDB document:
```
{ _id, ownerId, propertyId, name, stepTemplates: [{id, order, description}], schedule: {unit, multiplier, startDate, endDate?}, createdAt, updatedAt }
```
Collection: `job_definitions`. Indexes: `{ ownerId: 1 }`, `{ ownerId: 1, propertyId: 1 }`.

**`JobDocument`** --- add field `jobDefinitionId` (string, nullable). Sparse index: `{ jobDefinitionId: 1 }` (only indexes documents where field exists, for the "find generated jobs by definition" query).

**`JobGeneratorService`** (`BackgroundService`):
- Uses `PeriodicTimer` with 24-hour interval
- On each tick: load all definitions for all owners (system actor query --- no OwnerId filter needed here; the service IS the system), call `JobGenerationService.GenerateForDefinition` for each
- Runs once on startup (before entering the periodic loop) to cover any gaps from downtime

### Phase 4 --- API

New endpoint group `/api/job-definitions`:

| Verb | Route | Handler | Response |
|---|---|---|---|
| POST | `/api/job-definitions` | CreateJobDefinitionHandler | 201 + JobDefinitionDto |
| GET | `/api/job-definitions` | ListJobDefinitionsHandler | 200 + JobDefinitionDto[] |
| GET | `/api/job-definitions/{id}` | GetJobDefinitionHandler | 200 + JobDefinitionDto |
| PATCH | `/api/job-definitions/{id}` | UpdateJobDefinitionHandler | 200 + JobDefinitionDto |
| POST | `/api/job-definitions/{id}/generate-next` | GenerateNextOccurrenceHandler | 201 + JobDto |

`GET /api/jobs` --- unchanged endpoint; `JobDto` gains nullable `jobDefinitionId` field.

### Phase 5 --- Frontend

- **Property page** (`/properties/[id]`): add "Recurring jobs" section below one-shot jobs; list `JobDefinition` names with schedule summary; "Add recurring job" form (name, steps, schedule picker: unit dropdown + multiplier number input + start date).
- **JobDefinition detail page** (`/job-definitions/[id]`): name + schedule header (editable), step template list (same add/remove/reorder/edit UX as Slice 1 job steps), "Generate next" button, list of generated jobs linked to this definition.
- **Job list display**: generated jobs show a "↻ From: {definition name}" badge; one-shot jobs are unchanged.

### Phase 6 --- Tests

**Unit**:
- `ScheduleDefinitionTests`: OccurrencesInRange for all four cadence units; multiplier > 1; month-end clamping; end-date cutoff; start date in past (only from-forward results); empty range.
- Handler tests: CreateJobDefinition success + validation; GenerateNextOccurrence success + duplicate rejection + not-found; UpdateJobDefinition non-destructive (existing job step count unchanged).

**Integration**:
- `JobDefinitionEndpointsTests`: full CRUD, cross-owner 404, anonymous 401.
- `JobGeneratorServiceTests`: scheduler run generates correct occurrences; second run produces no duplicates; end-date respected; `IDateTimeProvider` stubbed to control "today".
- `FrAcceptanceTests` additions: FR-113 (idempotency), FR-117 (duplicate rejection), FR-104 (non-destructive edit).

## Work Package Outline

| WP | Title | Depends on |
|---|---|---|
| WP01 | Domain: ScheduleDefinition + JobDefinition aggregate + Job.JobDefinitionId | --- |
| WP02 | Application: interfaces + use cases + JobGenerationService | WP01 |
| WP03 | Infrastructure: repositories + BackgroundService + IDateTimeProvider | WP02 |
| WP04 | API: JobDefinition endpoints + JobDto update | WP03 |
| WP05 | Frontend: JobDefinition create/list on Property page | WP04 |
| WP06 | Frontend: JobDefinition detail page + Generate Next | WP05 |
| WP07 | Hardening: cross-owner matrix, idempotency suite, FR-named tests | WP06 |

## Risks and Non-Obvious Decisions

- **Month-end clamping is the trickiest part of OccurrenceCalculator.** `DateOnly.AddMonths` in .NET already handles this correctly (Jan 31 + 1 month = Feb 28/29). Verify this in unit tests with leap year cases (Feb 29 + 12 months).
- **Scheduler cold-start gap**: if the app is down for > 24h, the horizon may have advanced beyond what was generated. The startup run (before the periodic loop) closes this gap by treating today as the reference point.
- **`HasGeneratedJobForOccurrence` performance**: on a sparse index over `jobDefinitionId`, this query is O(1) per occurrence. For personal scale (< 50 definitions x ~3 occurrences = 150 checks) this is negligible.
- **Inline generation on create**: when a `JobDefinition` is created, the handler immediately runs `GenerateForDefinition` (within the same request). This keeps the user experience snappy --- they see upcoming jobs right away --- without a separate async mechanism.
- **`UpdateJobDefinition` does not re-generate**: changing the schedule does not delete or recreate already-generated jobs. The next scheduler run (or a "Generate next" invocation) uses the new cadence going forward. This matches the spec and avoids surprising data mutations.
