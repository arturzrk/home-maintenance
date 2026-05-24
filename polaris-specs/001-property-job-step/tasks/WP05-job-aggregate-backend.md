---
work_package_id: WP05
lane: "done"
dependencies: [WP02]
base_branch: 001-property-job-step-WP02
base_commit: f469a5ac99802743e58a36a6dcb9d7ee04a30e94
created_at: '2026-05-15T10:03:42.001870+00:00'
subtasks: [T026, T027, T028, T029, T030, T031]
test_status: required
test_file: tests/e2e/WP05-wp05-job-aggregate-backend.e2e.js
domain: backend-logic
shell_pid: "60117"
agent: "claude"
assignee: "arturzrk@gmail.com"
reviewed_by: "Artur Żurek"
review_status: "approved"
---

# WP05 - Job aggregate backend

## Objective

Deliver the Job aggregate from Domain through to API for the headline
flows: create a Job with steps, list/get Jobs, tick/untick a step,
explicitly complete a Job. Step mutation beyond initial creation
(add/remove/reorder/edit) and Job rename are deferred to WP07. Frontend
is WP06.

This WP can run in parallel with WP04 once WP03 has landed (it does
not require any of WP04's frontend changes, only the Property API
which WP03 provides).

## Inputs

- Spec: FR-014..FR-018 (Jobs), FR-021..FR-027 (Steps subset relevant
  here), user stories US3, US4, US5.
- Data model: `data-model.md` "Job" + "Step" + "JobDocument" +
  "StepDocument" + indexes + state diagrams.
- Contracts: `contracts/jobs.md` and the tick / untick / complete
  sections of `contracts/steps.md`.

## Subtasks

### T026 - Domain: Job aggregate + Step entity

Create `backend/src/HomeMaintenance.Domain/Jobs/`:

- `JobStatus.cs` - enum { Active, Completed }.
- `Step.cs` - sealed class extending Entity. Methods: `Tick(DateTime)`,
  `Untick()`, `EditDescription(string)`, `SetOrder(int)`. All
  except construction are `internal` (only Job calls them).
- `Job.cs` - sealed aggregate root. Members per `data-model.md`:
  - Private constructor + static `Create(...)` factory.
  - Static `Hydrate(...)` factory for Infrastructure
    reconstruction (no validation, internal).
  - Methods: `Rename`, `SetDueDate`, `AddStep`, `RemoveStep`,
    `ReorderSteps`, `EditStepDescription`, `TickStep`, `UntickStep`,
    `Complete`. Each routes through `EnsureActive()` for non-creation
    cases.
  - `_steps` is a private `List<Step>` exposed via
    `IReadOnlyList<Step> Steps => _steps.AsReadOnly();`.
  - `RenumberSteps()` runs after every remove and reorder so `Order`
    stays contiguous from 0.

Decorate `Domain.csproj` with
`<InternalsVisibleTo Include="HomeMaintenance.Infrastructure" />` and
`<InternalsVisibleTo Include="HomeMaintenance.Unit.Tests" />` so
mappers and tests can reach the internal helpers.

Unit tests in
`backend/tests/HomeMaintenance.Unit.Tests/Domain/Jobs/`:

| Test | Asserts |
|---|---|
| `Create_WithValidInputs_StartsActive` | Status Active, CompletedAt null, Steps empty if input empty, Orders 0..N-1 if input N-long |
| `Create_WithEmptyName_Throws` | ArgumentException |
| `Create_WithName201Chars_Throws` | ArgumentException |
| `Create_WithStepDescription501Chars_Throws` | ArgumentException at step creation |
| `AddStep_AssignsNextOrder` | Order == _steps.Count before add |
| `AddStep_OnCompletedJob_Throws` | InvalidOperationException (EnsureActive) |
| `RemoveStep_Renumbers` | Remove middle step -> Orders 0..N-2 contiguous |
| `RemoveStep_UnknownId_ReturnsNotFound` | Result.Failure with NotFoundError |
| `ReorderSteps_FullList_SucceedsAndRenumbers` | Orders match new order |
| `ReorderSteps_PartialList_ReturnsValidationError` | |
| `ReorderSteps_WithDuplicate_ReturnsValidationError` | |
| `ReorderSteps_WithUnknownId_ReturnsValidationError` | |
| `TickStep_KnownId_SetsCompletedAt` | IsCompleted true, CompletedAt set |
| `TickStep_Idempotent` | Tick twice; CompletedAt unchanged after second tick |
| `UntickStep_ClearsCompletedAt` | IsCompleted false, CompletedAt null |
| `EditStepDescription_Trims` | |
| `EditStepDescription_Empty_Throws` | (or returns Validation; pick consistent shape - prefer Result.Failure in aggregate methods that already return Result, throw for raw constructors) |
| `Complete_AllStepsDone_Transitions` | Status -> Completed, CompletedAt set |
| `Complete_AnyStepIncomplete_ReturnsBusinessRule_steps_incomplete` | |
| `Complete_NoSteps_ReturnsBusinessRule_job_has_no_steps` | |
| `Complete_AlreadyCompleted_ReturnsBusinessRule_job_already_completed` | |
| `MutationOnCompletedJob_Throws_EnsureActive` | for AddStep, RemoveStep, etc. (one parameterised test) |

