# Tasks: 001-property-job-step

Decomposition of the plan into eight work packages (50 subtasks).
Each WP is independently mergeable. CI must stay green on every merge.

## Overview

| WP | Title | Subtasks | Domain | Depends on |
|---|---|---|---|---|
| WP01 | Authentication foundation | 7 (T001-T007) | backend-logic | - |
| WP02 | Cross-cutting infrastructure | 7 (T008-T014) | backend-logic | WP01 |
| WP03 | Property aggregate backend | 6 (T015-T020) | backend-logic | WP02 |
| WP04 | Property frontend | 5 (T021-T025) | frontend-craft | WP03 |
| WP05 | Job aggregate backend | 6 (T026-T031) | backend-logic | WP02 |
| WP06 | Job frontend | 6 (T032-T037) | frontend-craft | WP04, WP05 |
| WP07 | Step mutation + rename | 7 (T038-T044) | backend-logic | WP05, WP06 |
| WP08 | Hardening + acceptance | 6 (T045-T050) | testing-specialist | WP07 |

Parallelisation: WP04 and WP05 are independent after WP03 lands;
otherwise the chain is linear. MVP = WP01..WP06 (all P1 user stories).

## WP01 - Authentication foundation

Foundational plumbing: typed Result + errors, OwnerId value object,
identity provider abstraction, Google JWT validation, local stub, and
the production-blocks-stub startup assertion. No business endpoints yet.

- [ ] T001 [P] `Result<T>` and typed `Error` records in
      `backend/src/HomeMaintenance.Application/Common/` with unit tests
      covering success and every error variant.
- [ ] T002 [P] `OwnerId` value object in
      `backend/src/HomeMaintenance.Domain/Identity/` with unit tests for
      construction validation and equality semantics.
- [ ] T003 `IIdentityProvider` interface in
      `backend/src/HomeMaintenance.Application/Common/Interfaces/` plus
      `HttpContextIdentityProvider` in
      `backend/src/HomeMaintenance.Infrastructure/Auth/`. Registered as
      scoped.
- [ ] T004 Google OIDC JWT bearer validation in
      `backend/src/HomeMaintenance.API/Program.cs` (or extracted helper).
      Authority, audience, lifetime, signing-key validation; 24h JWKS
      cache; 60s clock skew.
- [ ] T005 Local stub `AuthenticationHandler` in
      `backend/src/HomeMaintenance.Infrastructure/Auth/`. Accepts
      `Authorization: Bearer dev-<sub>` and maps `<sub>` to OwnerId.
- [ ] T006 Startup assertion that throws when
      `Auth:UseStub == true` AND environment is not Development.
- [ ] T007 Integration tests: anonymous requests get 401; valid stub
      token resolves OwnerId; stub-in-production assertion fires.

## WP02 - Cross-cutting infrastructure

Audit log + correlation + RFC 7807 error translation. Dormant until
WP03+ wire it into use cases.

- [ ] T008 `IAuditLog` interface and `AuditEvent` record in Application.
- [ ] T009 `FileAuditLog` implementation in
      `backend/src/HomeMaintenance.Infrastructure/AuditLog/`. JSONL
      writer protected by `SemaphoreSlim`. Sink path from config.
- [ ] T010 `CorrelationIdMiddleware` in
      `backend/src/HomeMaintenance.API/Middleware/`. Reads or generates
      a request-scoped UUID; attaches to logs and audit events.
- [ ] T011 Result -> RFC 7807 problem-details translator in
      `backend/src/HomeMaintenance.API/Middleware/AuthErrorTranslator.cs`.
      Maps error codes to HTTP status per `contracts/README.md`.
- [ ] T012 Add `audit-trail/` to `.gitignore`; ensure dir exists at
      startup (created lazily by `FileAuditLog`).
- [ ] T013 [P] Unit tests for `FileAuditLog` (concurrent writes serialise,
      JSON format stable, file rolls over cleanly on restart).
- [ ] T014 Integration test: cross-owner access returns 404 with the
      correlationId present in the problem-details body and
      `X-Correlation-Id` header.

## WP03 - Property aggregate backend

Domain to API for the Property aggregate. Delivers user stories US1
(sign in + list) and US2 (create) on the server side. Frontend lands in
WP04.

