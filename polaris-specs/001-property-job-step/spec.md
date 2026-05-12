# Feature Specification: Properties, Jobs and Step Checklists (Slice 1)

**Feature Branch**: `001-property-job-step`
**Created**: 2026-05-12
**Status**: Draft
**Tracker**: GitHub issue [#1](https://github.com/arturzrk/home-maintenance/issues/1)
**Input**: User description: "Slice 1 of the home-maintenance app. Properties + one-shot Jobs with ordered Step checklists, secured by Google OIDC with ownership-based authz. Asset is deferred to Slice 1b; recurrence is deferred to Slice 2."

## Summary

A homeowner signs in with Google, registers one or more **Properties** they
own, and creates one-shot **Jobs** against a Property. Each Job carries an
ordered checklist of **Steps**. Steps may be ticked off in any sequence
(display-only ordering). When every step is complete, the homeowner
explicitly **marks the Job complete**, sealing it as a permanent record.
A homeowner cannot see or modify another homeowner's Properties or Jobs.

This is the first vertical slice of the home-maintenance product. It
deliberately excludes Assets (Slice 1b), recurring jobs (Slice 2), and
several other concerns enumerated in the Out of Scope section.

## User Scenarios and Testing *(mandatory)*

### User Story 1 - Sign in and see my Properties (Priority: P1)

A homeowner opens the app, signs in with their Google account, and lands on
their list of Properties. New users see an empty list with a clear call to
action to create their first Property.

**Why this priority**: Authentication and per-user data isolation are the
absolute floor. Without them, no other feature can ship safely.

**Independent Test**: With Google OIDC configured (or the local stub
enabled), launch the app, sign in, and observe that the Properties page is
shown with only the signed-in user's Properties (zero or more). Sign in as
a different user and verify the previous user's Properties are not visible.

**Acceptance Scenarios**:

1. **Given** an anonymous visitor, **When** they navigate to any
   non-public route, **Then** they are redirected to Google sign-in and
   returned to the originally requested page after consent.
2. **Given** a signed-in user with no Properties, **When** they land on
   `/properties`, **Then** they see an empty-state message and a "Create
   Property" affordance.
3. **Given** a signed-in user with one or more Properties, **When** they
   land on `/properties`, **Then** they see only their own Properties,
   listed by Name.
4. **Given** user A has Property X and user B has Property Y, **When** user
   A signs in, **Then** Property Y is not present in any response and any
   direct request for Property Y's id returns a not-found response (not a
   "forbidden" leak).

---

### User Story 2 - Create a Property (Priority: P1)

A signed-in homeowner creates a new Property by providing a name. The
Property is owned by them automatically.

**Why this priority**: Properties are the parent of Jobs. Without at least
one Property, no Job can be created.

**Independent Test**: Sign in, click "Create Property", enter a name, and
verify the Property appears in the list with the correct name. Refresh the
page and verify it persists. Sign in as a different user and verify the
new Property is not visible.

**Acceptance Scenarios**:

1. **Given** a signed-in user on `/properties`, **When** they submit
   "Create Property" with name "Main House", **Then** the new Property
   appears in their list and is selected/viewable.
2. **Given** a signed-in user, **When** they submit "Create Property"
   with an empty name, **Then** the create action is rejected with a
   validation error and no Property is created.
3. **Given** a signed-in user, **When** they submit "Create Property"
   with a 101-character name, **Then** the create action is rejected with
   a validation error.
4. **Given** a signed-in user with an existing Property "Main House",
   **When** they create a second Property named "Beach Cabin",
   **Then** both Properties are visible.

---

### User Story 3 - Create a Job with a step checklist (Priority: P1)

A signed-in homeowner opens one of their Properties and creates a one-shot
Job against it. They provide a name, optionally a due date, and an ordered
list of step descriptions. The Job starts in **Active** state with no steps
completed.

**Why this priority**: This is the core write-path for the product. Without
creating Jobs, none of the maintenance value is delivered.

**Independent Test**: Sign in, open a Property, click "Create Job", enter
a name, optionally a due date, add three step descriptions in order, and
submit. Verify the Job appears under the Property with the steps in the
order entered and all unchecked.

**Acceptance Scenarios**:

1. **Given** a signed-in user with a Property, **When** they submit a Job
   with name "Service boiler", due date 2026-06-01, and three steps
   ("Shut off gas", "Drain system", "Replace filter"), **Then** the Job
   is created against that Property with Status = Active, the steps appear
   in the entered order with Orders 0, 1, 2, and all IsCompleted = false.
2. **Given** a signed-in user, **When** they submit a Job with no steps,
   **Then** the Job is created with an empty checklist (steps can be
   added later while Active).
3. **Given** a signed-in user, **When** they submit a Job with an empty
   name, **Then** creation is rejected with a validation error.
4. **Given** a signed-in user, **When** they submit a Job with a
   step description over 500 characters, **Then** creation is rejected.
5. **Given** user A has Property X, **When** user B attempts to create a
   Job against Property X, **Then** creation is rejected with a not-found
   response (no leak that X exists).
6. **Given** an unauthenticated client, **When** they attempt to create a
   Job, **Then** the request is denied with a 401 response.

---

### User Story 4 - Tick steps off in any order (Priority: P1)

A signed-in homeowner opens an Active Job and ticks off steps as they make
progress. Each tick records the timestamp. Unticking a step clears that
timestamp. The displayed order is preserved regardless of completion
sequence.

**Why this priority**: The whole point of the checklist is to mark progress.
P1 because the user gains zero value if they can create Jobs but cannot
record progress on them.

**Independent Test**: Open an Active Job with three steps. Tick the third
step; observe that it shows completed with a timestamp and the other two
remain unchecked. Untick the third step; observe the timestamp clears.
Tick all three; observe the Job remains in Active status (completion is
explicit, see Story 5).

**Acceptance Scenarios**:

1. **Given** an Active Job with steps unchecked, **When** the owner ticks
   any step, **Then** that step's IsCompleted becomes true and CompletedAt
   records the current UTC timestamp.
2. **Given** a Step that was ticked, **When** the owner unticks it,
   **Then** IsCompleted becomes false and CompletedAt is cleared to null.
3. **Given** an Active Job, **When** the owner ticks the last
   step in display order before any earlier step, **Then** the operation
   succeeds (ordering is display-only).
4. **Given** an Active Job with all steps ticked, **When** no
   `CompleteJob` call has been made, **Then** Status remains Active and
   CompletedAt remains null. Job-level completion is explicit.
5. **Given** an Active Job, **When** a non-owner attempts to tick a step,
   **Then** the request is denied with a not-found response.

---

### User Story 5 - Explicitly complete a Job (Priority: P1)

When every step is ticked, the homeowner explicitly clicks "Complete Job".
The Job transitions to Status = Completed, CompletedAt is recorded, and the
Job becomes read-only. The "Complete Job" action is rejected if any step
is still incomplete.

**Why this priority**: The "seal" is the proof-of-work record that makes
the app valuable beyond a checklist - it gives a permanent history. P1
because shipping without sealing leaves Jobs in an indeterminate state.

**Independent Test**: Create an Active Job with three steps. Try
"Complete Job" before ticking any step; observe rejection. Tick the
first two and try again; observe rejection. Tick all three and complete;
observe the Job transitions to Completed, CompletedAt is recorded, and
all mutation actions (add step, untick step, rename) are now rejected.

**Acceptance Scenarios**:

1. **Given** an Active Job with at least one incomplete step, **When** the
   owner invokes `CompleteJob`, **Then** the request is rejected with a
   business-rule error and the Job remains Active.
2. **Given** an Active Job with all steps complete, **When** the owner
   invokes `CompleteJob`, **Then** Status becomes Completed, CompletedAt
   is set to the current UTC timestamp, and the response reflects the new
   state.
3. **Given** a Completed Job, **When** the owner attempts any mutation
   (add/remove/reorder/edit step, tick/untick step, rename Job, change
   due date, complete again), **Then** the request is rejected.
4. **Given** a Completed Job, **When** the owner views it, **Then** the UI
   shows it as read-only with the completion timestamp.

---

### User Story 6 - Edit, reorder and remove steps while Active (Priority: P2)

A signed-in homeowner can refine an Active Job's checklist after creation:
add a new step, remove a step, reorder steps, or edit a step description.
All mutations are rejected once the Job is Completed.

**Why this priority**: Useful for real workflows (you find out about a step
mid-way), but the core flow can ship without it. P2 because Stories 3-5
already deliver the headline value.