Aim for ~25 unit tests across Job and Step. The aggregate is the
business heart of Slice 1; thorough coverage is non-negotiable.

### T027 - Infrastructure: JobDocument + JobRepository

Create
`backend/src/HomeMaintenance.Infrastructure/Persistence/Documents/JobDocument.cs`
and `StepDocument.cs` per `data-model.md`. Use BSON attributes for
field names.

Create
`backend/src/HomeMaintenance.Infrastructure/Persistence/JobRepository.cs`:

- `Get(id, owner)`: filter `_id == id && ownerId == owner.Value`,
  hydrate via mapper.
- `List(owner, propertyId?, status?)`: filter on owner + optional
  propertyId + optional status (string-encoded enum). Project out
  `steps` for the list view to keep payloads small; callers needing
  steps use `Get`.
- `Add(job)`: insert.
- `Update(job)`: `ReplaceOneAsync` with the full document; ownership
  enforced in filter (`Id == job.Id && OwnerId == job.Owner.Value`).
  Acceptable for Slice 1; targeted `$set`/`$pull` optimisations are
  noted in `research.md` R2 as a follow-up.

Add the indexes from `data-model.md`:
- `{ ownerId: 1 }`
- `{ ownerId: 1, propertyId: 1 }`
- `{ ownerId: 1, status: 1 }`

Add a `DateOnly` BSON serialiser if MongoDB.Driver 3.1.0 does not
include one out of the box (test with a round-trip).

Mappers (`JobMappings.ToDomain()`, `Job.ToDocument()`) live next to
the repository. `Job.Hydrate` is the internal factory the mapper uses.

Register in `Infrastructure.DependencyInjection`:
```csharp
services.AddScoped<IJobRepository, JobRepository>();
```

### T028 - Application: CreateJob + GetJob + ListJobs

`CreateJobCommand(PropertyId, Name, DueDate?, IReadOnlyList<string> StepDescriptions)`.

Handler:
1. Resolve `OwnerId` from `IIdentityProvider`.
2. Verify `propertyRepo.GetAsync(propertyId, owner)` returns non-null.
   If null, return `NotFoundError("Property", propertyId)`.
3. Call `Job.Create(...)`. Wrap `ArgumentException` -> `ValidationError`.
4. `jobRepo.AddAsync(job)`.
5. Emit `job.created` audit event with `propertyId`, `name`,
   `dueDate`, `stepCount`.
6. Return `JobDetailDto`.

`GetJobQuery(Id)` -> `Result<JobDetailDto>` or `NotFoundError`.

`ListJobsQuery(PropertyId?, Status?)` -> list filtered by caller's
owner.

Unit tests with NSubstitute: success path, validation, cross-owner
property -> NotFound (FR-008).

### T029 - Application: TickStep + UntickStep + CompleteJob

`TickStepCommand(JobId, StepId)`:
1. Load Job by id+owner; NotFound if null.
2. `job.TickStep(stepId, DateTime.UtcNow)`. Returns
   `Result<None>`. If success: `UpdateAsync(job)` and emit
   `step.ticked` audit event.

`UntickStepCommand(JobId, StepId)`: analogous; emit `step.unticked`.

`CompleteJobCommand(JobId)`:
1. Load Job.
2. `job.Complete(DateTime.UtcNow)`. Returns `Result<None>`.
3. On success: `UpdateAsync(job)` and emit `job.completed` audit
   event.
4. Pass through BusinessRuleError (`steps_incomplete`,
   `job_has_no_steps`, `job_already_completed`).

