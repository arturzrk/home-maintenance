---
work_package_id: WP04
title: 'API: endpoints + JobDto update'
lane: "testing"
dependencies: []
subtasks: [T022, T023, T024, T025, T026]
assignee: ''
agent: ''
shell_pid: ''
test_status: required
test_file: tests/e2e/WP04-api-endpoints-jobdto-update.e2e.js
review_status: ''
reviewed_by: ''
history:
- timestamp: '2026-05-29T00:00:00Z'
  lane: planned
  agent: system
  action: Prompt generated via /polaris.tasks
domain: backend-logic
---

# WP04 - API: endpoints + JobDto update

## Objective

Wire the Application handlers to HTTP via Minimal API endpoints, add the
`generate-next` endpoint, expose `jobDefinitionId` on the existing Job
endpoints, and add integration tests for all five new endpoints plus the
updated Job response shape. No frontend changes in this WP.

## Inputs

- Spec: `polaris-specs/002-recurring-jobs/spec.md` (FR-121..FR-123, SC-105, SC-106)
- Contracts: `polaris-specs/002-recurring-jobs/contracts/job-definitions.md` (all endpoint shapes)
- Plan: `polaris-specs/002-recurring-jobs/plan.md` (Phase 4 API section)
- Existing: `backend/src/HomeMaintenance.API/Endpoints/JobEndpoints.cs` (pattern reference)
- Existing: `backend/src/HomeMaintenance.API/Program.cs` (where `app.Map*()` is called)
- WP03 output: all Infrastructure services registered in DI

## Subtasks

### T022 - JobDefinitionEndpoints: POST + GET list + GET by id + PATCH

Create `backend/src/HomeMaintenance.API/Endpoints/JobDefinitionEndpoints.cs`.

Register as an extension: `public static IEndpointRouteBuilder MapJobDefinitionEndpoints(this IEndpointRouteBuilder app)`.

**POST `/api/job-definitions`**
- Request: `CreateJobDefinitionApiRequest` (propertyId, name, schedule, stepTemplates)
  with `[Required]` and `[StringLength]` annotations for MiniValidator.
- Validate with `MiniValidator.TryValidate`. Return 400 on failure.
- Resolve `OwnerId` via `IIdentityProvider`.
- Map to `CreateJobDefinitionCommand`; dispatch to handler.
- On success: 201 + `JobDefinitionDto` + `Location: /api/job-definitions/{id}`.
- On `NotFoundError`: 404. On `ValidationError`: 400.

**GET `/api/job-definitions`**
- Optional query param: `propertyId`.
- Dispatch `ListJobDefinitionsQuery`; return 200 + `JobDefinitionDto[]`.

**GET `/api/job-definitions/{id}`**
- Dispatch `GetJobDefinitionQuery`; return 200 or 404.

**PATCH `/api/job-definitions/{id}`**
- Request: `UpdateJobDefinitionApiRequest` (all optional fields from contract).
- At least one field must be non-null (validate in handler, return 400 if all null).
- Map to `UpdateJobDefinitionCommand`; dispatch.
- Return 200 + updated `JobDefinitionDto` or appropriate error.

All endpoints require authentication. Use `RequireAuthorization()` on the route group
(same pattern as existing `JobEndpoints`).

Call `app.MapJobDefinitionEndpoints()` in `Program.cs`.

### T023 - POST /api/job-definitions/{id}/generate-next endpoint

Add to `JobDefinitionEndpoints.cs`:

**POST `/api/job-definitions/{id}/generate-next`**
- Empty request body (no DTO needed; route param provides `id`).
- Dispatch `GenerateNextOccurrenceCommand`.
- On success (`JobDetailDto`): 201 + body + `Location: /api/jobs/{jobId}`.
- On `BusinessRuleError("next_occurrence_already_exists")`: 400 + problem-details with `code: "next_occurrence_already_exists"`.
- On `NotFoundError`: 404.
- On `BusinessRuleError("no_future_occurrence")`: 400 with appropriate code.

Return the existing `JobDetailDto` mapped to the response shape (use the same
Result -> HTTP translator used by other endpoints).

