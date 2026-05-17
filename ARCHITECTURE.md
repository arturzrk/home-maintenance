# Home Maintenance Software - Architecture & Project Rules

## Core Philosophy

> **Start minimal. Grow intentionally. Test everything.**

Every feature is added only when needed. No speculative code. Every new piece of
functionality is accompanied by tests before it is considered done.

---

## Project Rules

### 1. Minimal Working Set First
- The repository starts with connected components and no business logic.
- Features are added one at a time, in vertical slices (Domain → Application → Infrastructure → API → Frontend).
- No "placeholder" implementations - if it's not needed yet, it doesn't exist.

### 2. Test-Driven Growth
- Unit tests live in `HomeMaintenance.Unit.Tests` and test the Application and Domain layers in isolation.
- Integration tests live in `HomeMaintenance.Integration.Tests` and test Infrastructure + API against real dependencies (MongoDB via TestContainers).
- A feature is not complete until its tests pass.
- Test coverage is a signal, not a target - favour meaningful tests over high percentages.

### 3. Dependency Rule (Clean Architecture)
```
Domain  ←  Application  ←  Infrastructure
                ↑                  ↑
               API  ←──────────────┘
```
- **Domain** depends on nothing.
- **Application** depends only on Domain.
- **Infrastructure** depends on Application (implements its interfaces).
- **API** depends on Application and Infrastructure (DI wiring only).
- The Frontend never calls Infrastructure or Domain directly - only through the API.

### 4. No Leaking Abstractions
- Database models (`MongoDocument`) never leave the Infrastructure layer.
- API responses are always mapped to DTOs defined in the Application layer.
- Domain entities are never serialised directly to JSON.

### 5. Code Style (C# backend)
- `nullable enable` and `ImplicitUsings` are on for all projects.
- Use `record` types for DTOs and Value Objects.
- Use `sealed` on classes that are not designed for inheritance.
- No `static` helper classes - favour extension methods or injected services.
- No exceptions for control flow - use a `Result<T>` pattern (added when first needed).

### 6. Code Style (Frontend)
- TypeScript strict mode (`strict: true`).
- Components are co-located with their feature; shared components live in `src/components`.
- Server Components by default; opt into `'use client'` only when necessary.
- API calls go through the typed client in `src/lib/api-client.ts`.

---

## Technology Stack

| Layer | Technology |
|---|---|
| Runtime | .NET 9 |
| Architecture | Clean Architecture |
| API style | Minimal API |
| Database | MongoDB (via `MongoDB.Driver`) |
| Unit tests | xUnit + Shouldly + NSubstitute |
| Integration tests | xUnit + Testcontainers (MongoDB) |
| Frontend | Next.js 15 (App Router) + TypeScript |
| Styling | Tailwind CSS v4 |
| Local infra | Docker Compose |

---

## Repository Structure

```
home-maintenance/
├── ARCHITECTURE.md          ← you are here
├── docker-compose.yml       ← MongoDB + API for local dev
├── .gitignore
├── backend/
│   ├── HomeMaintenance.sln
│   ├── src/
│   │   ├── HomeMaintenance.Domain/         ← Entities, Value Objects, Domain Events
│   │   ├── HomeMaintenance.Application/    ← Use Cases, Interfaces, DTOs
│   │   ├── HomeMaintenance.Infrastructure/ ← MongoDB, external services
│   │   └── HomeMaintenance.API/            ← Minimal API endpoints, DI wiring
│   └── tests/
│       ├── HomeMaintenance.Unit.Tests/
│       └── HomeMaintenance.Integration.Tests/
└── frontend/
    ├── src/
    │   ├── app/             ← Next.js App Router pages
    │   ├── components/      ← Shared UI components
    │   ├── lib/             ← API client, utilities
    │   └── types/           ← Shared TypeScript types
    └── public/
```

---

## Adding a New Feature - Checklist

1. Add the domain entity / value object in `Domain` (if new concept).
2. Add the repository interface in `Application/Common/Interfaces`.
3. Add the use-case command/query + handler in `Application`.
4. Add the MongoDB repository implementation in `Infrastructure`.
5. Add the API endpoint in `API`.
6. Add the frontend page/component in `frontend/src/app`.
7. Write unit tests for the handler.
8. Write integration tests for the repository + endpoint.
9. Open a PR - no feature merges without green tests.

---

## Security and audit

This file captures the architectural shape. The durable security
baseline lives in [.polaris/memory/constitution.md](.polaris/memory/constitution.md):

- **Authentication**: Google OIDC. The OwnerId is the verified `sub`
  claim. A local OIDC stub (`Auth:UseStub=true`) is gated to the
  Development environment; production startup refuses to enable it.
- **Authorization**: ownership-based, default-deny. Cross-owner
  requests return 404 (not 403) to prevent enumeration leaks.
- **Audit log**: append-only JSONL at `audit-trail/property-job-step.jsonl`
  in local dev (gitignored). Captures auth outcomes, authorization
  denials, and every write against owned data. Production swaps in a
  managed sink behind the `IAuditLog` interface; the producer side
  does not change.

The full event-type catalogue, retention policy, and threat-model
boundaries are documented in the constitution.
