---
work_package_id: WP03
title: 'Infrastructure: repositories + BackgroundService'
lane: planned
dependencies: []
subtasks: [T015, T016, T017, T018, T019, T020, T021]
assignee: ''
agent: ''
shell_pid: ''
test_status: required
test_file: tests/e2e/WP03-infrastructure-repositories-backgroundservice.e2e.js
review_status: ''
reviewed_by: ''
history:
- timestamp: '2026-05-29T00:00:00Z'
  lane: planned
  agent: system
  action: Prompt generated via /polaris.tasks
domain: backend-logic
---

# WP03 - Infrastructure: repositories + BackgroundService

## Objective

Provide all Infrastructure-layer persistence and scheduling components:
`JobDefinitionDocument` + `JobDefinitionRepository`, extension of `JobDocument`
and `JobRepository` with the new methods, `SystemDateTimeProvider`, and the
`JobGeneratorService` background service. Wire everything into DI. Write
integration tests using real MongoDB via Testcontainers.

## Inputs

- Spec: `polaris-specs/002-recurring-jobs/spec.md` (FR-110..FR-114, SC-102)
- Plan: `polaris-specs/002-recurring-jobs/plan.md` (Phase 3 Infrastructure section)
- Data model: `polaris-specs/002-recurring-jobs/data-model.md` (MongoDB shapes, indexes)
- Existing: `backend/src/HomeMaintenance.Infrastructure/Persistence/` (document patterns)
- Existing: `backend/src/HomeMaintenance.Infrastructure/Persistence/Documents/JobDocument.cs`
- Existing: `backend/src/HomeMaintenance.Infrastructure/Persistence/JobRepository.cs`
- Existing: `backend/src/HomeMaintenance.Infrastructure/DependencyInjection.cs`
- WP02 output: `IJobDefinitionRepository`, `IDateTimeProvider`, new `IJobRepository` methods

## Subtasks

### T015 - JobDefinitionDocument + JobDefinitionRepository

Create `backend/src/HomeMaintenance.Infrastructure/Persistence/Documents/JobDefinitionDocument.cs`:

```csharp
public sealed class JobDefinitionDocument
{
    [BsonId] public string Id { get; set; } = null!;
    public string OwnerId { get; set; } = null!;
    public string PropertyId { get; set; } = null!;
    public string Name { get; set; } = null!;
    public List<StepTemplateDocument> StepTemplates { get; set; } = new();
    public ScheduleDefinitionDocument Schedule { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed class StepTemplateDocument
{
    public string Id { get; set; } = null!;
    public int Order { get; set; }
    public string Description { get; set; } = null!;
}

public sealed class ScheduleDefinitionDocument
{
    public string Unit { get; set; } = null!;  // "Day"|"Week"|"Month"|"Year"
    public int Multiplier { get; set; }
    public string StartDate { get; set; } = null!;  // "yyyy-MM-dd"
    public string? EndDate { get; set; }
}
```

Create `backend/src/HomeMaintenance.Infrastructure/Persistence/JobDefinitionRepository.cs`
implementing `IJobDefinitionRepository`:

- `GetAsync`: filter `{_id: id, ownerId: owner}` - returns null on miss or cross-owner.
- `ListAsync`: filter `{ownerId: owner}` + optional `{propertyId: propertyId}`.
- `ListAllActiveAsync`: filter `{}` (all documents, no owner scope). Used by scheduler only.
- `AddAsync`: `InsertOneAsync`.
- `UpdateAsync`: `ReplaceOneAsync` with filter `{_id: id}`.

Add index creation to the existing `MongoIndexInitializer` (or wherever collection
indexes are bootstrapped):
```
job_definitions collection:
  { ownerId: 1 }
  { ownerId: 1, propertyId: 1 }
```

Use `DateOnly` <-> `"yyyy-MM-dd"` string serialization (same as the pattern used
in `JobDocument` for `DueDate` if applicable, or roll a `DateOnlyBsonSerializer`
if one does not exist).

### T016 - Extend JobDocument + JobRepository

Modify `backend/src/HomeMaintenance.Infrastructure/Persistence/Documents/JobDocument.cs`:
- Add `public string? JobDefinitionId { get; set; }` (nullable).
- No migration needed: missing field reads as null in MongoDB.

Modify `backend/src/HomeMaintenance.Infrastructure/Persistence/JobRepository.cs`:
- Implement `HasGeneratedJobForOccurrenceAsync(string definitionId, DateOnly dueDate, CancellationToken ct)`:
  filter `{ jobDefinitionId: definitionId, dueDate: dueDate.ToString("yyyy-MM-dd") }`, count > 0.
- Implement `LatestGeneratedJobDueDateAsync(string definitionId, CancellationToken ct)`:
  find documents where `{ jobDefinitionId: definitionId }`, sort by `dueDate` descending, take 1,
  return its `DueDate` parsed to `DateOnly`, or null if none.
- Update the `Hydrate` mapping from `JobDocument` to `Job` to pass `jobDefinitionId`.
- Update the document mapping from `Job` to `JobDocument` to include `jobDefinitionId`.

Add sparse index on `jobs` collection:
```
jobs collection:
  { jobDefinitionId: 1 } sparse
```

Register index in the same initializer as T015.

### T017 - SystemDateTimeProvider

Create `backend/src/HomeMaintenance.Infrastructure/Scheduling/SystemDateTimeProvider.cs`:

```csharp
using HomeMaintenance.Application.Common.Interfaces;

namespace HomeMaintenance.Infrastructure.Scheduling;

public sealed class SystemDateTimeProvider : IDateTimeProvider
{
    public DateOnly UtcToday => DateOnly.FromDateTime(DateTime.UtcNow);
}
```

Register as `Singleton` in DI (see T019).

