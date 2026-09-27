# Project summary

## Purpose

Maintained House (repository name: `home-maintenance`) is a home-maintenance
tracking application. Owners organise properties, the assets within them,
one-off maintenance work, and recurring maintenance schedules.

## Architecture

The backend follows Clean Architecture:

```text
Domain <- Application <- Infrastructure
                 ^              ^
                 API <----------+
```

- `backend/src/HomeMaintenance.Domain`: aggregates and value objects.
- `backend/src/HomeMaintenance.Application`: use cases, DTOs, and ports.
- `backend/src/HomeMaintenance.Infrastructure`: MongoDB persistence,
  authentication, email, audit logging, and background services.
- `backend/src/HomeMaintenance.API`: .NET minimal API endpoints and DI wiring.
- `frontend`: Next.js application, using server-side API calls and server
  actions.

## Core capabilities

- Owner-scoped properties.
- Assets belonging to properties.
- One-off jobs with ordered checklist steps and completion rules.
- Recurring job definitions which produce job occurrences on a schedule.
- Notification preferences and daily due/overdue email digests.
- Public landing, privacy, terms, and user-manual pages.

## Technology

- .NET 9 minimal API
- MongoDB via `MongoDB.Driver`
- Next.js 15, React 19, TypeScript, Tailwind CSS
- NextAuth with Google OIDC
- Docker Compose for local MongoDB, API, and frontend
- xUnit/Testcontainers for backend tests; Jest and Playwright for frontend

## Security model

Google OIDC's verified `sub` claim identifies an owner. API data access is
ownership-scoped and cross-owner lookups return 404 to avoid enumeration.
Development supports an explicitly gated token stub; production refuses to
start if that stub is enabled. Authenticated writes emit append-only audit
events in local development.

## Operational model

Health probes are available at `/health`, `/liveness`, `/readiness`, and
`/detailed`. CI builds and tests both applications, including Mongo-backed
Playwright E2E tests. The backend deploys to Azure App Service and the
frontend deploys through Vercel's Git integration.

## Documentation and feature history

`ARCHITECTURE.md` is the main project-rules document. `polaris-specs/` records
the specifications, work packages, contracts, and acceptance coverage for
features 001 through 011. The latest implemented feature set adds reminder
preferences and daily email digests.
