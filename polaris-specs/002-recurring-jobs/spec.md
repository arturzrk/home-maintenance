# Feature Specification: Recurring Jobs (Slice 2)

**Feature Branch**: `002-recurring-jobs`
**Created**: 2026-05-29
**Status**: Draft
**Tracker**: ---
**Input**: Recurring maintenance job definitions with schedule-driven and manual job generation.

## Summary

A homeowner creates a **JobDefinition** against one of their Properties, giving it a name, an ordered list of step description templates, and a **ScheduleDefinition** (a cadence unit and an integer multiplier, e.g. every 3 months). A background service generates concrete **Job** instances from each active definition up to 3 months in advance, so the homeowner always has a visible plan of upcoming work without any manual effort. The homeowner can also manually generate the next occurrence at any time. Generated jobs behave identically to Slice 1 one-shot jobs but carry a `JobDefinitionId` reference back to their source. Editing a definition's steps is non-destructive --- only future generations pick up the change.

## User Scenarios and Testing *(mandatory)*

### User Story 1 --- Create a JobDefinition with a schedule (Priority: P1)

A signed-in homeowner opens one of their Properties and creates a recurring job definition: they provide a name, an ordered list of step descriptions, and a schedule (e.g. "every 3 months starting 2026-06-01"). On save, the definition is stored and the system immediately generates the first batch of upcoming job instances (up to the 3-month horizon).

**Independent Test**: Sign in, open a Property, create a JobDefinition "Service boiler" with three steps and a schedule of "every 3 months" starting today. Verify the definition appears in the Property view and that at least one concrete Job has been generated with the matching name, steps, and a due date matching the schedule.

**Acceptance Scenarios**:

1. **Given** a signed-in user with a Property, **When** they submit a JobDefinition with name "Service boiler", steps ["Shut off gas", "Drain system", "Replace filter"], and schedule every 3 months starting 2026-06-01, **Then** the definition is stored and at least one Job is generated with due date 2026-06-01, status Active, and steps matching the template.
2. **Given** a signed-in user, **When** they submit a JobDefinition with an empty name, **Then** creation is rejected with a validation error.
3. **Given** a signed-in user, **When** they submit a JobDefinition with multiplier X = 0 or negative, **Then** creation is rejected with a validation error.
4. **Given** user A has Property X, **When** user B attempts to create a JobDefinition against Property X, **Then** the request is rejected with a 404.

---

### User Story 2 --- See upcoming scheduled jobs (Priority: P1)

A homeowner opens their job list and can see future jobs generated from active definitions, giving a plan of upcoming maintenance work.

**Independent Test**: Create a JobDefinition "Monthly filter check" every 1 month starting this month. Verify that the job list for the Property shows jobs generated for the next 3 months, each with the correct due date.

**Acceptance Scenarios**:

1. **Given** an active JobDefinition with monthly cadence and start date this month, **When** the homeowner lists jobs for the Property, **Then** they see generated jobs for the current month and up to 3 months ahead, each with the correct due date and status Active.
2. **Given** two active JobDefinitions on the same Property, **When** the homeowner lists jobs, **Then** generated instances from both definitions appear, each carrying the correct `JobDefinitionId`.
3. **Given** a completed one-shot job (no `JobDefinitionId`), **When** it appears in the list alongside generated jobs, **Then** both are rendered correctly and the one-shot job has no schedule attribution.

---

### User Story 3 --- Background scheduler generates jobs automatically (Priority: P1)

Without any user action, the system generates upcoming jobs as time passes and the horizon rolls forward.

**Independent Test**: Create a JobDefinition with daily cadence starting today. Fast-forward the system clock by 2 months (or use a test hook). Trigger the scheduler. Verify that new jobs have been generated for dates now within the 3-month window that were not generated before.

**Acceptance Scenarios**:

1. **Given** an active JobDefinition, **When** the background scheduler runs, **Then** all occurrences within the rolling 3-month window that do not yet have a generated Job are created.
2. **Given** a Job already generated for a given occurrence date, **When** the scheduler runs again, **Then** no duplicate Job is created for that occurrence.
3. **Given** a JobDefinition with an end date in the past, **When** the scheduler runs, **Then** no new jobs are generated for that definition.

---

### User Story 4 --- Manually generate the next occurrence (Priority: P2)

A homeowner can explicitly trigger generation of the next job occurrence from the JobDefinition view --- for example, after completing a job ahead of schedule and wanting to start the next one immediately.

**Independent Test**: Create a JobDefinition "Quarterly service" every 3 months. Complete the first generated job. Invoke "Generate next" from the definition view. Verify a new job is created with the next occurrence date (even if it falls outside the automatic 3-month horizon).

**Acceptance Scenarios**:

1. **Given** an active JobDefinition with a next occurrence date in the future, **When** the owner invokes "Generate next", **Then** a new Job is created immediately with that occurrence's due date, steps copied from the current template, and the `JobDefinitionId` set.
2. **Given** that the next occurrence already has a generated Job, **When** the owner invokes "Generate next", **Then** the request is rejected with a business rule error (no duplicate).
3. **Given** user B, **When** they attempt "Generate next" for a JobDefinition owned by user A, **Then** a 404 is returned.

