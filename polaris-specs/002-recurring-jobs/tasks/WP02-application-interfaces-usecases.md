---
work_package_id: WP02
title: 'Application: interfaces, use cases, generation logic'
lane: "testing"
dependencies: []
base_branch: main
base_commit: e4e79a6ef99a106868cf6d6c95256861defcf4db
created_at: '2026-06-05T13:08:30.837784+00:00'
subtasks: [T008, T009, T010, T011, T012, T013, T014]
assignee: ''
agent: "claude"
shell_pid: "90162"
test_status: required
test_file: tests/e2e/WP02-application-interfaces-use-cases-generation-logic.e2e.js
review_status: ''
reviewed_by: ''
history:
- timestamp: '2026-05-29T00:00:00Z'
  lane: planned
  agent: system
  action: Prompt generated via /polaris.tasks
domain: backend-logic
---

# WP02 - Application: interfaces, use cases, generation logic

## Objective

Add all Application-layer code for recurring jobs: the two new interfaces
(`IJobDefinitionRepository`, `IDateTimeProvider`), the DTO + mapping layer,
the shared `JobGenerationService`, and five command/query handlers with unit
tests. No Infrastructure or API code is touched in this WP.

## Inputs

- Spec: `polaris-specs/002-recurring-jobs/spec.md` (FR-101..FR-122)
- Plan: `polaris-specs/002-recurring-jobs/plan.md` (Phase 2 Application section)
- Data model: `polaris-specs/002-recurring-jobs/data-model.md` (DTOs, interfaces)
- Contracts: `polaris-specs/002-recurring-jobs/contracts/job-definitions.md`
- Existing: `backend/src/HomeMaintenance.Application/Common/Interfaces/` (pattern reference)
- Existing: `backend/src/HomeMaintenance.Application/Jobs/` (handler pattern)
- WP01 output: `HomeMaintenance.Domain.JobDefinitions.*`

## Subtasks

### T008 [P] - IJobDefinitionRepository and IDateTimeProvider interfaces

Create two files:

**`backend/src/HomeMaintenance.Application/Common/Interfaces/IJobDefinitionRepository.cs`**:
```csharp
using HomeMaintenance.Domain.Identity;
using HomeMaintenance.Domain.JobDefinitions;

namespace HomeMaintenance.Application.Common.Interfaces;

public interface IJobDefinitionRepository
{
    Task<JobDefinition?> GetAsync(string id, OwnerId owner, CancellationToken ct);
    Task<IReadOnlyList<JobDefinition>> ListAsync(OwnerId owner, string? propertyId, CancellationToken ct);
    Task<IReadOnlyList<JobDefinition>> ListAllActiveAsync(CancellationToken ct);
    Task AddAsync(JobDefinition definition, CancellationToken ct);
    Task UpdateAsync(JobDefinition definition, CancellationToken ct);
}
```

Note: `ListAllActiveAsync` is the system-actor query used by the background
scheduler (no OwnerId filter). The duplicate/date checks delegate to
`IJobRepository` - they are NOT on this interface.

**`backend/src/HomeMaintenance.Application/Common/Interfaces/IDateTimeProvider.cs`**:
```csharp
namespace HomeMaintenance.Application.Common.Interfaces;

public interface IDateTimeProvider
{
    DateOnly UtcToday { get; }
}
```

No tests needed for pure interfaces.

### T009 [P] - DTOs, mapping, and JobDto extension

Create `backend/src/HomeMaintenance.Application/JobDefinitions/Dto/JobDefinitionDtos.cs`:

```csharp
public sealed record ScheduleDefinitionDto(
    string Unit,
    int Multiplier,
    DateOnly StartDate,
    DateOnly? EndDate);

public sealed record StepTemplateDto(
    string Id,
    int Order,
    string Description);

public sealed record JobDefinitionDto(
    string Id,
    string PropertyId,
    string Name,
    ScheduleDefinitionDto Schedule,
    IReadOnlyList<StepTemplateDto> StepTemplates);
```

Create `backend/src/HomeMaintenance.Application/JobDefinitions/Mapping.cs`:
Extension methods `ToDto()` for `JobDefinition` -> `JobDefinitionDto`,
`ScheduleDefinition` -> `ScheduleDefinitionDto`, `StepTemplate` -> `StepTemplateDto`.

Extend existing Job DTOs in `backend/src/HomeMaintenance.Application/Jobs/Dto/JobDtos.cs`:
- Add `string? JobDefinitionId` to `JobSummaryDto` and `JobDetailDto`.
- Update the Job -> DTO mapping in `backend/src/HomeMaintenance.Application/Jobs/Mapping.cs`
  to include the new field.

The `Unit` field in `ScheduleDefinitionDto` is serialized as the enum name
string (e.g. `"Month"`). Use `.ToString()` in mapping.

