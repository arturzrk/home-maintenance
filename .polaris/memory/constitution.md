# home-maintenance Constitution

**Version**: 1.0.0
**Adopted**: 2026-05-10
**Status**: Active
**Source of truth for**: architectural decisions, security baseline, quality gates, governance

This constitution governs the home-maintenance project. It elevates the rules
already captured in [ARCHITECTURE.md](../../ARCHITECTURE.md) and adds the
cross-cutting concerns (security, audit, governance) that ARCHITECTURE.md does
not cover. Where ARCHITECTURE.md and this constitution overlap they MUST agree;
if they ever drift, this constitution wins and ARCHITECTURE.md is updated to
match.

## Project Identity

- **Name**: home-maintenance
- **Purpose**: A clean-architecture home maintenance tracking application. A
  single owner records and tracks the maintenance lifecycle of their property
  and assets. Personal-scale, opinionated, test-driven.
- **Primary language**: C# 12 on .NET 9 (backend)
- **Secondary language**: TypeScript 5.8 on Node 22+ (frontend)
- **Database**: MongoDB 7
- **Repository layout**: `backend/` (Clean Architecture solution),
  `frontend/` (Next.js 15 App Router), `docker-compose.yml` for local infra.

## Core Philosophy

> Start minimal. Grow intentionally. Test everything.

Every feature is added only when needed. No speculative code. Every new piece
of functionality is accompanied by tests before it is considered done.
Vertical slices (Domain -> Application -> Infrastructure -> API -> Frontend)
are the unit of growth.

## Technical Standards

### Languages and runtimes (Q1)
- **Backend**: .NET 9 SDK, C# 12. `nullable enable` and `ImplicitUsings` on
  for every project. ASP.NET Core Minimal API as the API style.
- **Frontend**: Next.js 15.3+ on the App Router, React 19, TypeScript 5.8 in
  strict mode (`"strict": true`). Server Components by default; opt into
  `'use client'` only when necessary.
- **Database driver**: `MongoDB.Driver` (official). MongoDB 7 in production
  and tests.
- **Local infra**: Docker Desktop with `docker compose` for MongoDB and full
  stack composition.

### Testing (Q2)
- **Backend unit tests** live in `backend/tests/HomeMaintenance.Unit.Tests`,
  use xUnit + Shouldly + NSubstitute, and exercise the Application and Domain
  layers in isolation.
- **Backend integration tests** live in
  `backend/tests/HomeMaintenance.Integration.Tests`, use xUnit +
  Testcontainers (MongoDB), and exercise Infrastructure + API against real
  dependencies.
- **Frontend tests** use Jest + Testing Library (`@testing-library/react`,
  `@testing-library/jest-dom`, `@testing-library/user-event`).
- **Coverage**: meaningful tests over high percentages. A feature is not
  complete until its tests are green.
- **Mandatory before merge**: `dotnet test` (both projects) and
  `npm --prefix frontend test`.

### Performance and scale (Q3)
- **Concurrent users**: targets up to ~10 concurrent users (personal-scale).
- **Latency budget**: p95 < 500ms for typical CRUD endpoints.
- **No throughput SLO** at this scale. Revisit performance targets via a
  constitution amendment before promoting the application beyond a single
  household / operator.

### Deployment target (Q4)
- **Today**: Docker Compose on the developer machine. The MongoDB +
  API + frontend topology in `docker-compose.yml` is the supported deployment
  surface.
- **Tomorrow**: any production target (AKS, ECS, single VM, etc.) MUST be
  revisited via a constitution amendment **before** the application is
  exposed beyond `localhost`. AuthN, audit log sink, and TLS termination MUST
  be designed before the change ships.

## Architectural Constraints

### Clean Architecture dependency rule
```
Domain  <-  Application  <-  Infrastructure
                ^                  ^
               API  <--------------+
```
- **Domain** depends on nothing.
- **Application** depends only on Domain.
- **Infrastructure** depends on Application (implements its interfaces).
- **API** depends on Application and Infrastructure (DI wiring only).
- **Frontend** never calls Infrastructure or Domain directly; it only calls
  the API.

### No leaking abstractions
- Database models (`MongoDocument`-style types) never leave the Infrastructure
  layer.
- API responses are always mapped to DTOs defined in the Application layer.
- Domain entities are never serialised directly to JSON.

