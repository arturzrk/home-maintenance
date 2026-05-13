# Contract: Steps (sub-resource of Jobs)

Base: `/api/jobs/{jobId}/steps`

All step mutations require the parent Job to be in `Status = Active`.
Mutating a step on a Completed Job returns
`400 BusinessRule {code: "job_completed"}`.

## POST /api/jobs/{jobId}/steps

Add a step at the end of the checklist.

**Request body**:
```json
{
  "description": "Refill expansion tank"
}
```

| Field | Type | Required | Constraints |
|---|---|---|---|
| `description` | string | yes | 1..500 chars, trimmed |

**201 Created**:
```json
{
  "id": "...",
  "order": 3,
  "description": "Refill expansion tank",
  "isCompleted": false,
  "completedAt": null
}
```

**400 ValidationError**.
**400 BusinessRule** (Job is completed).
**404 NotFound** (Job unknown or not owned).

**Audit**: `step.added` with `target=job:{jobId}/step:{stepId}`.

## DELETE /api/jobs/{jobId}/steps/{stepId}

Remove a step. Remaining steps are renumbered to keep `Order` contiguous
from 0.

**204 No Content** on success.
**404 NotFound** (Job or Step unknown / not owned).
**400 BusinessRule** (Job is completed).

**Audit**: `step.removed`.

## PATCH /api/jobs/{jobId}/steps/{stepId}

Edit a step's description.

**Request body**:
```json
{
  "description": "Refill expansion tank to 1.2 bar"
}
```

**200 OK** returns the updated step.

**400 ValidationError**.
**400 BusinessRule** (Job is completed).
**404 NotFound**.

**Audit**: `step.description_edited`.

## PUT /api/jobs/{jobId}/steps/order

Reorder the entire step list. The request MUST list every existing step id
exactly once.

**Request body**:
```json
{
  "orderedStepIds": ["stepC", "stepA", "stepB"]
}
```

**200 OK**: returns the updated `JobDetail`.

**400 ValidationError** if the list omits/duplicates an id or contains an
id not in the Job.
**400 BusinessRule** (Job is completed).
**404 NotFound** (Job unknown).

**Audit**: `step.reordered` with payload `{order: [...stepIds]}`.

## POST /api/jobs/{jobId}/steps/{stepId}/tick

Mark a step complete.

**Request body**: empty.
**200 OK** returns the updated step.

Idempotent: ticking an already-ticked step leaves it ticked and returns
200 with the existing `completedAt`.

**400 BusinessRule** (Job is completed).
**404 NotFound**.

**Audit**: `step.ticked`.

## POST /api/jobs/{jobId}/steps/{stepId}/untick

Mark a step incomplete.

**Request body**: empty.
**200 OK** returns the updated step.

Idempotent.

**400 BusinessRule** (Job is completed).
**404 NotFound**.

**Audit**: `step.unticked`.