- [ ] T015 `Property` aggregate root in
      `backend/src/HomeMaintenance.Domain/Properties/` with `Create` and
      `Rename` factories. Unit tests cover every invariant
      (FR-009, FR-012).
- [ ] T016 `PropertyDocument` and `PropertyRepository` in
      `backend/src/HomeMaintenance.Infrastructure/Persistence/`.
      Includes the `ownerId` and `ownerId+name` indexes from
      `data-model.md`. Registered as scoped.
- [ ] T017 `CreateProperty` and `RenameProperty` use cases in
      `backend/src/HomeMaintenance.Application/Properties/Commands/`
      with handler unit tests (success, validation, not-found-not-owned).
- [ ] T018 `ListProperties` and `GetProperty` queries in
      `backend/src/HomeMaintenance.Application/Properties/Queries/` with
      handler unit tests.
- [ ] T019 `PropertyEndpoints` in
      `backend/src/HomeMaintenance.API/Endpoints/`. POST /api/properties,
      GET /api/properties, GET /api/properties/{id}, PATCH
      /api/properties/{id}. Wires Result -> HTTP via translator.
- [ ] T020 Integration tests over the four endpoints: happy path,
      validation rejections, cross-owner access -> 404, audit events
      emitted for writes.

## WP04 - Property frontend

NextAuth-driven sign-in plus the `/properties` page (list + create).
Delivers US1 and US2 end-to-end.

- [ ] T021 NextAuth v5 wiring: `frontend/src/lib/auth.ts` with Google
      provider; `frontend/src/app/api/auth/[...nextauth]/route.ts`
      handler. `jwt` and `session` callbacks attach
      `session.idToken`.
- [ ] T022 Typed API client in `frontend/src/lib/api-client.ts` with
      Properties methods. Auto-attaches `Authorization: Bearer
      ${session.idToken}` for Server Component fetches and a
      header-getter for Client Components.
- [ ] T023 Sign-in page at `frontend/src/app/(auth)/signin/page.tsx`
      and middleware redirect for any unauthenticated request to a
      protected route.
- [ ] T024 `frontend/src/app/properties/page.tsx` (Server Component
      list) + `CreatePropertyForm` client component (POST + revalidate
      with `router.refresh()`).
- [ ] T025 [P] Jest + Testing Library: sign-in flow stub, create
      Property happy path, validation error rendered to user.

## WP05 - Job aggregate backend

Job aggregate end-to-end for the headline write/read flows. Step mutation
beyond initial creation is deferred to WP07 to keep prompt size
manageable.

- [ ] T026 `Job` aggregate root and `Step` child entity in
      `backend/src/HomeMaintenance.Domain/Jobs/` with full behaviour:
      `Create`, `Rename`, `SetDueDate`, `AddStep`, `RemoveStep`,
      `ReorderSteps`, `EditStepDescription`, `TickStep`, `UntickStep`,
      `Complete`. Unit tests for every invariant and lifecycle
      transition.
- [ ] T027 `JobDocument` (with embedded `StepDocument`) and
      `JobRepository` in
      `backend/src/HomeMaintenance.Infrastructure/Persistence/`.
      Indexes: `ownerId`, `ownerId+propertyId`, `ownerId+status`.
      Register `DateOnly` BSON serialiser if not built-in.
- [ ] T028 `CreateJob` command + `GetJob` and `ListJobs` queries in
      `backend/src/HomeMaintenance.Application/Jobs/`. `CreateJob`
      performs the cross-aggregate ownership check against
      `IPropertyRepository`. Handler unit tests.
- [ ] T029 `TickStep`, `UntickStep`, and `CompleteJob` use cases. The
      `CompleteJob` handler returns `BusinessRuleError` for the three
      failure modes (already-completed, no-steps, steps-incomplete).
      Handler unit tests.
- [ ] T030 `JobEndpoints` in `backend/src/HomeMaintenance.API/Endpoints/`.
      POST /api/jobs, GET /api/jobs, GET /api/jobs/{id}, POST
      /api/jobs/{id}/complete, POST /api/jobs/{id}/steps/{stepId}/tick,
      POST /api/jobs/{id}/steps/{stepId}/untick.
- [ ] T031 Integration tests: full create-tick-complete flow, cross-owner
      protections on every endpoint, complete-with-incomplete-step
      rejected, audit events emitted in order.

## WP06 - Job frontend