### Code style (C# backend)
- Use `record` types for DTOs and Value Objects.
- Use `sealed` on classes that are not designed for inheritance.
- No `static` helper classes; favour extension methods or injected services.
- No exceptions for control flow; use a `Result<T>` pattern (introduced when
  first needed, not pre-emptively).

### Code style (frontend)
- TypeScript strict mode is non-negotiable.
- Components are co-located with their feature; shared components live in
  `frontend/src/components`.
- API calls go through the typed client in `frontend/src/lib/api-client.ts`.

### Vertical-slice growth (mandatory order)
1. Add the domain entity / value object in `Domain` (only if it is a new
   concept).
2. Add the repository interface in `Application/Common/Interfaces`.
3. Add the use-case command/query + handler in `Application`.
4. Add the MongoDB repository implementation in `Infrastructure`.
5. Add the API endpoint in `API`.
6. Add the frontend page/component in `frontend/src/app`.
7. Write unit tests for the handler.
8. Write integration tests for the repository + endpoint.
9. Open a PR; do not merge until tests are green.

## Security and Audit Baseline

### Data classification (Q6)
- **Tier**: Internal.
- **Stored data**: home/asset metadata, maintenance task history, user
  account references, scheduling notes. No payment data, no PHI, no
  government identifiers.
- **Implication**: encryption-at-rest is desirable but not yet mandated;
  TLS-in-transit is mandatory the moment the app leaves `localhost`.
- If the data tier ever rises (e.g. payment, health), this section MUST be
  updated via constitution amendment **before** the higher-tier data is
  collected.

### Audit logging (Q7)
The application MUST emit append-only audit log records for the following
events:

| Event class | Examples |
| --- | --- |
| Authentication outcomes | sign-in success, sign-in failure, token refresh failure |
| Authorization denials | any request rejected by an authz check |
| Writes and deletes against user-owned data | create / update / delete of homes, assets, tasks, schedules |
| Configuration changes | feature flags, integration toggles, role grants |
| Admin actions | impersonation, data export, retention overrides |

- **Retention**: 1 year minimum.
- **Sink**: locally, append-only files under `audit-trail/` (project root,
  gitignored). In any non-local deployment, a managed sink (Azure Monitor,
  Splunk, S3 + Object Lock, or equivalent) MUST be wired before launch.
- **Storage rule**: audit records MUST NOT be stored in the same MongoDB
  collection as the entities they describe.

### AuthN / AuthZ (Q8)
- **Authentication**: OIDC / OAuth2. The principal source is the verified
  ID token (or session derived from it). API key auth is permitted only for
  service-to-service callers, never for end users.
- **Authorization**: ownership-based. Each user owns their own homes,
  assets, and tasks; access is granted only when the principal matches the
  resource owner (or has an explicitly modelled admin role).
- **Default-deny**: if a request does not match a permit rule, it is
  denied. Every endpoint MUST be authenticated unless explicitly listed as
  public (e.g. `/health`).
- **Local development**: a stub identity provider is acceptable; production
  MUST use a real OIDC provider configured via environment variables, never
  hard-coded secrets.

### Threat surface (Q9)
Untrusted input enters the system through:

| Boundary | Owner | Notes |
| --- | --- | --- |
| Public HTTP API (`/api/...`) | API project | All requests pass through AuthN + ownership authz |
| Health endpoints (`/health`) | API project | Public, MUST NOT leak version, secrets, or environment |
| Frontend public pages | frontend | Server Components fetch via the typed API client; no direct DB access |
| Docker Compose internal network | infra | Trusted boundary in dev; production deployment must isolate |

- **Multi-tenant?** Single-owner today, but the data model already keys on
  user identity, so the ownership rule above doubles as future-proofing.
- The seed of the threat model lives at `security/threat-model.md` (create
  on first deployment beyond localhost).

## Code Quality

### PR requirements (solo-friendly)
- **Approvals**: self-review allowed. The author MUST sign off in the PR
  description that they ran the full test suite locally.
- **CI must be green** before merge: `dotnet test` (unit + integration), the
  frontend Jest suite, the lint step (`npm --prefix frontend run lint`), and
  the Polaris encoding validator.
- **No force-push to `main`**. Feature branches only.
- **Pre-commit hooks** are not bypassed (`--no-verify` is forbidden). If a
  hook fails, fix the issue and commit again.

### Review checklist
Whether self-reviewing or being reviewed by a teammate, confirm:
- The change adds tests, or has a written reason it cannot.
- Dependency rule is preserved (no Infrastructure references in
  Application or Domain).
- No DB document types escape Infrastructure; no Domain entities serialise
  directly to JSON.