**Independent Test**: Create an Active Job with three steps. Add a fourth
step; verify it appears at Order 3. Remove the second step; verify
remaining steps renumber to 0, 1, 2 contiguously. Reorder the steps;
verify the new Order values reflect the request. Edit a step's
description; verify it persists.

**Acceptance Scenarios**:

1. **Given** an Active Job with N steps, **When** a step is added,
   **Then** it appears with Order = N and prior Orders are unchanged.
2. **Given** an Active Job with steps at Orders 0, 1, 2, **When** the
   step at Order 1 is removed, **Then** the remaining steps are
   renumbered to 0, 1 and the document state is contiguous.
3. **Given** an Active Job, **When** the owner submits a full reorder
   request listing all current step ids in a new order, **Then** Orders
   are renumbered accordingly. Submitting a partial list is rejected.
4. **Given** an Active Job, **When** an empty or 501-character description
   is submitted for a step, **Then** the edit is rejected.
5. **Given** a Completed Job, **When** any of the above is attempted,
   **Then** the request is rejected.

---

### User Story 7 - Rename Property or Job, update Job due date (Priority: P3)

A signed-in homeowner can rename one of their Properties at any time, and
rename an Active Job or change its due date. Renaming a Completed Job and
changing the due date of a Completed Job are both rejected.

**Why this priority**: Quality-of-life. The user can work around it
(create a new Property/Job with the right name) for an initial release.

**Acceptance Scenarios**:

1. **Given** an existing Property, **When** the owner submits a new name,
   **Then** the Property is updated and the new name appears in the list.
2. **Given** an Active Job, **When** the owner changes the name or due
   date, **Then** the change is persisted.
3. **Given** a Completed Job, **When** any rename or due-date change is
   attempted, **Then** the request is rejected.

---

### Edge Cases

- **Cross-owner access by id**: any GET/PATCH/DELETE referencing a
  Property or Job owned by another user returns a 404 (not 403), to
  prevent enumerability of others' ids.
- **Stale ids**: a GET against a non-existent or deleted-style id returns
  a 404. Since neither Property nor Job supports deletion in Slice 1, the
  not-found case applies only to ids never created.
- **Concurrent edits**: two devices editing the same Active Job submit
  step mutations. The latest write wins; we do not implement optimistic
  concurrency control in Slice 1. Documented as a known limitation; a
  follow-up may add ETag/If-Match.
- **Token expiry mid-session**: when a Bearer token expires, the next
  request returns 401 and the frontend silently refreshes via the OIDC
  flow before retrying. If refresh fails, the user is redirected to
  sign-in.
- **Clock skew between client and server**: all timestamps in the
  database are UTC produced server-side. The frontend formats locally.
- **Empty step list at completion**: a Job with zero steps cannot be
  completed via `CompleteJob` (the "all steps complete" rule has nothing
  to satisfy in a meaningful way). Open question - see Assumptions; the
  default is to require at least one step before `CompleteJob` succeeds.
- **Reorder request that includes an id not in the Job**: rejected as
  validation error. Reorder request that omits an existing id: same.
- **Sign-in cancelled at Google**: returned to the marketing/landing
  page with a non-blocking notice; no partial session is created.
- **Owner-id mismatch on stored token**: if the OIDC `sub` no longer
  matches the stored OwnerId for a session (extremely unlikely; would
  indicate token tampering), the request is rejected and the session is
  cleared.

## Requirements *(mandatory)*

### Functional Requirements

**Authentication**
- **FR-001**: System MUST authenticate users via Google OIDC. The
  authenticated principal's OwnerId is the verified `sub` claim of the
  ID token. The system MUST validate the token signature, issuer, audience
  and expiry on every request.
- **FR-002**: Local development MUST support an OIDC stub that issues a
  synthetic `sub` (configurable per developer) without contacting Google.
  Production MUST NOT accept stub tokens.
- **FR-003**: System MUST treat every endpoint except `/health` as
  authenticated. Anonymous requests to any other endpoint MUST return 401.

**Authorization (ownership-based, default-deny)**
- **FR-004**: A Property MUST be readable only by its Owner. Any request
  for a Property whose OwnerId differs from the caller's MUST return 404.
