# API Contract: Job Definitions

Base path: `/api/job-definitions`
Auth: Bearer token required on all endpoints.
Error shape: RFC 7807 problem-details (same as Slice 1).

---

## POST /api/job-definitions

Create a new JobDefinition.

**Request body**
```json
{
  "propertyId": "string (required)",
  "name": "string (required, 1--200 chars)",
  "schedule": {
    "unit": "Day | Week | Month | Year (required)",
    "multiplier": 1,
    "startDate": "2026-06-01",
    "endDate": "2027-06-01"
  },
  "stepTemplates": [
    { "description": "string (required, 1--500 chars)" }
  ]
}
```

**Responses**
- `201 Created` --- body: `JobDefinitionDto` (see schema below). `Location: /api/job-definitions/{id}`
- `400 Bad Request` --- validation error (empty name, multiplier < 1, etc.)
- `401 Unauthorized`
- `404 Not Found` --- propertyId does not belong to caller

---

## GET /api/job-definitions

List the caller's JobDefinitions.

**Query params**
- `propertyId` (optional) --- filter to one property

**Responses**
- `200 OK` --- body: `JobDefinitionDto[]` (may be empty)
- `401 Unauthorized`

---

## GET /api/job-definitions/{id}

Read a single JobDefinition by id.

**Responses**
- `200 OK` --- body: `JobDefinitionDto`
- `401 Unauthorized`
- `404 Not Found` --- id unknown or owned by another user

---

## PATCH /api/job-definitions/{id}

Update name, schedule, and/or step templates. All fields optional; at least one must be present.

**Request body**
```json
{
  "name": "string (optional, 1--200 chars)",
  "schedule": {
    "unit": "Month",
    "multiplier": 6,
    "startDate": "2026-01-01",
    "endDate": null
  },
  "addStepTemplates": [
    { "description": "string" }
  ],
  "removeStepTemplateIds": ["string"],
  "editStepTemplates": [
    { "id": "string", "description": "string" }
  ],
  "reorderStepTemplateIds": ["id1", "id2", "id3"]
}
```

Only mutations present in the request body are applied. `reorderStepTemplateIds`, if present, must list all current template ids.

**Responses**
- `200 OK` --- body: updated `JobDefinitionDto`
- `400 Bad Request` --- validation or business rule error
- `401 Unauthorized`
- `404 Not Found`

---

## POST /api/job-definitions/{id}/generate-next

Manually generate the next occurrence as a concrete Job.

**Request body**: empty (`{}`)

**Responses**
- `201 Created` --- body: `JobDto` (standard job shape with `jobDefinitionId` set). `Location: /api/jobs/{jobId}`
- `400 Bad Request` --- `code: "next_occurrence_already_exists"` if the next occurrence is already generated
- `401 Unauthorized`
- `404 Not Found`

---

## Schemas

### JobDefinitionDto
```json
{
  "id": "string",
  "propertyId": "string",
  "name": "string",
  "schedule": {
    "unit": "Month",
    "multiplier": 3,
    "startDate": "2026-06-01",
    "endDate": null
  },
  "stepTemplates": [
    { "id": "string", "order": 0, "description": "string" }
  ]
}
```

### JobDto (updated --- adds jobDefinitionId)
```json
{
  "id": "string",
  "propertyId": "string",
  "name": "string",
  "dueDate": "2026-06-01",
  "status": "Active | Completed",
  "completedAt": null,
  "steps": [
    { "id": "string", "order": 0, "description": "string", "isCompleted": false, "completedAt": null }
  ],
  "jobDefinitionId": "string | null"
}
```

`jobDefinitionId` is `null` for one-shot jobs; a GUID string for generated jobs. This field is read-only --- it cannot be set or changed via the jobs API.
