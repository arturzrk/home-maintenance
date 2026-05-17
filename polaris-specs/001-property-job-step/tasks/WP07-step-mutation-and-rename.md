---
work_package_id: WP07
lane: "doing"
dependencies: [WP05, WP06]
base_branch: main
base_commit: 5dcf4fdbe30f1d0fb36f5a1fb2292b86ee6702e0
created_at: '2026-05-17T13:07:57.631582+00:00'
subtasks: [T038, T039, T040, T041, T042, T043, T044]
test_status: required
test_file: tests/e2e/WP07-wp07-step-mutation-and-rename.e2e.js
domain: backend-logic
shell_pid: "16074"
---

# WP07 - Step mutation + Property/Job rename (US6, US7)

## Objective

Land US6 (P2: edit/reorder/remove steps on an Active Job) and US7
(P3: rename Property, rename Job, update due date). The Job and Step
aggregates already implement these behaviours (WP05 wrote them);
WP07 exposes them as use cases, endpoints, and frontend affordances.

## Inputs

- Spec: US6, US7 acceptance scenarios; FR-017, FR-019, FR-021..FR-024.
- Contracts: `contracts/jobs.md` PATCH section and `contracts/steps.md`
  (everything except tick/untick which is in WP05).

## Subtasks

### T038 - Application: AddStep, RemoveStep, ReorderSteps, EditStepDescription

Four command handlers under
`backend/src/HomeMaintenance.Application/Jobs/Commands/`. Each:

1. Load Job by id+owner. NotFound on miss.
2. Short-circuit with `BusinessRuleError("job_completed", ...)` if
   `Status == Completed`.
3. Delegate to the corresponding aggregate method (which returns
   `Result<None>` for the methods that report business failures or
   throws for raw validation - catch and translate).
4. `UpdateAsync(job)` on success.
5. Emit the matching audit event (`step.added`, `step.removed`,
   `step.reordered`, `step.description_edited`).

Handler unit tests for each: success, not-found-not-owned,
completed-job rejection, and the use-case-specific validation
failures (reorder with wrong count, duplicate id, etc.).

### T039 - Application: RenameJob, SetJobDueDate, RenameProperty

Three command handlers:

- `RenameJob`: load, check active, call aggregate `Rename`, persist,
  audit `job.renamed`.
- `SetJobDueDate`: load, check active, call `SetDueDate`, persist,
  audit `job.due_date_changed`.
- `RenameProperty` already exists in `Application/Properties/Commands`
  (added in WP03). Nothing new here unless behaviour drift is
  detected; if so, fix in this WP.

### T040 - API: step sub-resource endpoints

In `JobEndpoints` (extend the file created in WP05):

| Verb | Route | Handler |
|---|---|---|
| POST | `/api/jobs/{id}/steps` | AddStep |
| DELETE | `/api/jobs/{id}/steps/{stepId}` | RemoveStep |
| PATCH | `/api/jobs/{id}/steps/{stepId}` | EditStepDescription |
| PUT | `/api/jobs/{id}/steps/order` | ReorderSteps |

Wire DTOs (`AddStepRequest`, `EditStepDescriptionRequest`,
`ReorderStepsRequest`) and DataAnnotation validation via
MiniValidator. Map results via `.ToHttpCreated(...)` for POST and
`.ToHttp(...)` for the rest.

### T041 - API: PATCH /api/jobs/{id}

In `JobEndpoints` add:

```csharp
group.MapPatch("{id}", async (string id, UpdateJobRequest body, ...) =>
{
    if (!MiniValidator.TryValidate(body, out var errors))
        return Results.ValidationProblem(errors);

    // Apply two potentially independent updates as a small unit-of-work
    // by loading once, mutating both, persisting once.
    var result = await handler.Handle(new UpdateJobCommand(id, body.Name, body.DueDate), ct);
    return result.ToHttp(ctx);
});
```

`UpdateJobRequest` has optional `Name` and `DueDate`; if neither is
present, return 400 `validation`. If both, apply in order
(Rename -> SetDueDate). Persist once. Emit one audit event per field
actually changed.