- **FR-005**: A Property MUST be mutable only by its Owner.
- **FR-006**: A Job MUST be readable only by its Owner.
- **FR-007**: A Job MUST be mutable only by its Owner.
- **FR-008**: System MUST verify that any PropertyId supplied during Job
  creation resolves to a Property owned by the caller; otherwise the
  request is rejected with 404.

**Properties**
- **FR-009**: A user MUST be able to create a Property with a Name
  (non-empty, trimmed, max 100 characters). The created Property's
  Owner is the caller's OwnerId.
- **FR-010**: A user MUST be able to list their Properties, ordered by
  Name ascending, case-insensitive.
- **FR-011**: A user MUST be able to read a single Property by id.
- **FR-012**: A user MUST be able to rename a Property at any time.
  Validation matches FR-009.
- **FR-013**: Property deletion is out of scope for Slice 1 (see Out of
  Scope). No delete endpoint MUST be exposed.

**Jobs**
- **FR-014**: A user MUST be able to create a Job against one of their
  Properties with: Name (non-empty, trimmed, max 200 characters), optional
  DueDate (calendar date, not datetime; ISO-8601 date format), and an
  initial list of Steps (may be empty; each description non-empty,
  trimmed, max 500 characters). The created Job's Status is Active and
  Owner matches the caller.
- **FR-015**: A user MUST be able to list their Jobs, optionally filtered
  by PropertyId. Listing MUST return Jobs owned by the caller only.
- **FR-016**: A user MUST be able to read a single Job by id, including
  its Steps.
- **FR-017**: A user MUST be able to rename an Active Job (validation
  matches FR-014) and change its DueDate (or clear it).
- **FR-018**: A user MUST be able to explicitly complete an Active Job.
  The request MUST be rejected if the Job has zero Steps or if any Step
  is incomplete. On success, Status becomes Completed and CompletedAt is
  the current UTC timestamp.
- **FR-019**: A Completed Job MUST be immutable. Any mutation request
  (rename, due-date change, step add/remove/reorder/edit, step tick or
  untick, repeated complete) MUST be rejected.
- **FR-020**: Job deletion is out of scope for Slice 1.

**Steps**
- **FR-021**: While a Job is Active, a user MUST be able to add a Step
  (description per FR-014). The new Step's Order MUST be the current
  Step count.
- **FR-022**: While a Job is Active, a user MUST be able to remove a
  Step by its id. After removal, remaining Steps' Orders MUST be
  renumbered to remain contiguous from 0.
- **FR-023**: While a Job is Active, a user MUST be able to submit a
  full reorder of Steps: the request lists every existing Step id in the
  desired new order. The request MUST be rejected if the list omits or
  duplicates any existing id, or contains an id not in the Job. On
  success, Step Orders MUST be renumbered accordingly.
- **FR-024**: While a Job is Active, a user MUST be able to edit a
  Step's description (validation per FR-014).
- **FR-025**: While a Job is Active, a user MUST be able to tick a Step
  (set IsCompleted = true, CompletedAt = current UTC timestamp).
- **FR-026**: While a Job is Active, a user MUST be able to untick a
  Step (set IsCompleted = false, clear CompletedAt).
- **FR-027**: Ticking, unticking, or any other Step mutation against a
  Completed Job MUST be rejected.

**Data integrity**
- **FR-028**: All ids MUST be globally unique (GUID / Mongo ObjectId
  equivalent). Ids are not enumerable.
- **FR-029**: All timestamps stored MUST be UTC. The frontend MUST
  format timestamps in the user's local timezone for display.
- **FR-030**: All persisted Property and Job documents MUST carry an
  indexed `ownerId` field to support the ownership query.

**API conventions**
- **FR-031**: The API MUST never serialise Domain entities directly.
  All API responses MUST be DTOs defined in the Application layer.
- **FR-032**: The API MUST reject any request body that fails DTO
  validation with a 400 response and a structured error payload.

### Key Entities

- **OwnerId** (value object): wraps the OIDC `sub` claim. Equality is by
  string value. No user metadata stored.
- **Property** (aggregate root): identified by a unique id. Holds
  OwnerId (immutable after creation) and Name. Multiple Properties per
  OwnerId.
- **Job** (aggregate root): identified by a unique id. Holds OwnerId
  (immutable), PropertyId (immutable; must reference a Property with
  the same OwnerId), Name, optional DueDate, Status (Active or
  Completed), nullable CompletedAt, and an ordered list of Steps.
