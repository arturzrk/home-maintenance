---
work_package_id: WP07
title: 'Hardening: acceptance + cross-owner + perf tests'
lane: "doing"
dependencies: []
base_branch: main
base_commit: 92a46ab49e5a1ffd42b999363c989f87425e04e0
created_at: '2026-06-08T19:44:04.574600+00:00'
subtasks: [T039, T040, T041, T042, T043]
assignee: ''
agent: ''
shell_pid: "73197"
test_status: required
test_file: tests/e2e/WP07-hardening-acceptance-cross-owner-perf-tests.e2e.js
review_status: ''
reviewed_by: ''
history:
- timestamp: '2026-05-29T00:00:00Z'
  lane: planned
  agent: system
  action: Prompt generated via /polaris.tasks
domain: testing-specialist
---

# WP07 - Hardening: acceptance + cross-owner + perf tests

## Objective

Final quality gate before Slice 2 is declared complete. Add the cross-owner
security matrix (SC-105), the anonymous 401 matrix (SC-106), FR-named acceptance
tests for idempotency and non-destructive edits (SC-102, SC-104), and the
performance test for generate-next (SC-103). All tests run against real
infrastructure (Testcontainers MongoDB, real HTTP stack).

## Inputs

- Spec: `polaris-specs/002-recurring-jobs/spec.md` (SC-101..SC-106, FR-113, FR-117, FR-111, FR-116, FR-104)
- Contracts: `polaris-specs/002-recurring-jobs/contracts/job-definitions.md`
- Existing: `backend/tests/HomeMaintenance.Integration.Tests/Acceptance/CrossOwnerMatrixTests.cs`
- Existing: `backend/tests/HomeMaintenance.Integration.Tests/Acceptance/AnonymousMatrixTests.cs`
- Existing: `backend/tests/HomeMaintenance.Integration.Tests/Acceptance/FrAcceptanceTests.cs`
- WP06 output: all endpoints and frontend components complete

## Subtasks

### T039 - Cross-owner matrix for all 5 new endpoints

Extend `backend/tests/HomeMaintenance.Integration.Tests/Acceptance/CrossOwnerMatrixTests.cs`
with parameterised tests covering all new endpoint + method combinations.

Matrix: for each endpoint, create a resource as alice, then attempt the
action as bob, assert 404 with `correlationId` in the response body.

Endpoints to cover (method, route):
1. `GET /api/job-definitions/{id}` (alice's definition id, bob's token)
2. `PATCH /api/job-definitions/{id}` (alice's definition id, bob's token)
3. `POST /api/job-definitions/{id}/generate-next` (alice's definition id, bob's token)
4. `POST /api/job-definitions` with `propertyId` = alice's property, bob's token
5. `GET /api/job-definitions` as bob - does NOT return alice's definitions (assert count 0 or only bob's)

Use `[Theory]` + `[MemberData]` or `[InlineData]` to keep the matrix compact.
Assert: status 404 + response body contains `"correlationId"` field.

Reference: SC-105 from spec.md.

### T040 - 401 matrix for all 5 new endpoints

Extend `backend/tests/HomeMaintenance.Integration.Tests/Acceptance/AnonymousMatrixTests.cs`.

For each of the 5 new endpoints, test two cases:
1. No `Authorization` header -> 401 with `code: "unauthorized"` in body.
2. Malformed token (e.g. `Authorization: Bearer invalid-token`) -> 401 with `code: "unauthorized"`.

Endpoints:
1. `POST /api/job-definitions`
2. `GET /api/job-definitions`
3. `GET /api/job-definitions/{id}`
4. `PATCH /api/job-definitions/{id}`
5. `POST /api/job-definitions/{id}/generate-next`

Use `[Theory]` with the endpoint list as data source. Assert: status 401 +
`code: "unauthorized"` in body.

Reference: SC-106 from spec.md.

### T041 - Non-destructive edit acceptance test (SC-104)

Add to `backend/tests/HomeMaintenance.Integration.Tests/Acceptance/FrAcceptanceTests.cs`:

```
FR_104_EditJobDefinitionSteps_DoesNotModifyAlreadyGeneratedJob:
1. Create a JobDefinition with 2 step templates.
2. Assert at least one Job was generated (inline generation on create).
3. Record the generated job's id and its step count (should be 2).
4. PATCH the definition to add a 3rd step template.
5. GET the generated job (by id from step 3).
6. Assert the job still has exactly 2 steps (the snapshot was not modified).
7. Call generate-next to produce the next job.
8. GET the new job.
9. Assert the new job has 3 steps (snapshot from updated template).
```

This test covers FR-104 and SC-104.

### T042 - FR-named acceptance tests for scheduler and generate-next

Add to `backend/tests/HomeMaintenance.Integration.Tests/Acceptance/FrAcceptanceTests.cs`:

