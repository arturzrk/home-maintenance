# Tasks: 002-recurring-jobs

Decomposition of the plan into seven work packages (43 subtasks).
Each WP is independently mergeable. CI must stay green on every merge.

## Overview

| WP | Title | Subtasks | Domain | Depends on |
|---|---|---|---|---|
| WP01 | Domain: ScheduleDefinition + JobDefinition + Job extension | 7 (T001-T007) | backend-logic | - |
| WP02 | Application: interfaces, use cases, generation logic | 7 (T008-T014) | backend-logic | WP01 |
| WP03 | Infrastructure: repositories + BackgroundService | 7 (T015-T021) | backend-logic | WP02 |
| WP04 | API: endpoints + JobDto update | 5 (T022-T026) | backend-logic | WP03 |
| WP05 | Frontend: JobDefinition create/list on Property page | 6 (T027-T032) | frontend-craft | WP04 |
| WP06 | Frontend: JobDefinition detail page + Generate Next | 6 (T033-T038) | frontend-craft | WP05 |
| WP07 | Hardening: acceptance + cross-owner + perf tests | 5 (T039-T043) | testing-specialist | WP06 |

Parallelisation: WP05 and WP06 are strictly sequential (WP06 builds on WP05 components).
WP01 T001 and T002 are parallel-safe. WP02 T008 and T009 are parallel-safe.

## WP01 - Domain: ScheduleDefinition + JobDefinition + Job extension

New Domain entities and value objects. No Application or Infrastructure changes yet.

- [ ] T001 [P] `CadenceUnit` enum (`Day | Week | Month | Year`) in
      `backend/src/HomeMaintenance.Domain/JobDefinitions/CadenceUnit.cs`.
- [ ] T002 [P] `ScheduleDefinition` sealed record in
      `backend/src/HomeMaintenance.Domain/JobDefinitions/ScheduleDefinition.cs`
      with `OccurrencesInRange(DateOnly from, DateOnly to)` method.
      Constructor validates Multiplier >= 1 and EndDate > StartDate.
- [ ] T003 `StepTemplate` sealed class in
      `backend/src/HomeMaintenance.Domain/JobDefinitions/StepTemplate.cs`
      with `Create` factory and `EditDescription` mutation.
- [ ] T004 `JobDefinition` aggregate root in
      `backend/src/HomeMaintenance.Domain/JobDefinitions/JobDefinition.cs`
      with `Create`, `Hydrate`, `Rename`, `UpdateSchedule`, and all four
      step-template mutation methods.
- [ ] T005 Extend `Job` aggregate:
      add nullable `JobDefinitionId` property; extend `Create` and
      `Hydrate` to accept optional `jobDefinitionId`. `JobDefinitionId`
      is immutable after creation.
- [ ] T006 Unit tests for `ScheduleDefinition` in
      `backend/tests/HomeMaintenance.Unit.Tests/Domain/JobDefinitions/ScheduleDefinitionTests.cs`:
      all four cadence units, multiplier > 1, month-end clamping (Jan 31),
      leap-year (Feb 29 + 12 months), EndDate cutoff, start date in past,
      empty range, horizon boundary inclusion.
- [ ] T007 Unit tests for `JobDefinition` in
      `backend/tests/HomeMaintenance.Unit.Tests/Domain/JobDefinitions/JobDefinitionTests.cs`:
      Create invariants, Rename validation, UpdateSchedule, all four
      StepTemplate mutations (add/remove/reorder/edit) and their
      outcome enums.

## WP02 - Application: interfaces, use cases, generation logic

All Application-layer code. No Infrastructure or API code yet.

- [x] T008 [P] `IJobDefinitionRepository` interface in
      `backend/src/HomeMaintenance.Application/Common/Interfaces/IJobDefinitionRepository.cs`
      and `IDateTimeProvider` in the same folder.
- [x] T009 [P] `JobDefinitionDto`, `ScheduleDefinitionDto`, `StepTemplateDto`
      records in `backend/src/HomeMaintenance.Application/JobDefinitions/Dto/JobDefinitionDtos.cs`
      plus `Mapping.cs` extension methods. Add nullable `JobDefinitionId`
      to existing `JobDto` and update `Job` -> `JobDto` mapping.
