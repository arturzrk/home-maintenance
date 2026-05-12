# Contract: Jobs

Base: `/api/jobs`

## POST /api/jobs

Create a one-shot Job under a Property the caller owns.

**Request body**:
```json
{
  "propertyId": "01J9P7MF1XAN70VW8RHP3Y6E13",
  "name": "Service boiler",
  "dueDate": "2026-06-01",
  "steps": [
    { "description": "Shut off gas" },
    { "description": "Drain system" },
    { "description": "Replace filter" }
  ]
}
```

| Field | Type | Required | Constraints |
|---|---|---|---|
| `propertyId` | string | yes | Must reference a Property owned by caller |
| `name` | string | yes | 1..200 chars, trimmed |
| `dueDate` | string \| null | no | `YYYY-MM-DD` (calendar date, no timezone) |
| `steps` | array | yes | May be empty; each step description 1..500 chars |
| `steps[].description` | string | yes (if step present) | 1..500 chars, trimmed |

**201 Created**: returns `JobDetail` (see below). `Location: /api/jobs/{id}`.

**400 ValidationError**: name/description/dueDate validation fails.
**401 Unauthorized**.
**404 NotFound**: `propertyId` is unknown OR owned by another user (no leak).

**Audit**: `job.created` with `target=job:{id}`, payload
`{propertyId, name, dueDate, stepCount}`.

## GET /api/jobs

List Jobs owned by the caller. Returns summaries (no embedded steps).

**Query**:
- `propertyId` (optional): restrict to one Property.
- `status` (optional): `active` | `completed`.

**200 OK**:
```json
{
  "jobs": [
    {
      "id": "...",
      "propertyId": "...",
      "name": "Service boiler",
      "dueDate": "2026-06-01",
      "status": "Active",
      "completedAt": null,
      "stepCount": 3,
      "completedStepCount": 1
    }
  ]
}
```

**401 Unauthorized**.

## GET /api/jobs/{id}

Read a single Job with full embedded steps.

**200 OK**:
```json
{
  "id": "...",
  "propertyId": "...",
  "name": "Service boiler",
  "dueDate": "2026-06-01",
  "status": "Active",
  "completedAt": null,
  "steps": [
    {
      "id": "...",
      "order": 0,
      "description": "Shut off gas",
      "isCompleted": true,
      "completedAt": "2026-05-12T14:21:09Z"
    },
    {
      "id": "...",
      "order": 1,
      "description": "Drain system",
      "isCompleted": false,
      "completedAt": null
    }
  ]
}
```

**404 NotFound** (unknown or not owned).

## PATCH /api/jobs/{id}

Rename Job and/or update due date. Body fields are independently optional;
omitting a field leaves it unchanged.

**Request body**:
```json
{
  "name": "Service boiler (Spring 2026)",
  "dueDate": "2026-05-30"
}
```

**200 OK**: returns updated `JobDetail`.
**400 ValidationError** (name length).
**404 NotFound**.
**409 Conflict** (returned as 400 with `code: "business_rule"`,
`detail: "Job is completed; cannot modify."`) if Status is Completed.

**Audit**: `job.renamed` and/or `job.due_date_changed` events, one per
changed field.

## POST /api/jobs/{id}/complete

Explicitly mark the Job complete. Aggregate rejects if any step is
incomplete or the Job has no steps.

**Request body**: empty.

**200 OK**: returns updated `JobDetail` with `status: "Completed"` and
`completedAt` populated.

**400 BusinessRule** with body `code: "steps_incomplete"` if any step
is unticked; `code: "job_has_no_steps"` if the step list is empty;
`code: "job_already_completed"` if the Job is already completed.
**404 NotFound** (unknown or not owned).

**Audit**: `job.completed` with `target=job:{id}`.