**`FR_113_SchedulerRunTwice_ProducesNoDuplicateJobs`**:
1. Create a monthly JobDefinition (startDate = today).
2. Run `JobGenerationService.GenerateForDefinition` twice with the same `today` date.
3. Count jobs with `jobDefinitionId = definition.Id`.
4. Assert count == count after first run (no duplicates added).

**`FR_117_GenerateNext_RejectsIfOccurrenceAlreadyExists`**:
1. Create a JobDefinition; let inline generation create the first job.
2. Manually determine the "next occurrence" date.
3. Create a job directly with `dueDate = nextOccurrence` and `jobDefinitionId = definition.Id` (simulate existing).
4. Call `POST /api/job-definitions/{id}/generate-next`.
5. Assert 400 with `code: "next_occurrence_already_exists"`.

**`FR_111_Scheduler_GeneratesOccurrencesWithinHorizon`**:
1. Create a monthly JobDefinition with startDate = today.
2. Run generation with a stub date provider set to today.
3. Assert jobs exist for today, today+1month, today+2months, today+3months.
4. Assert NO job exists for today+4months (outside 3-month horizon).

**`FR_116_GenerateNext_UsesEarliestOccurrenceAfterLatestJob`**:
1. Create a monthly JobDefinition with startDate = today.
2. Let inline generation create jobs through today+3months.
3. Call `POST /api/job-definitions/{id}/generate-next`.
4. Assert the new job's dueDate = today+4months (first occurrence after the last generated).

For tests that need `JobGenerationService` to run directly (not via HTTP), inject
it through the `WebApplicationFactory`'s service provider or call the endpoint
and use the HTTP API exclusively.

### T043 - Performance test for generate-next (SC-103)

Create `backend/tests/HomeMaintenance.Integration.Tests/Performance/GenerateNextPerformanceTests.cs`.

Mark with `[Trait("category", "perf")]` so it can be run opt-in:
```bash
dotnet test --filter "category=perf"
```

Test method `GenerateNext_P95Under500ms`:
1. Create a monthly JobDefinition.
2. Warm-up: call generate-next once; discard timing.
3. Loop 100 iterations:
   a. Advance the stub clock by 1 month (so each call has a new "next occurrence").
   b. Call `POST /api/job-definitions/{id}/generate-next` via `HttpClient`.
   c. Record round-trip time in milliseconds.
4. Sort timings; assert `timings[94]` (p95) < 500ms.

Use `Stopwatch` for timing. Assert after all iterations are complete.

Note: this test requires the stub date provider to be injectable via the
`WebApplicationFactory`'s service replacement. Check if a test-configurable
`IDateTimeProvider` override is already wired up (e.g. via `WithWebHostBuilder`);
if not, add one in the test project's `CustomWebApplicationFactory`.

## Test Strategy

- All tests use the existing `WebApplicationFactory` + Testcontainers MongoDB fixture.
- Cross-owner and 401 tests use real HTTP calls with controlled tokens (same as Slice 1 acceptance tests).
- Acceptance tests (T041, T042) exercise the full stack: HTTP -> Application -> Infrastructure -> MongoDB.
- Perf test (T043) is opt-in via trait filter; it is NOT run in the default CI test suite.

## Definition of Done

- [ ] CrossOwnerMatrixTests covers all 5 new endpoints, all returning 404.
- [ ] AnonymousMatrixTests covers all 5 new endpoints with both no-token and malformed-token cases, all returning 401.
- [ ] FR_104 test passes: generated job step count unchanged after template edit.
- [ ] FR_113, FR_117, FR_111, FR_116 tests all pass.
- [ ] Perf test present and runnable with `--filter "category=perf"` (p95 assertion may be skipped in CI).
- [ ] `dotnet test` (excluding perf trait) is green on HomeMaintenance.Integration.Tests.
- [ ] No cross-owner data leakage in any scenario.

## Risks

- **StubDateTimeProvider in WebApplicationFactory**: replacing `IDateTimeProvider`
  in the ASP.NET Core DI container for tests requires `WithWebHostBuilder` +
  `services.RemoveAll<IDateTimeProvider>()` + `services.AddSingleton(stub)`.
  If not already set up, add a `TestWebApplicationFactory` subclass.
- **Perf test flakiness**: the p95 < 500ms assertion may be flaky in CI due to
  container startup overhead and shared resources. Mark it opt-in (`[Trait]`) and
  skip it in the standard CI pipeline. Run it locally or in a dedicated perf run.
- **FR_116 timing**: "next occurrence after latest job" depends on the scheduler
  having generated all occurrences in the 3-month window first. Ensure the test
  explicitly runs a generation pass before calling generate-next.

## Run Command

```bash
polaris implement WP07 --base WP06
```