### T018 - JobGeneratorService BackgroundService

Create `backend/src/HomeMaintenance.Infrastructure/Scheduling/JobGeneratorService.cs`:

```csharp
public sealed class JobGeneratorService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<JobGeneratorService> _logger;

    public JobGeneratorService(IServiceScopeFactory scopeFactory, ILogger<JobGeneratorService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RunGenerationPassAsync(stoppingToken);  // startup pass

        using var timer = new PeriodicTimer(TimeSpan.FromHours(24));
        while (await timer.WaitForNextTickAsync(stoppingToken))
            await RunGenerationPassAsync(stoppingToken);
    }

    private async Task RunGenerationPassAsync(CancellationToken ct)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var definitionRepo = scope.ServiceProvider.GetRequiredService<IJobDefinitionRepository>();
        var generationService = scope.ServiceProvider.GetRequiredService<JobGenerationService>();
        var dateTimeProvider = scope.ServiceProvider.GetRequiredService<IDateTimeProvider>();

        var definitions = await definitionRepo.ListAllActiveAsync(ct);
        var today = dateTimeProvider.UtcToday;
        foreach (var definition in definitions)
        {
            try { await generationService.GenerateForDefinition(definition, today, ct); }
            catch (Exception ex) { _logger.LogError(ex, "Generation failed for {DefinitionId}", definition.Id); }
        }
    }
}
```

Note: The service uses `IServiceScopeFactory` because `JobGenerationService`
(Application) and `JobDefinitionRepository` are scoped; `BackgroundService`
is singleton. Each generation pass creates its own scope.

### T019 - Register all new services in DependencyInjection

Modify `backend/src/HomeMaintenance.Infrastructure/DependencyInjection.cs`:

```csharp
// Scoped
services.AddScoped<IJobDefinitionRepository, JobDefinitionRepository>();
services.AddScoped<JobGenerationService>();  // Application-layer service

// Singleton
services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();

// Hosted service
services.AddHostedService<JobGeneratorService>();
```

Ensure `JobGenerationService` from Application is registered here (Infrastructure
DI is responsible for wiring Application services that have Infrastructure dependencies).

### T020 - Integration tests for JobDefinitionRepository

Create `backend/tests/HomeMaintenance.Integration.Tests/JobDefinitions/JobDefinitionRepositoryTests.cs`.

Test class uses the existing `MongoDbFixture` pattern (Testcontainers).

Required tests:
- `AddAndGet_OwnedDefinition_ReturnsCorrectData`
- `Get_CrossOwner_ReturnsNull`
- `List_ByOwner_ReturnsOwnedOnly`
- `List_FilteredByPropertyId_ReturnsMatchingOnly`
- `Update_RoundTrips_ScheduleAndStepTemplates` (update schedule + steps; re-get; assert fields match)
- `ListAllActive_ReturnsAllDefinitionsAcrossOwners`

### T021 - Integration tests for JobGeneratorService scheduler

Create `backend/tests/HomeMaintenance.Integration.Tests/JobDefinitions/JobGeneratorServiceTests.cs`.

Use a `StubDateTimeProvider(DateOnly fixedDate)` (implement inline in the test file
or in a test helpers file):
```csharp
public sealed class StubDateTimeProvider : IDateTimeProvider
{
    public DateOnly UtcToday { get; set; }
    public StubDateTimeProvider(DateOnly today) => UtcToday = today;
}
```

Required tests:
- `Startup_GeneratesOccurrencesWithinHorizon`: create monthly definition starting today;
  run `RunGenerationPassAsync` (expose as internal or test via hosted-service spin-up);
  assert jobs exist for today, today+1m, today+2m, today+3m (4 occurrences if today = start).
- `SecondRun_ProducesNoDuplicates`: run pass twice; count jobs; assert count unchanged.
- `EndDate_InPast_GeneratesNoJobs`: set EndDate = today-1; run pass; assert 0 jobs.
- `StartupRun_FillsGapFromDowntime`: stub date to 45 days ahead; create definition with
  startDate=today; run pass; assert occurrences in new 3-month window from day+45 are created.

These tests spin up a scoped service environment backed by Testcontainers MongoDB;
they do NOT actually start the `BackgroundService` timer. Test the `RunGenerationPassAsync`
logic directly by calling it or by constructing the service and calling `ExecuteAsync`
with a short-lived `CancellationToken`.

## Test Strategy

- Integration tests use real MongoDB via Testcontainers (same fixture as existing integration tests).
- Scheduler tests use `StubDateTimeProvider` to control "today" deterministically.
- No mocking of MongoDB; all assertions go through the repository to verify persistence.

## Definition of Done

- [ ] `JobDefinitionRepository` implements all 5 interface methods.
- [ ] `JobRepository` implements the 2 new interface methods.
- [ ] `JobDocument` has `JobDefinitionId` field; mapping updated in both directions.
- [ ] `SystemDateTimeProvider` present and registered as Singleton.
- [ ] `JobGeneratorService` registered as hosted service.
- [ ] `job_definitions` collection indexes created on startup.
- [ ] Sparse index on `jobs.jobDefinitionId` created on startup.
- [ ] All integration tests pass with real MongoDB.
- [ ] `dotnet test` is green on HomeMaintenance.Integration.Tests.

## Risks

- **Scope in BackgroundService**: use `IServiceScopeFactory`; never inject scoped
  services directly into the hosted service constructor.
- **DateOnly serialization**: verify the BSON serialization roundtrip for `StartDate`
  and `EndDate` strings. If the codebase already has a `DateOnly` serializer, reuse it.
- **ListAllActiveAsync**: this returns ALL definitions with no owner filter. Ensure
  it is only called by the scheduler path, never by user-facing endpoints.

## Run Command

```bash
polaris implement WP03 --base WP02
```
