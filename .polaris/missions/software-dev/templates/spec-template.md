# Feature Specification: [FEATURE NAME]
<!-- Replace [FEATURE NAME] with the confirmed friendly title generated during /polaris.specify. -->

**Feature Branch**: `[###-feature-name]`  
**Created**: [DATE]  
**Status**: Draft  
**Input**: User description: "$ARGUMENTS"

## User Scenarios & Testing *(mandatory)*

<!--
  IMPORTANT: User stories should be PRIORITIZED as user journeys ordered by importance.
  Each user story/journey must be INDEPENDENTLY TESTABLE - meaning if you implement just ONE of them,
  you should still have a viable MVP (Minimum Viable Product) that delivers value.
  
  Assign priorities (P1, P2, P3, etc.) to each story, where P1 is the most critical.
  Think of each story as a standalone slice of functionality that can be:
  - Developed independently
  - Tested independently
  - Deployed independently
  - Demonstrated to users independently
-->

### User Story 1 - [Brief Title] (Priority: P1)

[Describe this user journey in plain language]

**Why this priority**: [Explain the value and why it has this priority level]

**Independent Test**: [Describe how this can be tested independently - e.g., "Can be fully tested by [specific action] and delivers [specific value]"]

**Acceptance Scenarios**:

1. **Given** [initial state], **When** [action], **Then** [expected outcome]
2. **Given** [initial state], **When** [action], **Then** [expected outcome]

---

### User Story 2 - [Brief Title] (Priority: P2)

[Describe this user journey in plain language]

**Why this priority**: [Explain the value and why it has this priority level]

**Independent Test**: [Describe how this can be tested independently]

**Acceptance Scenarios**:

1. **Given** [initial state], **When** [action], **Then** [expected outcome]

---

### User Story 3 - [Brief Title] (Priority: P3)

[Describe this user journey in plain language]

**Why this priority**: [Explain the value and why it has this priority level]

**Independent Test**: [Describe how this can be tested independently]

**Acceptance Scenarios**:

1. **Given** [initial state], **When** [action], **Then** [expected outcome]

---

[Add more user stories as needed, each with an assigned priority]

### Edge Cases

<!--
  ACTION REQUIRED: The content in this section represents placeholders.
  Fill them out with the right edge cases.
-->

- What happens when [boundary condition]?
- How does system handle [error scenario]?

## Requirements *(mandatory)*

<!--
  ACTION REQUIRED: The content in this section represents placeholders.
  Fill them out with the right functional requirements.
-->

### Functional Requirements

- **FR-001**: System MUST [specific capability, e.g., "allow users to create accounts"]
- **FR-002**: System MUST [specific capability, e.g., "validate email addresses"]  
- **FR-003**: Users MUST be able to [key interaction, e.g., "reset their password"]
- **FR-004**: System MUST [data requirement, e.g., "persist user preferences"]
- **FR-005**: System MUST [behavior, e.g., "log all security events"]

*Example of marking unclear requirements:*

- **FR-006**: System MUST authenticate users via [NEEDS CLARIFICATION: auth method not specified - email/password, SSO, OAuth?]
- **FR-007**: System MUST retain user data for [NEEDS CLARIFICATION: retention period not specified]

### Key Entities *(include if feature involves data)*

- **[Entity 1]**: [What it represents, key attributes without implementation]
- **[Entity 2]**: [What it represents, relationships to other entities]

## Non-Functional Requirements *(mandatory)*

<!--
  ACTION REQUIRED: Every feature must answer the resource-lifecycle and runtime
  posture questions below before implementation. Inherit defaults from the
  project constitution's "Non-Functional Baseline" section
  (.polaris/memory/constitution.md). If this feature changes the lifecycle
  posture, throughput target, or resource budget relative to the baseline,
  document the delta here.

  Greenfield apps consistently leak DB connections, sessions, and threads
  because the lifecycle questions were never answered explicitly. Answer them
  now so the plan and code reviewers can verify the implementation matches.
-->

### Resource Lifecycle

- **Database connections**: [reused via pool | per-request short-lived | none]
  - Pool implementation: [SQLAlchemy `create_async_engine` | HikariCP | `pg.Pool` | Django CONN_MAX_AGE | other]
  - Pool size / max_overflow: [e.g., size=10, overflow=20] - sized for expected concurrency, NOT default zeros
  - Connection acquired per: [request | transaction | task] - and released by: [`with` block | `try/finally` | framework lifespan hook]
- **ORM / DB sessions**: [scoped per-request | scoped per-task | application-wide singleton]
  - Session disposal: [explicit `close()` in finally | dependency-injection lifespan | middleware]
- **HTTP clients**: [singleton across the process (REUSED) | one per request (anti-pattern, justify)]
  - For Python: `httpx.AsyncClient` / `requests.Session` instantiated once at startup, closed at shutdown.
  - For Node: `undici.Pool` / `axios.create()` singleton; outbound `keepAlive: true`.
  - For Java: `OkHttpClient` / `RestClient` as a Spring bean (singleton scope).
- **Background tasks / threads**: [N/A | bounded thread pool | asyncio task group | message queue consumer]
  - Lifecycle: [created on startup | spawned per-request (justify, must be bounded)]
  - Shutdown: [drained on SIGTERM with timeout | daemon threads | none (anti-pattern)]
- **Caches / in-memory state**: [N/A | bounded LRU | TTL-based | unbounded (anti-pattern)]
- **External resource handles** (file descriptors, sockets, message-queue consumers, gRPC channels): [list each + how it is closed]

### Graceful Shutdown

- **Signal handling**: [SIGTERM/SIGINT handler installed | framework default (e.g. Uvicorn lifespan, Spring Boot graceful shutdown) | none (anti-pattern)]
- **Drain timeout**: [e.g., "30s to finish in-flight requests, then force exit"]
- **Shutdown order**: [1. stop accepting new requests, 2. drain in-flight, 3. close DB pool, 4. flush logs / audit sink, 5. exit]
- **Liveness/readiness endpoints**: [readiness flips to NotReady at shutdown start | none]

### Runtime Posture

- **Expected concurrency**: [e.g., 100 RPS sustained, 500 RPS peak, 50 in-flight long-poll connections]
- **Latency budget (p50 / p95 / p99)**: [e.g., 50ms / 200ms / 500ms - cite the SLO]
- **Throughput target**: [e.g., 10k events/min for the queue consumer]
- **Resource budget**: [memory ceiling, CPU target, file-descriptor limit]
- **Backpressure strategy**: [bounded queue + 503 | drop-oldest | block (anti-pattern)]

### Observability for Lifecycle

- **Connection-pool metrics exposed**: [pool size, in-use, waiters, wait time histogram | none]
- **Background-task metrics**: [queue depth, in-flight, p99 task duration | none]
- **Leak detection**: [pool exhaustion alarm threshold | thread-count alarm | none]
- **Open-file-descriptor monitoring**: [yes / no - required if the feature opens many sockets or files]

## Success Criteria *(mandatory)*

<!--
  ACTION REQUIRED: Define measurable success criteria.
  These must be technology-agnostic and measurable.
-->

### Measurable Outcomes

- **SC-001**: [Measurable metric, e.g., "Users can complete account creation in under 2 minutes"]
- **SC-002**: [Measurable metric, e.g., "System handles 1000 concurrent users without degradation"]
- **SC-003**: [User satisfaction metric, e.g., "90% of users successfully complete primary task on first attempt"]
- **SC-004**: [Business metric, e.g., "Reduce support tickets related to [X] by 50%"]