- DTOs are records, classes are `sealed` unless inheritance is intended.
- Frontend changes do not bypass the typed API client.

### Quality gates (must pass before merge)
- All unit tests green.
- All integration tests green (Docker available).
- Frontend Jest suite green.
- Encoding validator (`polaris validate-encoding`) green: UTF-8 only, no
  em dashes, no smart quotes.
- Linter (`npm --prefix frontend run lint`) green.

### Documentation standards
- Public APIs (use-cases, endpoints) include XML doc comments on the
  command/query record and the handler.
- Architectural decisions that change a rule in this constitution or
  ARCHITECTURE.md are recorded as ADRs under `docs/adr/NNNN-title.md`.
- README and ARCHITECTURE.md are updated alongside any change that affects
  setup or the dependency rule.

## Tribal Knowledge

- **No placeholders**: if a layer does not need code yet, it does not exist
  yet. Empty methods, TODO classes, and "for future use" abstractions are
  rejected at review.
- **Result-pattern only when needed**: do not introduce `Result<T>`
  pre-emptively; introduce it the first time a control-flow case requires
  it, and migrate the rest only when the pattern proves itself.
- **Vertical slice or nothing**: changes that touch only one layer
  (e.g. a Domain class with no Application or API consumer) are a smell
  unless explicitly justified.
- **Tests describe behavior, not structure**: name tests for the scenario,
  not the method (`Should_reject_overdue_task_completion_for_non_owner`,
  not `CompleteTask_returns_Failure`).

## Governance

- **Amendments**: changes to this constitution are made via PR that touches
  this file plus, where relevant, ARCHITECTURE.md. The PR description MUST
  explain the trigger (new requirement, retro from an incident, change in
  deployment target).
- **Compliance validation**: every PR review confirms the change does not
  silently violate this constitution. Reviewers may block on constitution
  drift.
- **Exceptions**: case-by-case, documented in the PR description and (if
  the exception is durable) added as an ADR. Time-boxed exceptions MUST
  state their expiry date.
- **Versioning**: this file is semver. Backwards-incompatible changes
  (a stricter rule, a new mandatory phase) bump the major version. Adding
  guidance bumps the minor version. Editorial fixes bump the patch.

## Model Selection

Claude Code operates in two phases within any Polaris workflow. Use the right model for each.

### Plan Phase -> Opus

When reading specs, synthesizing the architecture, deciding what to build and how it connects:
use claude-opus-4-6. Planning mistakes compound through every line of code that follows.
Opus-level reasoning here is insurance, not indulgence.

Polaris planning commands: /polaris.specify, /polaris.plan, /polaris.tasks

### Implementation Phase -> Sonnet

When writing code, calling tools, executing against a defined plan:
use claude-sonnet-4-6. Implementation is where call volume lives - this is where savings compound.

Polaris implementation commands: /polaris.implement, /polaris.autopilot, /polaris.runtests

### When Implementation Hits Unexpected Complexity

Do not reason through ambiguity or contradiction at Sonnet level. Stop the step, describe what
is unexpected, and surface it:

REPLAN NEEDED: [one sentence - what was unexpected]
SPEC REFERENCE: [which spec/section the assumption came from]
OPTIONS: [2-3 ways to resolve, with tradeoffs]

Re-enter the plan phase (Opus) with the updated context before continuing.
Never self-escalate the model mid-implementation.

## License Compliance

- **Allowed inbound licenses** for runtime / build dependencies:
  Apache-2.0, BSD-2-Clause, BSD-3-Clause, MIT, ISC, PSF-2.0, Unlicense,
  0BSD, CC0-1.0.
- **Prohibited inbound licenses**: LGPL, AGPL, GPL (any version), SSPL,
  BSL, CPAL, EUPL, MPL-2.0.
- A new dependency MUST be license-checked before it is added. The PR
  description records the license and the source of that determination
  (package metadata, upstream repo, OSI listing).

## Out of Scope (Today)

- **Aptean branding**: the operator opted out at onboarding. Frontend
  styling stays as-is. Re-enable later via `/polaris.skill aptean-brand`
  if the project ever ships under the Aptean umbrella.
- **CI/CD pipelines**: none configured today. When a deployment target is
  chosen, run `/polaris.devops` to scaffold pipelines and revisit the
  Deployment section above.
- **Multi-tenant isolation beyond ownership**: not in scope at
  personal-scale. Revisit before any tenant-shared deployment.
