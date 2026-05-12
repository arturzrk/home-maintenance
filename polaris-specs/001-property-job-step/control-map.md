# Control Map - 001-property-job-step

A condensed map of the user-visible flows, the screens that host them, and
the shared dependencies between them. Use as the navigation index before
diving into `spec.md` for any single flow.

## Flows

| Flow | Screen(s) | Trigger | Outcome | Backed by |
|---|---|---|---|---|
| Sign-in | Landing page -> Google consent -> `/properties` | Anonymous user hits any protected route | Authenticated session; `OwnerId` resolved from `sub` | OIDC middleware, `IIdentityProvider` |
| List Properties | `/properties` | Authenticated land or refresh | Caller's Properties shown alphabetically | `ListProperties` use case |
| Create Property | `/properties` (modal or inline) | Click "Create Property", submit name | New Property persisted and visible | `CreateProperty` use case |
| Rename Property | `/properties/[id]` | Edit name, submit | Property name updated | `RenameProperty` use case |
| List Jobs for a Property | `/properties/[id]` | Open Property | Jobs filtered by PropertyId shown (Active first) | `ListJobs(propertyId)` |
| Create Job | `/properties/[id]` (modal or new page) | Click "Create Job", submit form with name + optional due date + initial steps | New Active Job under the Property | `CreateJob` use case |
| Open Job | `/jobs/[id]` | Click a Job card | Job detail with checklist | `GetJob` use case |
| Tick / untick step | `/jobs/[id]` | Click checkbox on a step | Step IsCompleted flipped, CompletedAt set/cleared | `TickStep` / `UntickStep` use cases |
| Add / remove / reorder / edit step | `/jobs/[id]` (inline edit, drag handle) | Edit affordances on the checklist | Step list updated; Orders renumbered to stay contiguous | `AddStep`, `RemoveStep`, `ReorderSteps`, `EditStepDescription` |
| Rename Job / update due date | `/jobs/[id]` | Edit Job header | Job header updated | `RenameJob`, `SetJobDueDate` |
| Complete Job | `/jobs/[id]` | Click "Complete Job" (enabled only when all steps ticked) | Job sealed: Status = Completed, CompletedAt set, all mutations rejected thereafter | `CompleteJob` use case |
| Cross-owner attempt | any route | Direct URL with another user's id | 404 (not 403, no leak) | Ownership filter in repository + use case |

## Shared Dependencies

| Dependency | Owner | Consumed by |
|---|---|---|
| OIDC token validation middleware | API project | Every non-`/health` endpoint |
| `IIdentityProvider` abstraction | Application | API auth wiring, local stub, future providers |
| `IPropertyRepository` | Application -> Infrastructure | `CreateProperty`, `ListProperties`, `GetProperty`, `RenameProperty`, `CreateJob` (cross-check) |
| `IJobRepository` | Application -> Infrastructure | All Job/Step use cases |
| Ownership-check helper (e.g. `RequireOwnership<T>`) | Application | Every Property/Job use case (read or write) |
| Audit logger | Infrastructure (via Application interface) | All write use cases + auth outcomes + authz denials |
| Typed API client (`frontend/src/lib/api-client.ts`) | Frontend | Every Server Component / Client Component that talks to the API |
| `audit-trail/property-job-step.jsonl` (local dev) | Infrastructure | Audit logger sink |

## Aggregate Boundaries

- `Property` is its own aggregate. The Job aggregate references it by id;
  the cross-aggregate invariant (Job.PropertyId belongs to Job.Owner) is
  enforced at the use-case layer, not by traversing aggregates.
- `Job` is the aggregate root for `Step`. Steps are loaded and saved with
  the Job document (MongoDB embedded array). No code outside the Job
  aggregate may mutate a Step.

## Lifecycle States

```
Job:
  (created) -> Active
  Active --[CompleteJob, all steps done]--> Completed
  Active --[* mutations]--> Active
  Completed --[any mutation]--> rejected
```

## Out-of-band (Slice 1b / Slice 2 placeholders)

- `Asset` is referenced in the future-flows column only. Slice 1 controls
  do not depend on it.
- `JobDefinition` / `JobOccurrence` are not in any flow above. Slice 2
  will introduce a "create from template" flow that supersedes
  `CreateJob` for recurring work.