`/jobs/[id]` page with the step checklist, plus the create-job affordance
from the Property page. Delivers US3, US4, US5.

- [ ] T032 Extend `frontend/src/lib/api-client.ts` with Jobs methods
      (create, list, get, complete, tick, untick step).
- [ ] T033 `frontend/src/app/properties/[id]/page.tsx`: list Jobs under
      the Property (filter by `propertyId`) + `CreateJobForm` client
      component with dynamic step rows.
- [ ] T034 `frontend/src/app/jobs/[id]/page.tsx`: Server Component
      header (name, due date, status) + `JobChecklist` client island
      hosting the step list and Complete Job button.
- [ ] T035 [P] `StepCheckbox` client component: optimistic update on
      click, calls tick/untick, reverts on failure, disabled when Job
      is Completed.
- [ ] T036 [P] `CompleteJobButton` client component: enabled only when
      every step is ticked and Status is Active; calls
      `POST /api/jobs/{id}/complete`.
- [ ] T037 Jest tests for create-job (with three steps), tick a step
      (optimistic happy path + rollback on error), complete-job (button
      enable/disable logic, success path).

## WP07 - Step mutation + property/job rename (US6, US7)

Mutation beyond initial creation. P2 (US6) and P3 (US7) priorities.

- [ ] T038 Application: `AddStep`, `RemoveStep`, `ReorderSteps`,
      `EditStepDescription` use cases with handler unit tests.
- [ ] T039 Application: `RenameJob`, `SetJobDueDate`, `RenameProperty`
      use cases with handler unit tests.
- [ ] T040 API: step sub-resource endpoints. POST /api/jobs/{id}/steps,
      DELETE /api/jobs/{id}/steps/{stepId}, PATCH
      /api/jobs/{id}/steps/{stepId}, PUT /api/jobs/{id}/steps/order.
- [ ] T041 API: PATCH /api/jobs/{id} for rename and due date; PATCH
      /api/properties/{id} already exists from WP03.
- [ ] T042 Integration tests: full step-mutation matrix on Active Job;
      every mutation rejected on Completed Job (sealing matrix from
      FR-019 and FR-027).
- [ ] T043 Frontend: step add (inline input), remove (trash icon),
      reorder (drag handle backed by `@dnd-kit`), edit description
      (in-place edit). All operations call API and revalidate via
      `router.refresh()`.
- [ ] T044 Frontend: edit Job name + due date controls on /jobs/[id]
      header; edit Property name on /properties/[id] header.

## WP08 - Hardening + acceptance

Final acceptance pass before declaring Slice 1 complete.

- [ ] T045 Cross-owner integration matrix: every read and write endpoint
      with user A signed in and a resource id belonging to user B
      returns 404 (SC-005).
- [ ] T046 Completed-job sealing matrix: every mutation endpoint against
      a Completed Job returns the documented `business_rule` error and
      changes nothing (SC-007).
- [ ] T047 401 matrix: every non-`/health` endpoint with no token / a
      malformed token returns 401 (SC-006).
- [ ] T048 Acceptance tests named after FR-IDs (e.g.
      `FR_018_CompleteJob_RejectsIfAnyStepIncomplete`) so reviewers can
      cross-reference the spec.
- [ ] T049 Performance sanity: step-tick p95 round-trip < 500ms over
      100 iterations against a warm Mongo (SC-004). Captured as a long
      `[Trait("category","perf")]` test, opt-in.
- [ ] T050 Update `README.md`: how to set up Google OAuth client and
      where to find the audit log. Update `ARCHITECTURE.md` with a
      pointer to the constitution and the audit-log policy.

## Dependencies summary

```
WP01 -> WP02 -> WP03 -> WP04 -+-> WP06 -> WP07 -> WP08
                  `--------> WP05 -+
```

## MVP scope

WP01-WP06 delivers all five P1 user stories end-to-end (sign in, list /
create Property, create Job with steps, tick steps, complete Job).
WP07 adds P2/P3 polish (edit, reorder, rename). WP08 hardens the slice
for the constitution's acceptance criteria.

## Parallelisation opportunities

- WP04 and WP05 are independent after WP03 lands; they can run in
  parallel worktrees.
- Within WPs, subtasks tagged `[P]` are parallel-safe (e.g., T001/T002
  in WP01 touch disjoint folders).
