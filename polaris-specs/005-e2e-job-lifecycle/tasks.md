# Tasks: 005-e2e-job-lifecycle

## Subtask List

| ID | WP | Description | Parallel |
|----|----|-------------|----------|
| T001 | WP01 | Add `JsonStringEnumConverter` to API http json options in Program.cs | |
| T002 | WP01 | Fix integration-test deserialization for string enums; run `dotnet test` | |
| T003 | WP01 | Verify via curl: job endpoints return `"status": "Active"` | |
| T004 | WP02 | Add `createJobViaApi` helper to `e2e/helpers/setup.ts` | |
| T005 | WP02 | Write WP06-1: create job (no steps) -> navigates to detail | [P] |
| T006 | WP02 | Write WP06-2: create job with steps -> 3 unchecked rows, Complete disabled | [P] |
| T007 | WP02 | Write WP06-3: tick a step -> strikethrough, button still disabled | [P] |
| T008 | WP02 | Write WP06-4: tick all + complete -> read-only state | [P] |
| T009 | WP02 | Write WP06-5: job card shows step count + Active badge | [P] |
| T010 | WP02 | Write WP06-6: Back to property link | [P] |

## Work Packages

### WP01 --- JobStatus serialization fix (backend)

**Goal**: API returns `"status": "Active"` / `"Completed"` instead of `0`/`1`,
matching the frontend `JobStatus` type. One-line config change + test updates.

**Subtasks**: T001-T003

**Dependencies**: none

**Implementation sketch**:
- `builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()))` in Program.cs
- Integration tests use `ReadFromJsonAsync<JobDetailDto>` with default options,
  which cannot read string enums -- give the tests a shared
  `JsonSerializerOptions` with the converter (or `JsonSerializerDefaults.Web` + converter)
- `dotnet test` green; curl smoke check on `/api/jobs`

**DoD**: backend tests pass; `"status": "Active"` visible in API responses;
existing e2e suites still pass (they don't assert status text today).

### WP02 --- `frontend/e2e/wp06-job-lifecycle.spec.ts` (helper + 6 tests)

**Goal**: Job lifecycle e2e coverage per issue #47. Requires WP01 (WP06-4
and WP06-5 assert completed state and the "Active" badge).

**Subtasks**: T004 first (helper used by WP06-3..6), then T005-T010.

**Dependencies**: WP01

**Implementation sketch**:
- `createJobViaApi(token, propertyId, name, steps = [])` -> POST /api/jobs
  (`steps` is `[Required]` -- always send an array)
- WP06-1/2 exercise the create form (`#job-name`, `#job-steps`, "Create job")
- WP06-3..6 seed via API; locators in plan.md (all verified against components)

**DoD**: `npx playwright test` -> full suite passes (15 existing + 6 new)

## Parallelization

WP01 -> WP02 strictly sequential (WP02 asserts behavior WP01 unlocks).
Within WP02 the 6 tests are independent; write in one pass.

## MVP Scope

Both WPs. WP01 alone fixes a visible production bug; WP02 delivers issue #47.

## Next Commands

```bash
polaris implement WP01
polaris implement WP02 --base WP01
```
