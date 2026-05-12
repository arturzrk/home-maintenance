# Implementation Plan: 001-property-job-step

**Branch**: `main` (planning artifacts; implementation worktree created later via `/polaris.implement`)
**Date**: 2026-05-12
**Spec**: [spec.md](./spec.md)
**Tracker**: GitHub issue [#1](https://github.com/arturzrk/home-maintenance/issues/1)

## Summary

Deliver Slice 1 of the home-maintenance app: Properties + one-shot Jobs with
ordered Step checklists, secured by Google OIDC with ownership-based authz.
Implementation strictly follows the constitution's Clean Architecture
dependency rule (Domain -> Application -> Infrastructure -> API -> Frontend)
and the spec's explicit out-of-scope list (no Asset, no recurrence, no delete,
no reopen). Job aggregates embed Steps in a single MongoDB document; Property
is a separate aggregate; all queries are filtered by `ownerId`. Frontend uses
Next.js 15 App Router with Server Components by default and a NextAuth-driven
sign-in.

## Technical Context

- **Backend language/runtime**: C# 12 on .NET 9 (SDK 9.0.x). `nullable enable`
  and `ImplicitUsings` on every project.
- **Backend frameworks**: ASP.NET Core Minimal API, MongoDB.Driver 3.1.0,
  `Microsoft.AspNetCore.Authentication.JwtBearer` (for Google ID-token
  validation), `Microsoft.AspNetCore.OpenApi` (developer-facing OpenAPI doc).
- **Frontend language/runtime**: TypeScript 5.8 strict on Node 22 LTS,
  Next.js 15.3 (App Router), React 19, Tailwind v4.
- **Frontend frameworks**: NextAuth (Auth.js) for the OAuth2 / OIDC handshake
  with Google. Application code reads the ID token from the NextAuth session
  and forwards it to the API as a Bearer header.
- **Storage**: MongoDB 7 via MongoDB.Driver. Two collections (`properties`,
  `jobs`); Steps are embedded inside the Job document.
- **Testing**: xUnit + Shouldly + NSubstitute for unit tests; xUnit +
  Testcontainers (MongoDB) for integration tests; Jest + Testing Library
  for frontend tests.
- **Target platform**: Docker Compose on the developer machine. No
  non-local deployment in scope.
- **Project type**: Web application monorepo (backend + frontend in the same
  repo, no shared package layer in Slice 1).
- **Performance goals**: p95 < 500ms for typical CRUD; up to 10 concurrent
  users. Step ticking round-trip p95 < 500ms (SC-004).
- **Constraints**: ownership-based authz with default-deny; cross-owner
  access returns 404 (no enumeration leak); audit log is append-only and
  separated from data; ASCII punctuation only in markdown.
- **Scale**: ~10 concurrent users today; data volume small (<1k Jobs per
  user). Re-evaluate on growth.

## Constitution Check

Cross-reference each principle in `.polaris/memory/constitution.md`. Pass /
fail for Slice 1 implementation.

| Principle | Status | Notes |
|---|---|---|
| Start minimal; grow intentionally; test everything | PASS | Slice 1 ships the smallest user-visible flow. No placeholders; every new type has tests. |
| Dependency rule (Domain <- Application <- Infrastructure <- API; Frontend via API only) | PASS | New domain types in `HomeMaintenance.Domain`; new use cases in `HomeMaintenance.Application`; Mongo repositories in `HomeMaintenance.Infrastructure`; endpoints in `HomeMaintenance.API`. Frontend talks only to the API. |
| No leaking abstractions | PASS | New `MongoDocument` types stay in Infrastructure; DTOs in Application; Domain entities never serialised. |
| C# style: nullable, ImplicitUsings, record DTOs, sealed classes, no static helpers, Result<T> only when first needed | PASS | All Slice 1 use-case handlers return `Result<T>` so the API layer can translate failures uniformly (introducing it here is the first genuine need - JobAggregate.Complete rejects business-rule violations without exceptions). |
| Frontend style: TS strict, co-located components, Server Components default, typed API client | PASS | New pages under `frontend/src/app/`, shared components under `frontend/src/components/`, typed client at `frontend/src/lib/api-client.ts`. |
| Internal data tier | PASS | Only OwnerId + user-supplied free text + timestamps. No PHI, no payment, no PII regulated. |
| Audit logging (auth outcomes, authz denials, writes, completions, 1y retention, separate sink) | PASS | All events enumerated in `spec.md` "Audit Logging" section; sink defaults to `audit-trail/property-job-step.jsonl`. |
| AuthN/AuthZ: OIDC, ownership-based, default-deny | PASS | Google OIDC validates ID tokens; `OwnerId` value object wraps `sub`; every Application use case requires the caller to be the resource owner. |
| Performance: p95 < 500ms for CRUD | PASS (design) | Single-Mongo round-trip per use case; `ownerId` index ensures O(log n) lookups. Validated in integration tests. |
| Deployment: Docker Compose only today | PASS | No production wiring in scope. |
| Quality gates: dotnet test + jest + lint + encoding all green | PASS | CI already enforces all four in `.github/workflows/ci.yml`. |
| ASCII-only markdown | PASS | This plan and all artifacts authored in ASCII; CI validator catches regressions. |