- **Step** (child entity of Job): identified by a unique id within the
  Job. Holds Order (int, unique within Job, contiguous from 0),
  Description, IsCompleted, and nullable CompletedAt.

## Audit and Security *(mandatory)*

### Data Classification

- **Data tier touched by this feature**: Internal.
- **Examples of data in scope**: OwnerId (opaque OIDC `sub`), Property
  Name (user-supplied free text), Job Name and DueDate (user-supplied),
  Step Description (user-supplied), completion timestamps.
- **Inherits from constitution baseline**: Yes. No user-identifying
  metadata (name, email, photo) is stored - it lives with Google.
  Treating OwnerId as opaque keeps the Internal tier honest.

### Audit Logging

- **Events that MUST be logged to the append-only audit trail**
  (`audit-trail/property-job-step.jsonl` in local development; managed
  sink in any non-local deployment):
  - `auth.signin_success` - actor=ownerId, outcome=success, timestamp,
    provider=google, correlation_id
  - `auth.signin_failure` - actor=email-hint-or-empty, outcome=failure,
    timestamp, reason (omit token contents), correlation_id
  - `authz.denied` - actor=ownerId, action, target (resource type + id),
    timestamp, correlation_id
  - `property.created` - actor=ownerId, target=propertyId, timestamp,
    name, correlation_id
  - `property.renamed` - actor=ownerId, target=propertyId, timestamp,
    old_name, new_name, correlation_id
  - `job.created` - actor=ownerId, target=jobId, propertyId, timestamp,
    name, due_date, step_count, correlation_id
  - `job.renamed` / `job.due_date_changed` - actor, target, old, new
  - `job.completed` - actor=ownerId, target=jobId, timestamp,
    correlation_id
  - `step.added` / `step.removed` / `step.reordered` /
    `step.description_edited` / `step.ticked` / `step.unticked` -
    actor, target=(jobId, stepId), timestamp, correlation_id
- **Retention**: 1 year per constitution baseline.
- **Sink**: `audit-trail/property-job-step.jsonl` locally; managed sink
  (Azure Monitor, Splunk, S3 + Object Lock, or equivalent) MUST be wired
  before any non-local deployment.
- **PII handling**: OwnerId is stored as-is. No additional PII is in the
  audit payload. Failure events MUST NOT log the raw token or the
  user's email if it cannot be safely captured.

### AuthN / AuthZ

- **Who can invoke this feature**: authenticated users only. There is no
  role distinction in Slice 1 - every user is the owner of their own
  data.
- **Default-deny rule**: middleware rejects any non-`/health` request
  without a valid Bearer token. Application-layer use cases reject any
  request whose target resource is not owned by the caller (return as
  404 to outside callers).
- **Principal source**: validated Google OIDC ID token (JWT) carried as
  `Authorization: Bearer <token>`. The OwnerId is the verified `sub`
  claim. The local stub returns a configurable synthetic sub.

### Threat Surface

- **Untrusted input boundaries this feature exposes**:
  - Public HTTP boundary at the Next.js sign-in flow (Google OAuth2
    redirect). Validated entirely by Google.
  - Authenticated HTTP boundary at the .NET API (`/api/properties`,
    `/api/jobs`, nested routes). Inputs validated at the DTO layer.
- **OWASP Top 10 categories the feature must defend against**:
  - A01 Broken Access Control - ownership checks, default-deny, return
    404 not 403 for cross-owner access (see edge cases).
  - A02 Cryptographic Failures - TLS required outside localhost; ID
    token signature MUST be validated (RS256/JWKS).
  - A03 Injection - MongoDB.Driver parameterised queries; no
    string-interpolated query construction.
  - A05 Security Misconfiguration - production MUST refuse the OIDC
    stub by configuration; tokens MUST require the correct
    issuer/audience.
  - A07 Identification and Authentication Failures - signature
    validation, expiry checks, audience checks, JWKS rotation
    supported.
  - A08 Software and Data Integrity Failures - audit log is
    append-only; backups out of scope for Slice 1.
- **Required mitigations** (designed in plan.md): JWT validation
  middleware with JWKS caching, DTO validation at API boundary, central
  authorization helper used by every Application use case, structured
  error responses (no stack traces).