---

### User Story 5 --- Edit a JobDefinition (Priority: P2)

A homeowner can rename a JobDefinition, edit its step templates, or change its schedule. Edits are non-destructive: already-generated jobs keep their snapshot.

**Independent Test**: Create a JobDefinition with three steps. Generate one job. Edit the definition to add a fourth step. Verify the already-generated job still has three steps. Generate the next occurrence. Verify the new job has four steps.

**Acceptance Scenarios**:

1. **Given** a JobDefinition with a generated Job, **When** the owner adds a step to the definition, **Then** the existing generated Job is unchanged and the next generated Job has the additional step.
2. **Given** a JobDefinition, **When** the owner changes the schedule from monthly to quarterly, **Then** subsequent scheduler runs use the new cadence; already-generated jobs are not deleted or moved.
3. **Given** a JobDefinition, **When** the owner submits an empty name, **Then** the update is rejected.

---

### Edge Cases

- **Horizon boundary**: An occurrence whose due date falls exactly on the boundary date (today + 3 months) is included.
- **Start date in the past**: The scheduler generates only occurrences from today forward within the horizon; it does not back-fill historical occurrences.
- **Multiplier = 1**: Every 1 Day / 1 Week / 1 Month / 1 Year are valid and treated as daily / weekly / monthly / yearly.
- **Cross-owner enumeration**: Any request referencing a `JobDefinitionId` owned by another user returns 404, not 403.
- **Definition with no steps**: A JobDefinition may be created with an empty step list. Generated jobs will have an empty checklist and cannot be completed until steps are added directly to the job while Active.
- **Scheduler idempotency**: The scheduler may be invoked multiple times in rapid succession. It MUST never create duplicate jobs for the same definition + occurrence date.
- **Month-end clamp**: "Every 1 Month" from 2026-01-31 produces 2026-02-28 (clamped to last day of month), not an error.

## Requirements *(mandatory)*

### Functional Requirements

**JobDefinition management**
- **FR-101**: A user MUST be able to create a JobDefinition against one of their Properties with: Name (non-empty, trimmed, max 200 characters), an ordered list of step description templates (may be empty; each max 500 characters), and a ScheduleDefinition. The created definition's Owner matches the caller.
- **FR-102**: A user MUST be able to list their JobDefinitions, optionally filtered by PropertyId.
- **FR-103**: A user MUST be able to read a single JobDefinition by id, including its step templates and schedule.
- **FR-104**: A user MUST be able to rename a JobDefinition (validation per FR-101) and edit its step templates (add, remove, reorder, edit description --- same rules as Slice 1 steps).
- **FR-105**: A user MUST be able to change the ScheduleDefinition of a JobDefinition. The change takes effect for future scheduler runs; no already-generated jobs are modified or deleted.
- **FR-106**: JobDefinition deletion is out of scope for Slice 2.

**ScheduleDefinition**
- **FR-107**: A ScheduleDefinition MUST consist of a cadence unit (Day, Week, Month, Year) and a positive integer multiplier X (≥ 1). Both fields are required.
- **FR-108**: The ScheduleDefinition MUST include a start date (ISO-8601 calendar date, no time component). This is the anchor date for all occurrence computations.
- **FR-109**: The ScheduleDefinition MAY include an optional end date (DateOnly). If set, no occurrences are generated after the end date. End date MUST be strictly after start date.

**Job generation --- background**
- **FR-110**: A background service MUST run at least once every 24 hours and attempt job generation for all active JobDefinitions.
- **FR-111**: For each active JobDefinition, the service MUST generate one Job per occurrence whose due date falls within the rolling horizon (today inclusive to today + 3 months inclusive) and for which no Job with the same `JobDefinitionId` and `DueDate` already exists.
- **FR-112**: Each generated Job MUST have: Name copied from the JobDefinition name, Steps copied from the JobDefinition's StepTemplates at generation time (snapshot), DueDate = occurrence date, Status = Active, Owner = JobDefinition owner, PropertyId = JobDefinition property, and `JobDefinitionId` set.
- **FR-113**: The background service MUST be idempotent: running it multiple times for the same window MUST NOT produce duplicate jobs for the same definition + occurrence date.
- **FR-114**: If a JobDefinition's ScheduleDefinition has an EndDate and that date has passed, the service MUST NOT generate further jobs for that definition.

**Job generation --- manual**
- **FR-115**: A user MUST be able to invoke "Generate next occurrence" for one of their active JobDefinitions.
- **FR-116**: The "next occurrence" is the earliest occurrence date strictly after the latest already-generated Job's DueDate for that definition (or the start date if no jobs have been generated yet).
- **FR-117**: If the computed next occurrence already has a generated Job (DueDate match), the request MUST be rejected with a business rule error.