- [x] T010 `JobGenerationService` in
      `backend/src/HomeMaintenance.Application/JobDefinitions/JobGenerationService.cs`:
      `GenerateForDefinition(JobDefinition, DateOnly today)` calls
      `OccurrencesInRange`, checks `IJobRepository` for duplicates,
      creates and persists jobs, emits `job.generated` audit event.
      Takes `IJobRepository`, `IJobDefinitionRepository`, `IAuditLog`.
- [x] T011 `CreateJobDefinition` command + handler in
      `backend/src/HomeMaintenance.Application/JobDefinitions/Commands/CreateJobDefinition.cs`:
      verify property ownership via `IPropertyRepository`, create aggregate,
      persist, call `JobGenerationService.GenerateForDefinition` for the
      inline initial generation, emit `job_definition.created` audit event.
      Unit tests: success, validation failure, cross-property 404.
- [x] T012 `ListJobDefinitions` and `GetJobDefinition` query handlers in
      `backend/src/HomeMaintenance.Application/JobDefinitions/Queries/`.
      List scoped to `CurrentOwner`, optional `propertyId` filter.
      GetJobDefinition returns `NotFoundError` on miss or cross-owner.
      Unit tests: success, not-found, cross-owner.
- [x] T013 `UpdateJobDefinition` command + handler in
      `backend/src/HomeMaintenance.Application/JobDefinitions/Commands/UpdateJobDefinition.cs`:
      accepts optional name, schedule, and step-template mutations (add,
      remove, reorder, edit); applies each present mutation; persists once;
      emits appropriate audit events per field changed.
      Unit tests: each mutation path, validation failures, not-found.
- [x] T014 `GenerateNextOccurrence` command + handler in
      `backend/src/HomeMaintenance.Application/JobDefinitions/Commands/GenerateNextOccurrence.cs`:
      load definition, find next occurrence date via `IDateTimeProvider` +
      `LatestGeneratedJobDueDateAsync`, reject if already exists
      (`BusinessRuleError("next_occurrence_already_exists", ...)`),
      create and persist job, emit `job.generated` audit.
      Unit tests: success, duplicate rejection, not-found, no prior jobs case.

## WP03 - Infrastructure: repositories + BackgroundService

MongoDB persistence and the scheduled generator. Depends on WP02 interfaces.

- [ ] T015 `JobDefinitionDocument` in
      `backend/src/HomeMaintenance.Infrastructure/Persistence/Documents/JobDefinitionDocument.cs`
      and `JobDefinitionRepository` in
      `backend/src/HomeMaintenance.Infrastructure/Persistence/JobDefinitionRepository.cs`
      implementing `IJobDefinitionRepository`.
      Bootstrap `job_definitions` collection indexes: `{ ownerId: 1 }`,
      `{ ownerId: 1, propertyId: 1 }` via the existing `MongoIndexInitializer`.
- [ ] T016 Extend `JobDocument` with nullable `jobDefinitionId` field.
      Add sparse index `{ jobDefinitionId: 1 }` on `jobs` collection.
      Implement `HasGeneratedJobForOccurrence` and
      `LatestGeneratedJobDueDateAsync` on `JobRepository` (queries on the
      sparse index). No migration needed -- missing field reads as null.
- [ ] T017 `SystemDateTimeProvider` in
      `backend/src/HomeMaintenance.Infrastructure/Scheduling/SystemDateTimeProvider.cs`
      implementing `IDateTimeProvider`. Returns
      `DateOnly.FromDateTime(DateTime.UtcNow)`. Registered as `Singleton`.
- [ ] T018 `JobGeneratorService` in
      `backend/src/HomeMaintenance.Infrastructure/Scheduling/JobGeneratorService.cs`
      extending `BackgroundService`. Uses `PeriodicTimer` with 24-hour
      interval. Runs one immediate generation pass on startup before
      entering the periodic loop. Calls
      `JobGenerationService.GenerateForDefinition` for every definition
      returned by `IJobDefinitionRepository.ListAllActiveAsync`.
- [ ] T019 Register all new services in `Infrastructure.DependencyInjection`:
      `JobDefinitionRepository` (scoped), `SystemDateTimeProvider`
      (singleton), `JobGeneratorService` (hosted service),
      `JobGenerationService` (scoped, Application layer).