Unit tests:
- TickStep success and step-not-found.
- TickStep on completed job: aggregate throws `EnsureActive`; handler
  catches and returns `BusinessRuleError("job_completed", ...)`.
  Update Job aggregate `TickStep` to return `Result.Failure` with
  this error instead of throwing - aligns with FR-027 semantics. Or
  alternatively, have the handler check `Status == Completed` upfront
  and short-circuit; pick ONE pattern and document it in the WP
  prompt. (Recommended: handler short-circuits, aggregate keeps
  `EnsureActive` as defence-in-depth.)
- CompleteJob success.
- CompleteJob with any step incomplete: BusinessRuleError
  `steps_incomplete`.
- CompleteJob with zero steps: BusinessRuleError `job_has_no_steps`.
- CompleteJob already completed: BusinessRuleError
  `job_already_completed`.

### T030 - API: JobEndpoints

Create `backend/src/HomeMaintenance.API/Endpoints/JobEndpoints.cs`
with the routes documented in `contracts/jobs.md` and the tick /
untick subset of `contracts/steps.md`:

| Verb | Route | Handler |
|---|---|---|
| POST | `/api/jobs` | CreateJob |
| GET | `/api/jobs?propertyId=&status=` | ListJobs |
| GET | `/api/jobs/{id}` | GetJob |
| POST | `/api/jobs/{id}/complete` | CompleteJob |
| POST | `/api/jobs/{id}/steps/{stepId}/tick` | TickStep |
| POST | `/api/jobs/{id}/steps/{stepId}/untick` | UntickStep |

All grouped under `MapGroup("/api/jobs").RequireAuthorization()`.

Register handlers in `Application.DependencyInjection.AddApplication`
and add `app.MapJobEndpoints();` to `Program.cs`.

### T031 - Integration tests

In `backend/tests/HomeMaintenance.Integration.Tests/Jobs/`:

- `Create_Job_AgainstOwnedProperty_Returns201_WithSteps`
- `Create_Job_AgainstOtherOwnersProperty_Returns404`
- `Create_Job_EmptyName_Returns400`
- `Create_Job_StepDescriptionTooLong_Returns400`
- `Get_Job_OwnedByCaller_ReturnsDetailWithSteps`
- `Get_Job_OwnedByOther_Returns404`
- `List_Jobs_FilteredByPropertyId_ReturnsOnlyMatching`
- `Tick_Step_SetsCompletedAt`
- `Untick_Step_ClearsCompletedAt`
- `Tick_Step_OnCompletedJob_Returns400_business_rule`
- `Complete_Job_AllStepsTicked_Transitions`
- `Complete_Job_WithIncompleteStep_Returns400_steps_incomplete`
- `Complete_Job_WithZeroSteps_Returns400_job_has_no_steps`
- `Complete_Job_AlreadyCompleted_Returns400_job_already_completed`
- `Audit_JobCompleted_AppearsInLog`
- `Anonymous_Job_Endpoints_All_Return401` (parameterised)

Use `dev-alice` + `dev-bob` with shared Testcontainers MongoDB.

## Test strategy

- Unit: Job aggregate is the bulk of WP05's tests (25+). Handler
  tests cover the wiring; Domain tests cover the rules.
- Integration: full HTTP round-trips for each endpoint, with explicit
  cross-owner and audit assertions.

## Definition of Done

- [ ] All six endpoints respond per `contracts/`.
- [ ] All Job lifecycle invariants enforced.
- [ ] Audit log shows `job.created`, `step.ticked`, `step.unticked`,
      `job.completed` events.
- [ ] CI green.

## Risks and non-obvious bits

- The handler-vs-aggregate split for "mutation rejected on completed
  Job": prefer handler-side short-circuits (load job, check status,
  return BusinessRuleError) so the API maps to 400 cleanly. The
  aggregate's `EnsureActive` is defence-in-depth - a bug in a handler
  that forgets to check status fails fast inside the aggregate
  instead of silently corrupting state.
- `ReplaceOneAsync` on the full document is acceptable at Slice 1
  scale. If a concurrent edit race becomes painful, swap to
  `$set` / `$pull` per the note in `research.md` R2.
- The integration tests need a fresh Mongo state between tests;
  reuse the `ApiFactory` collection-drop fixture pattern from
  WP01/WP03.
- `DateOnly` BSON serialisation: MongoDB.Driver 3.x ships a
  serialiser; verify with a round-trip test on first run.

## Next command

```
polaris implement WP05 --base WP02
```

## Activity Log

- 2026-05-17T11:39:17Z – unknown – shell_pid=60117 – lane=done – Merged via PR #12
- 2026-05-17T13:07:52Z – unknown – shell_pid=60117 – lane=done – Merged via PR #12