**Job behaviour**
- **FR-118**: A generated Job behaves identically to a Slice 1 one-shot Job in all respects. Mutations on the generated Job do not affect the JobDefinition or other generated Jobs.
- **FR-119**: `JobDefinitionId` on a generated Job is immutable after creation.
- **FR-120**: One-shot Jobs (JobDefinitionId = null) are unaffected by this slice.

**Authorization**
- **FR-121**: A JobDefinition MUST be readable and mutable only by its Owner. Any cross-owner or cross-property request returns 404.
- **FR-122**: The system MUST verify that any PropertyId supplied during JobDefinition creation resolves to a Property owned by the caller; otherwise 404.

**API conventions**
- **FR-123**: All Slice 1 API conventions (FR-031, FR-032) apply to new endpoints.

### Key Entities

- **JobDefinition** (aggregate root): id, OwnerId (immutable), PropertyId (immutable), Name, ordered list of StepTemplates, ScheduleDefinition.
- **StepTemplate** (child of JobDefinition): Order (int, contiguous from 0), Description (max 500 chars).
- **ScheduleDefinition** (value object embedded in JobDefinition): CadenceUnit (Day|Week|Month|Year), Multiplier (int ≥ 1), StartDate (DateOnly), optional EndDate (DateOnly).
- **Job** (extended from Slice 1): adds nullable `JobDefinitionId` (string). Null = one-shot. Non-null = generated.

## Audit and Security *(mandatory)*

### Data Classification

- **Data tier**: Internal (same as Slice 1).
- **New data in scope**: JobDefinition name, step template descriptions, schedule parameters. All user-supplied free text; no PII beyond OwnerId.

### Audit Logging

New events appended to the existing audit sink:
- `job_definition.created` --- actor, target=definitionId, propertyId, name, step_count, schedule, correlation_id
- `job_definition.renamed` --- actor, target, old_name, new_name, correlation_id
- `job_definition.schedule_changed` --- actor, target, old_schedule, new_schedule, correlation_id
- `job_definition.step_template_mutated` --- actor, target, mutation_type (added|removed|reordered|edited), correlation_id
- `job.generated` --- actor=system (scheduler) or ownerId (manual), trigger=scheduler|manual, target=jobId, definition_id, occurrence_date, correlation_id

### AuthN / AuthZ

- Same Google OIDC validation as Slice 1. All new endpoints require authentication.
- The background scheduler runs as a system actor with no user token; it operates across all owners' definitions. Audit events use actor=`system`.
- `JobDefinitionId` on a generated Job is set only by the service layer, never from user input.

### Threat Surface

- All Slice 1 mitigations carry forward.
- Scheduler idempotency (FR-113) is a correctness and integrity requirement: duplicate jobs would corrupt the user's maintenance record.
- The scheduler's internal query (all active definitions) runs with elevated access; it MUST NOT be exposed as a public API endpoint.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-101**: A homeowner with an active monthly JobDefinition always sees at least 1 upcoming generated job in their list without any manual action.
- **SC-102**: Running the background scheduler twice for the same window produces exactly 0 duplicate jobs (idempotency, verified by integration test).
- **SC-103**: "Generate next" p95 response time < 500ms.
- **SC-104**: Editing a JobDefinition's step templates does not modify any already-generated Job --- verified by step count comparison before and after edit.
- **SC-105**: Zero cross-owner data leakage for all new endpoints (same matrix pattern as SC-005).
- **SC-106**: All new mutating endpoints reject anonymous callers with 401.

## Assumptions

- **No back-fill**: The scheduler does not generate historical occurrences. Only today-forward occurrences within the horizon are generated.
- **Occurrence anchor**: Occurrences are start_date + N x interval for N = 0, 1, 2, .... N=0 gives the start date itself.
- **Month-end clamping**: When adding months/years results in a day that doesn't exist (e.g. Jan 31 + 1 month), clamp to the last valid day of the target month.
- **IHostedService**: The background scheduler is implemented as a .NET `BackgroundService` (IHostedService), not a separate process or external cron job.
- **No pause/archive in Slice 2**: All JobDefinitions are implicitly Active. Pausing and archiving are deferred.
- **No per-occurrence override**: Users cannot customise a specific future occurrence before generation.

## Out of Scope

- **JobDefinition deletion, pausing, archiving** --- deferred.
- **Per-occurrence overrides**.
- **Historical back-fill**.
- **Push notifications / due-date reminders**.
- **Calendar or timeline view** --- the job list with due dates is sufficient.
- **Cron expressions or fully custom intervals**.
- **Asset-scoped JobDefinitions** --- deferred until Slice 1b.

## Open Questions

None blocking. Plan phase will confirm:
- Whether occurrence date computation lives in the Domain layer or a dedicated OccurrenceCalculator service.
- `PeriodicTimer` vs. timer-based `BackgroundService` for the scheduler loop.
- Index strategy for the "all active definitions needing generation" query.
- Whether `JobDefinitionId` is added to the existing `jobs` collection or kept as a separate field with a sparse index.