- [ ] T020 Integration tests for `JobDefinitionRepository` in
      `backend/tests/HomeMaintenance.Integration.Tests/JobDefinitions/JobDefinitionRepositoryTests.cs`:
      Add, Get (owned), Get (cross-owner returns null), List (by owner),
      List (filtered by propertyId), Update round-trips schedule and steps.
- [ ] T021 Integration tests for the scheduler in
      `backend/tests/HomeMaintenance.Integration.Tests/JobDefinitions/JobGeneratorServiceTests.cs`:
      inject `StubDateTimeProvider`; verify horizon generation for monthly
      cadence; verify second run produces no duplicates (idempotency);
      verify end-date cutoff stops generation; verify startup run fills gap.

## WP04 - API: endpoints + JobDto update

REST endpoints wired to Application handlers.

- [ ] T022 `JobDefinitionEndpoints` in
      `backend/src/HomeMaintenance.API/Endpoints/JobDefinitionEndpoints.cs`:
      POST `/api/job-definitions`, GET `/api/job-definitions`,
      GET `/api/job-definitions/{id}`, PATCH `/api/job-definitions/{id}`.
      All use MiniValidator for DTO validation; wire Result -> HTTP via
      existing translator. Register handlers in DI; call
      `app.MapJobDefinitionEndpoints()` in `Program.cs`.
- [ ] T023 Add `POST /api/job-definitions/{id}/generate-next` to
      `JobDefinitionEndpoints`. Returns 201 + `JobDto` on success,
      400 + `code: "next_occurrence_already_exists"` on duplicate.
      `Location` header points to `/api/jobs/{newJobId}`.
- [ ] T024 Add `jobDefinitionId` (nullable string) to `JobDto` and
      update `JobEndpoints` response mapping. Existing `GET /api/jobs`
      and `GET /api/jobs/{id}` now surface the field -- no endpoint
      routing changes needed.
- [ ] T025 Integration tests in
      `backend/tests/HomeMaintenance.Integration.Tests/JobDefinitions/JobDefinitionEndpointsTests.cs`:
      POST success (definition stored + jobs generated), POST validation
      (empty name, multiplier 0), cross-owner GET/PATCH -> 404,
      anonymous POST/GET/PATCH -> 401, PATCH step mutations round-trip,
      PATCH schedule change persisted.
- [ ] T026 Integration tests for generate-next in the same file:
      success (201 + correct JobDto + Location header), duplicate -> 400
      with correct error code, cross-owner -> 404, anonymous -> 401.

## WP05 - Frontend: JobDefinition create/list on Property page

Delivers US1 and US2 end-to-end in the browser.

- [ ] T027 Extend `frontend/src/lib/api-client.ts` with JobDefinition
      methods: `createJobDefinition(propertyId, body)`,
      `listJobDefinitions(propertyId?)`. Mirror existing patterns for
      auth header attachment.
- [ ] T028 Add `jobDefinitionId: string | null` to the `Job` type in
      `frontend/src/lib/types.ts` (or wherever types live). Update any
      type assertions that spread job objects.
- [ ] T029 Add "Recurring jobs" section to
      `frontend/src/app/properties/[id]/page.tsx` (Server Component):
      fetch `listJobDefinitions(propertyId)`, render a list of definition
      names with their schedule summary (e.g. "Every 3 months from Jun 1").
      Link each to `/job-definitions/[id]`.
- [ ] T030 `CreateJobDefinitionForm` client component at
      `frontend/src/app/properties/[id]/components/CreateJobDefinitionForm.tsx`:
      fields: name, schedule (unit dropdown + multiplier number input +
      startDate date picker, optional endDate), dynamic step rows (same
      pattern as Slice 1 CreateJobForm). On submit, POST to api-client,
      then `router.refresh()`.
- [ ] T031 In the job list on the Property page, display a small
      "Recurring" badge (or icon) on jobs where `jobDefinitionId != null`.
      One-shot jobs are visually unchanged.
- [ ] T032 Jest tests:
      `CreateJobDefinitionForm` -- renders fields, submit calls api-client
      with correct payload, validation errors shown to user;
      job list badge -- present when `jobDefinitionId` set, absent when null.

## WP06 - Frontend: JobDefinition detail page + Generate Next

Delivers US3 (background generation visibility), US4 (manual generate), US5 (edit).