### T042 - Integration tests: step matrix + sealing matrix

In `backend/tests/HomeMaintenance.Integration.Tests/Jobs/`:

Step mutation happy paths on an Active Job:
- `AddStep_AppendsAtEnd`
- `RemoveStep_Renumbers`
- `ReorderSteps_FullList_Succeeds`
- `EditStepDescription_Updates`

Validation rejections:
- `ReorderSteps_PartialList_Returns400`
- `ReorderSteps_WithUnknownId_Returns400`
- `AddStep_Empty_Returns400`
- `EditStepDescription_OverLimit_Returns400`

Sealing matrix (a single parameterised test class is fine):
- For each of {AddStep, RemoveStep, ReorderSteps, EditStepDescription,
  TickStep, UntickStep, RenameJob, SetDueDate, CompleteJob}, when
  Job is Completed -> 400 with `code: "job_completed"` (or the
  specific code for that mutation).

Audit assertions:
- Step events recorded with `target=job:{id}/step:{stepId}` shape.

### T043 - Frontend: step add/remove/reorder/edit

Edit `JobChecklist` from WP06 to add:

- **Add step**: a `<input>` row below the list with "Add step"
  button. On click, POST `/api/local/jobs/{id}/steps`, refresh.
- **Remove step**: a trash-icon button per row, only shown for
  Active Jobs. POST DELETE BFF route, refresh.
- **Edit description**: double-click (or "Edit" button) on a step
  description; renders an `<input>` in place; on blur or Enter,
  PATCH the step; on Escape, cancel.
- **Reorder**: drag handle on each step. Use `@dnd-kit/core` + 
  `@dnd-kit/sortable`. On drop, compute the new ordered id list and
  PUT to `/api/local/jobs/{id}/steps/order`. Refresh.

Install:
```bash
npm install @dnd-kit/core @dnd-kit/sortable @dnd-kit/utilities
```

All four affordances are disabled when `job.status === "Completed"`.

### T044 - Frontend: edit Property and Job header

- `/properties/[id]`: add an inline "Edit name" button next to the
  Property name. Same in-place edit pattern as steps.
- `/jobs/[id]`: same for Job name and due date. Disable when
  Job is Completed.

The BFF route for PATCH `/api/jobs/{id}` accepts a JSON body with
either or both of `name` and `dueDate`.

## Test strategy

- Unit: handlers for the seven new commands (success / not-found /
  completed-job / validation per command).
- Integration: full happy-path matrix and the sealing matrix (the
  spec's FR-019 and FR-027 promises).
- Frontend Jest: at least one test per affordance (add, remove,
  reorder, edit description, rename Job, edit due date). For drag-
  and-drop, test the higher-level outcome (`PUT /steps/order` body
  contains the expected ordered ids) rather than the drag interaction.

## Definition of Done

- [ ] Active Job: every step affordance works end-to-end.
- [ ] Completed Job: every mutation endpoint returns
      `400 business_rule` and the UI controls are disabled.
- [ ] Renaming a Property updates it in the list view.
- [ ] Renaming a Job and updating due date updates the Job header.
- [ ] CI green.

## Risks and non-obvious bits

- Drag-and-drop reorder calls `PUT /api/jobs/{id}/steps/order` with
  the full ordered id list. The aggregate validates the list shape;
  failure modes are explicit (`partial list`, `unknown id`,
  `duplicate id`). Surface these as inline errors.
- The in-place edit pattern (input replaces text) is repeated four
  times. After this WP, consider extracting an `InlineEditableText`
  component. Do not pre-empt the abstraction here.
- `@dnd-kit` requires a `DndContext` provider; place it inside
  `JobChecklist` and not in the root layout.
- The PATCH /jobs/{id} endpoint accepts partial updates. The handler
  emits at most two audit events; reviewers may push for separate
  endpoints. Single PATCH was chosen for UX simplicity (rename and
  due-date change are commonly done together).

## Next command

```
polaris implement WP07 --base WP06
```