### T024 - Add jobDefinitionId to Job endpoint responses

Update the existing `JobEndpoints.cs` response mapping to include `jobDefinitionId`.

The field is already present on `JobSummaryDto` and `JobDetailDto` (added in WP02 T009).
The only change needed is ensuring the JSON serialization includes it (it will, as
all DTO properties are serialized by default). Verify by reading the mapping code.

Add `jobDefinitionId` to the response body schema comments/documentation if any
inline documentation exists. No routing changes needed.

### T025 - Integration tests: CRUD endpoints

Create `backend/tests/HomeMaintenance.Integration.Tests/JobDefinitions/JobDefinitionEndpointsTests.cs`.

Use the existing `WebApplicationFactory` + `MongoDbFixture` test harness.

Required tests:
- `POST_ValidRequest_Returns201WithDefinitionAndGeneratesJobs`: create definition; assert 201 + `JobDefinitionDto` in body; assert at least one job was generated (query `GET /api/jobs`).
- `POST_EmptyName_Returns400`
- `POST_MultiplierZero_Returns400`
- `POST_CrossPropertyOwnership_Returns404`: use alice's property with bob's token.
- `GET_List_Returns200WithOwnedDefinitions`
- `GET_List_FilteredByPropertyId_Returns200Subset`
- `GET_ById_Owned_Returns200`
- `GET_ById_CrossOwner_Returns404`
- `PATCH_Rename_Returns200WithUpdatedName`
- `PATCH_ChangeSchedule_Returns200AndSchedulePersisted`
- `PATCH_AddStepTemplate_Returns200AndStepPresent`
- `PATCH_CrossOwner_Returns404`
- `POST_Anonymous_Returns401`
- `GET_Anonymous_Returns401`
- `PATCH_Anonymous_Returns401`

### T026 - Integration tests: generate-next endpoint

Add to `JobDefinitionEndpointsTests.cs` (or a separate file if preferred):

Required tests:
- `GenerateNext_Success_Returns201WithJobDtoAndLocationHeader`: verify body is a `JobDto` with `jobDefinitionId` set; verify `Location` header points to `/api/jobs/{id}`.
- `GenerateNext_DuplicateOccurrence_Returns400WithCode`: call generate-next twice on a definition with only one occurrence in range; second call returns 400 with `code: "next_occurrence_already_exists"`.
- `GenerateNext_CrossOwner_Returns404`
- `GenerateNext_Anonymous_Returns401`

## Test Strategy

- Integration tests use the existing `WebApplicationFactory` with a real MongoDB
  Testcontainers instance.
- Create two users (alice, bob) via test helpers for cross-owner assertions.
- Use `HttpClient` with a valid stub auth header (same pattern as Slice 1 tests).
- Assert HTTP status codes, response body fields, and `Location` header values.

## Definition of Done

- [ ] `JobDefinitionEndpoints.cs` created with all 5 endpoint handlers.
- [ ] `app.MapJobDefinitionEndpoints()` called in `Program.cs`.
- [ ] All endpoints return correct status codes per contract.
- [ ] `GET /api/jobs` and `GET /api/jobs/{id}` responses include `jobDefinitionId`.
- [ ] All integration tests pass.
- [ ] `dotnet test` is green on HomeMaintenance.Integration.Tests.
- [ ] Anonymous requests to all new endpoints return 401.

## Risks

- **Result -> HTTP translation**: use the existing `TypedResultTranslator` or
  equivalent helper. Do not duplicate switch/match logic across endpoints.
- **Location header format**: ensure the `Location` value for generate-next points
  to `/api/jobs/{id}` (not `/api/job-definitions/...`).
- **MiniValidator**: confirm it is already referenced in the API project; if not, add
  the `MiniValidation` NuGet package.

## Run Command

```bash
polaris implement WP04 --base WP03
```

## Activity Log

- 2026-06-06T18:43:15Z – unknown – lane=doing – Resume: implementation already complete, moving through required lane transitions
- 2026-06-06T18:43:16Z – unknown – lane=testing – dotnet test green: 154/154 Unit.Tests + 150/150 Integration.Tests (19 new JobDefinition endpoint tests)
