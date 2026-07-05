---
work_package_id: WP01
title: JobStatus enum serialization fix
lane: "doing"
dependencies: []
base_branch: main
base_commit: 5673f9656a447a1108f2b23003feacec70be7cc2
created_at: '2026-07-05T09:14:21.752563+00:00'
subtasks: [T001, T002, T003]
test_status: required
test_file: backend/tests/HomeMaintenance.Integration.Tests/Jobs/JobEndpointsTests.cs
domain: api-design
shell_pid: "18825"
---

# WP01 - JobStatus enum serialization fix

## Objective

Make the API serialize enums as strings so job endpoints return
`"status": "Active"` / `"Completed"` instead of `0`/`1`. This matches the
frontend contract (`JobStatus = "Active" | "Completed"` in
`frontend/src/lib/api-client.ts`) and unblocks the job lifecycle e2e tests
(WP02): today the job card badge renders "0" and a completed job never
shows "Completed on ..." nor locks its checklist.

## Context

- Verified against the live API: `POST /api/jobs` returns `"status": 0`.
- Frontend components comparing status strings: `JobCard`
  (`job.status === "Completed"`, badge text `{job.status}`), `JobHeader`,
  `CompleteJobButton`, `jobs/[id]/page.tsx`.
- `JobStatus` is the only enum in response DTOs. `ScheduleDefinitionDto.Unit`
  is already a plain string, so job-definition endpoints are unaffected.
- Requests contain no enum-typed fields, so deserialization of existing
  payloads is unaffected (the converter additionally allows string enums in
  requests, which is harmless).

## Subtasks

### T001 --- Configure string enum serialization

In `backend/src/HomeMaintenance.API/Program.cs`, near the other
`builder.Services` registrations:

```csharp
builder.Services.ConfigureHttpJsonOptions(o =>
    o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
```

Add `using System.Text.Json.Serialization;` if needed. Minimal APIs read
these options for both request and response JSON.

### T002 --- Fix integration-test deserialization

`backend/tests/HomeMaintenance.Integration.Tests/Jobs/JobEndpointsTests.cs`
uses `resp.Content.ReadFromJsonAsync<JobDetailDto>()` with default options.
Default options cannot parse `"Active"` into the `JobStatus` enum, so these
calls will throw after T001. Add a shared options instance to the test
class/fixture:

```csharp
private static readonly JsonSerializerOptions Json =
    new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };
```

and pass it: `ReadFromJsonAsync<JobDetailDto>(Json)`. Check the whole
integration test project for other `ReadFromJsonAsync` calls on DTOs that
contain `JobStatus` (job summaries/lists) and update those too.

### T003 --- Verify end-to-end

```bash
dotnet test backend
# with the local stack running:
# create a property + job with a dev-stub token, then:
curl -s http://localhost:5000/api/jobs/{id} -H "Authorization: Bearer dev-..." | grep '"status"'
# expect: "status": "Active"
```

Also run the existing Playwright suites (`cd frontend && npx playwright test`)
-- they do not assert status text today, so they must stay green.

## Definition of Done

- [ ] `dotnet test` passes
- [ ] Job endpoints return `"status": "Active"` / `"Completed"` (curl check)
- [ ] Existing Playwright suites still pass (15/15)
- [ ] No frontend changes needed (types already expect strings)
- [ ] PR reviewed and merged to main

## Risks

- **Wider serialization impact**: the converter applies to all enums in
  http JSON. Audit response DTOs for other enums (expected: none besides
  `JobStatus`). If one is found, confirm its consumers expect strings.
- **OpenAPI schema drift**: if the OpenAPI doc is consumed anywhere, enum
  values change from integers to strings. No known consumers.

## Run Command

```bash
polaris implement WP01
```