- [ ] T033 Extend `frontend/src/lib/api-client.ts` with remaining
      JobDefinition methods: `getJobDefinition(id)`,
      `updateJobDefinition(id, body)`, `generateNextOccurrence(id)`.
- [ ] T034 `frontend/src/app/job-definitions/[id]/page.tsx` (Server
      Component): fetch definition, render name + schedule header and
      the StepTemplateList and GenerateNextButton client islands, plus
      a read-only list of already-generated jobs (fetched via
      `listJobs` filtered by... or just rendered from the definition page
      jobs data returned in the DTO if extended -- use `listJobs?definitionId=`
      query param, adding it to api-client).
- [ ] T035 `StepTemplateList` client component at
      `frontend/src/app/job-definitions/[id]/components/StepTemplateList.tsx`:
      add/remove/reorder (dnd-kit, reuse existing drag handle pattern from
      Slice 1) / edit description. Each mutation calls `updateJobDefinition`
      with the appropriate partial body and refreshes.
- [ ] T036 `GenerateNextButton` client component: POST to
      `generateNextOccurrence(id)`, on 201 navigate to the new job's page
      (`/jobs/{newJobId}`); on 400 (`next_occurrence_already_exists`)
      show inline error message; disabled while loading.
- [ ] T037 Inline name and schedule editing on the definition detail
      header: same in-place edit pattern as Slice 1 Job header. Name:
      click to edit, blur/Enter saves via `updateJobDefinition`. Schedule:
      a small "Edit schedule" affordance opens a form panel with unit,
      multiplier, startDate, endDate fields.
- [ ] T038 Jest tests: detail page renders definition data; StepTemplateList
      add step calls api-client with correct body; GenerateNextButton
      navigates on success and shows error on duplicate (mock 400 response).

## WP07 - Hardening: acceptance + cross-owner + perf tests

Final quality gate before Slice 2 is declared complete.

- [ ] T039 Cross-owner matrix for all 5 new endpoints in
      `backend/tests/HomeMaintenance.Integration.Tests/Acceptance/CrossOwnerMatrixTests.cs`
      (extend existing class or add a new parameterised region):
      GET /api/job-definitions/{id}, PATCH /api/job-definitions/{id},
      POST /api/job-definitions/{id}/generate-next, POST /api/job-definitions
      (with alice's propertyId), GET /api/job-definitions -- each as bob.
      All must return 404 with correlationId. (SC-105)
- [ ] T040 401 matrix for all 5 new endpoints in
      `backend/tests/HomeMaintenance.Integration.Tests/Acceptance/AnonymousMatrixTests.cs`
      (extend existing class): no token -> 401; malformed token -> 401.
      Assert body `code: "unauthorized"`. (SC-106)
- [ ] T041 Non-destructive edit tests in
      `backend/tests/HomeMaintenance.Integration.Tests/Acceptance/FrAcceptanceTests.cs`:
      `FR_104_EditJobDefinitionSteps_DoesNotModifyAlreadyGeneratedJob` --
      create definition with 2 step templates, generate a job (verify 2 steps),
      add a 3rd step template via PATCH, re-GET the generated job, assert
      still has 2 steps. (SC-104)
- [ ] T042 FR-named acceptance tests (add to `FrAcceptanceTests.cs`):
      `FR_113_SchedulerRunTwice_ProducesNoDuplicateJobs`,
      `FR_117_GenerateNext_RejectsIfOccurrenceAlreadyExists`,
      `FR_111_Scheduler_GeneratesOccurrencesWithinHorizon`,
      `FR_116_GenerateNext_UsesEarliestOccurrenceAfterLatestJob`.
- [ ] T043 Performance test in
      `backend/tests/HomeMaintenance.Integration.Tests/Performance/GenerateNextPerformanceTests.cs`
      (`[Trait("category","perf")]`, opt-in): warm-up run, then 100 iterations
      of generate-next on a definition with a fresh occurrence each time
      (advance stub clock). Assert p95 < 500ms. (SC-103)

## Dependencies summary

```
WP01 -> WP02 -> WP03 -> WP04 -> WP05 -> WP06 -> WP07
```

## MVP scope

WP01-WP04 delivers all P1 user stories (create definition, background generation,
see upcoming jobs in list). WP05-WP06 adds the frontend. WP07 hardens for release.