No violations. Skipping the Complexity Tracking table.

## Project Structure

### Documentation (this feature)

```
polaris-specs/001-property-job-step/
|-- meta.json
|-- spec.md
|-- plan.md              <- this file
|-- research.md          <- Phase 0 output
|-- data-model.md        <- Phase 1 output
|-- quickstart.md        <- Phase 1 output
|-- contracts/           <- Phase 1 output (REST contracts)
|   |-- README.md
|   |-- properties.md
|   |-- jobs.md
|   `-- steps.md
|-- control-map.md
|-- checklists/
|   `-- requirements.md
`-- tasks/               <- populated by /polaris.tasks
```

### Source code (repository root)

This is a web-application monorepo. The slice adds files within the existing
projects; it does not create new top-level directories.

```
home-maintenance/
|-- backend/
|   |-- src/
|   |   |-- HomeMaintenance.Domain/
|   |   |   |-- Common/                       <- existing Entity, ValueObject
|   |   |   |-- Identity/
|   |   |   |   `-- OwnerId.cs                <- NEW value object
|   |   |   |-- Properties/
|   |   |   |   `-- Property.cs               <- NEW aggregate root
|   |   |   `-- Jobs/
|   |   |       |-- Job.cs                    <- NEW aggregate root
|   |   |       |-- Step.cs                   <- NEW child entity
|   |   |       `-- JobStatus.cs              <- NEW enum
|   |   |-- HomeMaintenance.Application/
|   |   |   |-- Common/
|   |   |   |   |-- Result.cs                 <- NEW (Result<T>)
|   |   |   |   |-- Errors.cs                 <- NEW (named error types)
|   |   |   |   `-- Interfaces/
|   |   |   |       |-- IPropertyRepository.cs   <- NEW
|   |   |   |       |-- IJobRepository.cs        <- NEW
|   |   |   |       |-- IIdentityProvider.cs     <- NEW (abstraction)
|   |   |   |       `-- IAuditLog.cs             <- NEW
|   |   |   |-- Properties/
|   |   |   |   |-- Commands/
|   |   |   |   |   |-- CreateProperty.cs        <- NEW use case
|   |   |   |   |   `-- RenameProperty.cs        <- NEW use case
|   |   |   |   |-- Queries/
|   |   |   |   |   |-- ListProperties.cs        <- NEW use case
|   |   |   |   |   `-- GetProperty.cs           <- NEW use case
|   |   |   |   `-- Dto/
|   |   |   |       |-- PropertyDto.cs           <- NEW record
|   |   |   |       `-- ...
|   |   |   `-- Jobs/
|   |   |       |-- Commands/                    <- NEW (12 use cases)
|   |   |       |-- Queries/                     <- NEW (2 use cases)
|   |   |       `-- Dto/                         <- NEW records
|   |   |-- HomeMaintenance.Infrastructure/
|   |   |   |-- DependencyInjection.cs           <- extend with auth + repos
|   |   |   |-- Auth/
|   |   |   |   |-- GoogleOidcIdentityProvider.cs   <- NEW
|   |   |   |   `-- LocalStubIdentityProvider.cs    <- NEW (dev only)
|   |   |   |-- Persistence/
|   |   |   |   |-- MongoDbContext.cs            <- existing
|   |   |   |   |-- PropertyRepository.cs        <- NEW
|   |   |   |   |-- JobRepository.cs             <- NEW
|   |   |   |   `-- Documents/
|   |   |   |       |-- PropertyDocument.cs      <- NEW
|   |   |   |       `-- JobDocument.cs           <- NEW (with embedded StepDocument)
|   |   |   `-- AuditLog/
|   |   |       `-- FileAuditLog.cs              <- NEW (JSONL writer)
|   |   `-- HomeMaintenance.API/
|   |       |-- Program.cs                       <- extend with auth + endpoints
|   |       |-- Endpoints/
|   |       |   |-- PropertyEndpoints.cs         <- NEW
|   |       |   `-- JobEndpoints.cs              <- NEW
|   |       `-- Middleware/
|   |           |-- AuthErrorTranslator.cs       <- NEW (Result -> HTTP)
|   |           `-- CorrelationIdMiddleware.cs   <- NEW
|   `-- tests/
|       |-- HomeMaintenance.Unit.Tests/
|       |   |-- Domain/Properties/                  <- NEW
|       |   |-- Domain/Jobs/                        <- NEW
|       |   `-- Application/                        <- NEW (handlers)
|       `-- HomeMaintenance.Integration.Tests/
|           |-- Infrastructure/
|           |   |-- ApiFactory.cs                <- existing, extend for auth
|           |   `-- AuthStub.cs                  <- NEW (issues test tokens)
|           |-- Properties/                      <- NEW (endpoint + repo tests)
|           |-- Jobs/                            <- NEW
|           `-- System/
|               `-- HealthCheckTests.cs          <- existing
|-- frontend/
|   `-- src/
|       |-- app/
|       |   |-- (auth)/signin/page.tsx           <- NEW (sign-in landing)
|       |   |-- properties/
|       |   |   |-- page.tsx                     <- NEW (list + create)
|       |   |   `-- [id]/
|       |   |       `-- page.tsx                 <- NEW (property detail + jobs)
|       |   |-- jobs/
|       |   |   `-- [id]/
|       |   |       `-- page.tsx                 <- NEW (job detail + checklist)
|       |   |-- api/auth/[...nextauth]/route.ts  <- NEW (NextAuth handler)
|       |   `-- layout.tsx                       <- extend with session provider
|       |-- components/
|       |   |-- PropertyCard.tsx                 <- NEW
|       |   |-- JobCard.tsx                      <- NEW
|       |   |-- StepCheckbox.tsx                 <- NEW (client component)
|       |   |-- CreatePropertyForm.tsx           <- NEW (client component)
|       |   |-- CreateJobForm.tsx                <- NEW (client component)
|       |   `-- CompleteJobButton.tsx            <- NEW (client component)
|       |-- lib/
|       |   |-- api-client.ts                    <- NEW (typed REST client)
|       |   |-- auth.ts                          <- NEW (NextAuth options)
|       |   `-- session.ts                       <- NEW (Server Component session helper)
|       `-- types/
|           |-- property.ts                      <- NEW
|           `-- job.ts                           <- NEW
`-- docker-compose.yml                            <- existing
```

**Structure Decision**: web-application monorepo with Clean Architecture for
the backend and Next.js App Router for the frontend. No new top-level
directories; every Slice 1 file is placed within an existing project's
established structure. Auth, identity, and audit-log primitives live in
new dedicated folders (`Auth/`, `Identity/`, `AuditLog/`) so future slices
can extend without disturbing domain code.

## Implementation Strategy

Slice 1 will be decomposed by `/polaris.tasks` into work packages following
the spec's user-story priority order. The expected shape (subject to the
tasks command):

- **WP01** - Auth + plumbing: `OwnerId` value object, `IIdentityProvider`
  abstraction, Google JWT validation, OIDC stub for dev, default-deny
  middleware, `Result<T>` pattern, `IAuditLog` interface, `FileAuditLog`
  implementation. Tests for token validation, default-deny.
- **WP02** - Property aggregate end-to-end (US1, US2): Domain `Property`,
  Application CreateProperty / ListProperties / GetProperty / RenameProperty
  use cases, Infrastructure `PropertyRepository`, API endpoints, frontend
  `/properties` page with create flow. Tests at every layer.
- **WP03** - Job aggregate end-to-end (US3, US4, US5): Domain `Job` + `Step`,
  Application CreateJob / CompleteJob / TickStep / UntickStep / GetJob /
  ListJobs use cases, Infrastructure `JobRepository`, API endpoints, frontend
  `/jobs/[id]` page with checklist + complete button. Tests at every layer.
- **WP04** - Step mutation and Job rename (US6, US7): AddStep, RemoveStep,
  ReorderSteps, EditStepDescription, RenameJob, SetJobDueDate, RenameProperty
  use cases and endpoints; frontend edit affordances.
- **WP05** - Hardening: full cross-owner integration test matrix, audit-log
  sink verification, completion-seal matrix, OWASP A01/A03/A07 checks, p95
  measurement against SC-004.

Each WP is independently shippable and includes its own tests; merging WP01
without WP02 leaves auth wired but no business endpoints, which is an
acceptable intermediate state.

## Open Risks

| Risk | Mitigation |
|---|---|
| Google JWKS endpoint unreachable in offline dev | The local stub identity provider bypasses Google entirely; only production hits the real JWKS endpoint. |
| Testcontainers Docker requirement | CI already verified Docker is available on `ubuntu-latest`. Local dev requires Docker Desktop (documented in README). |
| NextAuth API changes between minor versions | Pin `next-auth` to a specific patch version once chosen; cover sign-in/sign-out in Jest tests. |
| MongoDB embedded-Step document growth | A Job with 1000 steps would approach the 16 MB BSON limit at ~16 KB/step. Slice 1 enforces 500-char step descriptions; with reasonable step counts (<100) this is comfortable. Re-evaluate when reaching this threshold. |
| Concurrent edits to the same Job | Last-write-wins documented as a known limitation. ETag/If-Match is a planned follow-up. |

## Next Step

`/polaris.tasks` to break this plan into work packages with concrete prompts
under `polaris-specs/001-property-job-step/tasks/`.