- **Multi-tenant isolation**: single-tenant per OwnerId; every
  Property and Job query is filtered by `ownerId`. Multi-tenant
  enterprise isolation is out of scope.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A returning homeowner can sign in and reach the first
  page of their Properties in under 5 seconds, measured from arrival
  on the landing page to first paint of `/properties` (excluding
  initial Google sign-in consent screen on first ever sign-in).
- **SC-002**: A signed-in homeowner can create their first Property
  with no documentation, in under 30 seconds from landing on
  `/properties` to seeing the Property in the list.
- **SC-003**: A signed-in homeowner can create a Job with at least
  three steps in under 60 seconds end-to-end.
- **SC-004**: Step ticking p95 round-trip is under 500ms (constitution
  performance budget).
- **SC-005**: Zero cross-owner data leakage. A test harness signing in
  as user A and probing user B's ids returns 404 in 100% of cases.
- **SC-006**: 100% of mutating endpoints reject anonymous callers with
  401 in the integration test suite.
- **SC-007**: Completed Jobs cannot be mutated. A test harness
  exercising every mutation endpoint against a Completed Job returns
  rejection in 100% of cases.
- **SC-008**: Backend unit + integration suites pass with no skipped
  tests on a clean checkout; frontend Jest suite passes; encoding
  validator and lint pass.

## Assumptions

- **Single-tenant per user**: a Property has exactly one Owner.
  Sharing (e.g., spouse access) is out of scope; the OwnerId model
  does not preclude adding it later.
- **At least one step required for completion**: `CompleteJob` rejects
  a Job with zero steps. Alternative interpretation - "an empty
  checklist is trivially complete" - is rejected here because it would
  let the user mark a Job as done without committing to any work.
- **Render order, not enforcement order**: clients render Steps by
  ascending Order. Marking step N+1 before N is intentional.
- **NextAuth chosen for the frontend OAuth handshake**: chosen for
  Next.js App Router parity. The API itself never depends on NextAuth;
  it only validates the ID token. Subject to plan-phase confirmation.
- **No optimistic concurrency in Slice 1**: last-write-wins for
  concurrent Step edits. Documented as a known limitation; ETag /
  If-Match is a follow-up.
- **Calendar date for DueDate**: local-date semantics. No time zone is
  stored with DueDate. The owner picks "May 31" not "May 31 14:30
  UTC".

## Out of Scope

These items are intentionally excluded from Slice 1 so the scope stays
shippable. Each is captured here so future me can find the conscious
decision rather than guess.

- **Asset entity and asset-scoped Jobs**. Targeted at Slice 1b.
- **Recurrence**. `JobDefinition` + `JobOccurrence` shape is targeted
  at Slice 2; migration story for Slice 1's one-shot Jobs is deferred
  until that slice begins.
- **Job reopen after completion**. A Completed Job is final in Slice 1.
- **Property delete / Job delete**. Not exposed.
- **Per-user "auto-complete Job when all steps done" preference**.
  Captured as a future follow-up; today completion is always explicit.
- **Step Notes / Step CompletedBy**. Steps carry only description and
  completion state.
- **Property fields beyond Name** (address, photo, geolocation).
- **Additional OIDC providers beyond Google** (Facebook, X/Twitter,
  Microsoft). The `IIdentityProvider` abstraction is in scope so
  adding them later is a plugin, not a refactor.
- **CI/CD pipelines** (GitHub Actions). Tracked separately under
  `/polaris.devops`.
- **Production deployment topology** (AKS, ECS, single VM). Slice 1
  ships as Docker Compose on localhost.

## Open Questions

None blocking. The plan-phase will confirm:
- NextAuth vs. raw `next-auth/google` integration on the frontend.
- Whether to embed Steps in the Job document (chosen default) or
  store them in a separate collection (rejected default - Steps are
  invariant-tied to a Job).
- JWKS caching window for Google's signing keys.

## Tracker and References

- GitHub issue: https://github.com/arturzrk/home-maintenance/issues/1
- Constitution: `.polaris/memory/constitution.md`
- Architecture rules: `ARCHITECTURE.md`
- Future slices:
  - Slice 1b: introduce Asset entity, optional asset scope on Job.
  - Slice 2: introduce `JobDefinition` (template + recurrence) and
    `JobOccurrence` (instance with its own step states).