### T010 - JobGenerationService

Create `backend/src/HomeMaintenance.Application/JobDefinitions/JobGenerationService.cs`.

This is the shared generation logic used by both inline creation (handler) and
the background scheduler. It lives in Application, takes only Application
interfaces.

Constructor dependencies: `IJobRepository`, `IAuditLog`.
(No `IJobDefinitionRepository` needed here - callers pass the definition in.)

Method signature:
```csharp
public async Task GenerateForDefinition(
    JobDefinition definition,
    DateOnly today,
    CancellationToken ct)
```

Algorithm:
1. `horizon = today.AddMonths(3)`
2. `occurrences = definition.Schedule.OccurrencesInRange(today, horizon)`
3. For each occurrence:
   - Check `await _jobRepository.HasGeneratedJobForOccurrenceAsync(definition.Id, occurrence, ct)`
   - If already exists: skip
   - Create job: `Job.Create(Guid.NewGuid().ToString("N"), definition.Owner, definition.PropertyId, definition.Name, occurrence, definition.StepTemplates.Select(st => st.Description), definition.Id)`
   - `await _jobRepository.AddAsync(job, ct)`
   - Emit `job.generated` audit event: `actor=system, trigger=scheduler, definitionId=definition.Id, occurrenceDate=occurrence`

Note: `IJobRepository` needs a new method `HasGeneratedJobForOccurrenceAsync(string definitionId, DateOnly dueDate, CancellationToken ct)`. Add it to the existing interface in `backend/src/HomeMaintenance.Application/Common/Interfaces/IJobRepository.cs`.

### T011 - CreateJobDefinition command + handler + unit tests

Create `backend/src/HomeMaintenance.Application/JobDefinitions/Commands/CreateJobDefinition.cs`:

Command record:
```csharp
public sealed record CreateJobDefinitionCommand(
    OwnerId Owner,
    string PropertyId,
    string Name,
    ScheduleDefinitionDto Schedule,
    IReadOnlyList<string> StepDescriptions);
```

Handler (`CreateJobDefinitionHandler`):
1. Verify property ownership: `await _propertyRepository.GetAsync(command.PropertyId, command.Owner, ct)` - if null, return `Result.Failure(new NotFoundError("Property", command.PropertyId))`
2. Parse `ScheduleDefinition` from dto (throw -> return `ValidationError` if invalid)
3. `JobDefinition.Create(Guid.NewGuid()..., command.Owner, ...)`
4. `await _definitionRepository.AddAsync(definition, ct)`
5. `await _generationService.GenerateForDefinition(definition, _dateTimeProvider.UtcToday, ct)`
6. Emit `job_definition.created` audit event
7. Return `Result.Success(definition.ToDto())`

Unit tests in `backend/tests/HomeMaintenance.Unit.Tests/Application/JobDefinitions/CreateJobDefinitionHandlerTests.cs`:
- `Success_CreatesDefinitionAndGeneratesJobs`
- `InvalidSchedule_MultiplierZero_ReturnsValidationError`
- `CrossPropertyOwnership_ReturnsNotFoundError`

Use NSubstitute for all interfaces.

### T012 - ListJobDefinitions and GetJobDefinition query handlers + unit tests

Create `backend/src/HomeMaintenance.Application/JobDefinitions/Queries/ListJobDefinitions.cs`:

Query: `ListJobDefinitionsQuery(OwnerId Owner, string? PropertyId)`
Handler: calls `_definitionRepository.ListAsync(query.Owner, query.PropertyId, ct)`, maps to `IReadOnlyList<JobDefinitionDto>`.

Create `backend/src/HomeMaintenance.Application/JobDefinitions/Queries/GetJobDefinition.cs`:

Query: `GetJobDefinitionQuery(string Id, OwnerId Owner)`
Handler: loads by id+owner; returns `NotFoundError` on null.

Unit tests in `backend/tests/HomeMaintenance.Unit.Tests/Application/JobDefinitions/GetJobDefinitionHandlerTests.cs`:
- `GetById_Owned_ReturnsDtoSuccess`
- `GetById_NotFound_ReturnsNotFoundError`
- `GetById_CrossOwner_ReturnsNotFoundError` (repository returns null)
- `List_ByOwner_ReturnsMappedDtos`
- `List_FilteredByPropertyId_PassesFilterToRepository`

### T013 - UpdateJobDefinition command + handler + unit tests

Create `backend/src/HomeMaintenance.Application/JobDefinitions/Commands/UpdateJobDefinition.cs`:

Command record (all mutations optional):
```csharp
public sealed record UpdateJobDefinitionCommand(
    string Id,
    OwnerId Owner,
    string? Name,
    ScheduleDefinitionDto? Schedule,
    IReadOnlyList<string>? AddStepDescriptions,
    IReadOnlyList<string>? RemoveStepTemplateIds,
    IReadOnlyList<string>? ReorderStepTemplateIds,
    IReadOnlyList<(string Id, string Description)>? EditStepTemplates);
```

Handler applies each non-null mutation in order: Rename, UpdateSchedule,
RemoveStepTemplate (each id), ReorderStepTemplates, EditStepTemplate (each),
AddStepTemplate (each). Maps outcome enums to appropriate `Result.Failure` types.
Calls `_definitionRepository.UpdateAsync` once after all mutations.
Emits one audit event per changed field type.

Unit tests in `backend/tests/HomeMaintenance.Unit.Tests/Application/JobDefinitions/UpdateJobDefinitionHandlerTests.cs`:
- `Rename_ValidName_Succeeds`
- `Rename_EmptyName_ReturnsValidationError`
- `UpdateSchedule_ValidSchedule_Persists`
- `AddStep_AppendsToStepTemplates`
- `RemoveStep_KnownId_Removes`
- `RemoveStep_UnknownId_ReturnsNotFound`
- `NotFound_ReturnsNotFoundError`

### T014 - GenerateNextOccurrence command + handler + unit tests

Create `backend/src/HomeMaintenance.Application/JobDefinitions/Commands/GenerateNextOccurrence.cs`:

Command: `GenerateNextOccurrenceCommand(string DefinitionId, OwnerId Owner)`

Handler algorithm:
1. Load definition (return `NotFoundError` if null/wrong owner)
2. `latestDueDate = await _jobRepository.LatestGeneratedJobDueDateAsync(definition.Id, ct)`
   - If null: `nextOccurrence = definition.Schedule.StartDate` (no jobs yet)
   - Else: find the first occurrence strictly after `latestDueDate` via `Schedule.OccurrencesInRange(latestDueDate.AddDays(1), latestDueDate.AddYears(10))`; take first or error if none
3. Check `HasGeneratedJobForOccurrenceAsync(definition.Id, nextOccurrence)` - if true return `BusinessRuleError("next_occurrence_already_exists", "...")`
4. Create and persist job (same as GenerateForDefinition)
5. Emit `job.generated` audit event with `trigger=manual`
6. Return `Result.Success(job.ToDetailDto())`

Add `LatestGeneratedJobDueDateAsync(string definitionId, CancellationToken ct)` to `IJobRepository`.

Unit tests in `backend/tests/HomeMaintenance.Unit.Tests/Application/JobDefinitions/GenerateNextOccurrenceHandlerTests.cs`:
- `NoExistingJobs_UsesStartDate`
- `ExistingJobs_UsesNextOccurrenceAfterLatest`
- `NextOccurrenceAlreadyExists_ReturnsBusinessRuleError`
- `DefinitionNotFound_ReturnsNotFoundError`

## Test Strategy

- All tests: NSubstitute for interfaces, Shouldly for assertions, xUnit Facts.
- Test the handler logic in isolation; repository/audit calls verified via `Received()`.
- For `GenerateNextOccurrenceHandler`, use a `ScheduleDefinition` with a fixed
  StartDate so occurrence arithmetic is deterministic.

## Definition of Done

- [ ] All new files compile with no warnings or errors.
- [ ] `IJobRepository` has the two new methods (will fail to compile at Infrastructure until WP03 implements them - that is expected).
- [ ] All unit test files created with all specified test methods passing.
- [ ] `dotnet test` is green on Unit.Tests (Infrastructure tests that implement new IJobRepository methods may not compile yet - only run Unit.Tests project).
- [ ] `JobDefinitionDtos.cs` and `Mapping.cs` present and correct.
- [ ] `JobSummaryDto` and `JobDetailDto` have `JobDefinitionId` field.

## Risks

- **IJobRepository changes**: adding two methods to the existing interface will
  cause compilation failures in Infrastructure until WP03 implements them. This
  is expected. The Unit.Tests project mocks the interface so it still compiles.
- **GenerateNextOccurrence edge case**: if the definition's schedule has an
  EndDate in the past, `OccurrencesInRange` on a 10-year window returns empty;
  return a `BusinessRuleError("no_future_occurrence", ...)` in that case.
- **Audit event shape**: follow the events defined in spec.md Audit Logging
  section exactly (field names matter for future log parsing).

## Run Command

```bash
polaris implement WP02 --base WP01
```

## Activity Log

- 2026-06-05T13:08:31Z – claude – shell_pid=90162 – lane=doing – Assigned agent via workflow command
- 2026-06-06T12:03:04Z – claude – shell_pid=90162 – lane=testing – Tests passing (154/154); PR #27 merged
