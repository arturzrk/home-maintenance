# Home Maintenance Software

A clean-architecture home maintenance tracking application.
See [ARCHITECTURE.md](./ARCHITECTURE.md) for all project rules and decisions.

**Working title**: `home-maintenance` (codebase, repo).
**Intended production domain**: [`maintained.house`](https://maintained.house) (reserved as the public branding target; not yet provisioned).

## Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9)
- [Node.js 22+](https://nodejs.org/)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/)

## Quick Start (local dev)

### 1. Start MongoDB

```bash
docker compose up mongodb -d
```

### 2. Run the backend API

```bash
cd backend
ASPNETCORE_ENVIRONMENT=Development dotnet run --project src/HomeMaintenance.API
# API available at http://localhost:5000
# Health check: http://localhost:5000/health
```

The Development environment enables the local OIDC stub: any
`Authorization: Bearer dev-<sub>` header authenticates as the OwnerId
`<sub>` without going through Google. Production refuses to start with
`Auth:UseStub=true`. Manual API smoke tests live in
`backend/src/HomeMaintenance.API/api-calls.rest` (open with the VS Code
REST Client extension).

### 3. Run the frontend

```bash
cd frontend
npm install
cp ../.env.example .env.local        # then fill in NEXTAUTH_SECRET
npm run dev
# App available at http://localhost:3000
```

`frontend/.env.example` documents every variable. For local-only
testing set `NEXTAUTH_DEV_STUB=true` and you can sign in via the
amber dev-stub form at `/signin` without any Google OAuth setup.

### Run everything via Docker Compose

```bash
docker compose up --build
```

## Running Tests

### Backend unit tests

```bash
cd backend
dotnet test tests/HomeMaintenance.Unit.Tests
```

### Backend integration tests (requires Docker for Testcontainers)

```bash
cd backend
dotnet test tests/HomeMaintenance.Integration.Tests --filter "Category!=perf"
```

CI excludes performance tests by default. To run the step-tick p95
benchmark locally:

```bash
dotnet test tests/HomeMaintenance.Integration.Tests --filter "Category=perf"
```

### Frontend tests

```bash
cd frontend
npm test
```

## Setting up Google sign-in (staging / production)

See [`docs/oidc-setup.md`](./docs/oidc-setup.md) for the full
operational runbook: per-environment Google Cloud Console setup,
backend + frontend env-var matrix, verification checklist, and the
common error catalogue.

Short version: each non-dev environment gets its own Google OAuth
Client (separate redirect URI + audience). The backend reads
`Auth:Google:ClientId` and treats it as both issuer + audience for ID
token validation. The frontend uses NextAuth + the Google provider
with the same Client ID and a per-environment Client Secret.
Production startup refuses to enable the dev stub.

Brief sketch (full detail in the runbook):

1. Create an OAuth 2.0 Client ID in the
   [Google Cloud Console](https://console.cloud.google.com/apis/credentials)
   for a Web Application.
2. Set the authorized redirect URI to:
   `{NEXTAUTH_URL}/api/auth/callback/google` (e.g.
   `https://maintained.house/api/auth/callback/google`).
3. Wire the Client ID + Secret into the frontend (`GOOGLE_CLIENT_ID`,
   `GOOGLE_CLIENT_SECRET`) and the backend (`Auth:Google:ClientId`).
   Set `Auth:UseStub=false` and `NEXTAUTH_DEV_STUB` unset.
4. The backend validates the Google ID token (signature, issuer,
   audience, expiry, 60s clock skew, 24h JWKS cache) and resolves
   the `sub` claim to the `OwnerId`. See
   `.polaris/memory/constitution.md` for the audit-log baseline that
   then applies.

## Deployment

### Backend - Azure App Service

Auto-deploys on every push to `main` that touches `backend/**`. The
workflow is at `.github/workflows/deploy-backend.yml`; setup steps
documented in the file header.

One-time setup:

1. **Get the publish profile**. Azure portal -> your App Service ->
   Overview -> "Get publish profile" (downloads a `.PublishSettings`
   XML file).
2. **Add a GitHub repository secret** named
   `AZURE_BACKEND_PUBLISH_PROFILE` with the full XML content.
3. **Add a GitHub repository variable** named
   `AZURE_BACKEND_APP_NAME` with the App Service name (e.g.
   `home-maintenance-staging`).
4. **App Service runtime config** is set in the Azure portal under
   Configuration -> Application settings (see `docs/oidc-setup.md`
   for the staging env-var matrix).
5. Disable any other CD configured under Deployment Center to avoid
   double-deploys.

Manual deploy: Actions tab -> "Deploy backend to Azure App Service"
-> "Run workflow".

### Frontend - Vercel

Auto-deploys on every push to `main` via the Vercel-GitHub integration.
Configuration lives in the Vercel project (Settings -> Git, Settings
-> Environment Variables). No workflow file required.

## Audit log

Every authenticated write (Property create/rename, Job create/update/
complete, Step add/remove/edit/reorder/tick/untick) emits an
append-only JSON record. By default the local sink is
`audit-trail/property-job-step.jsonl` (gitignored). The schema and
event-type catalogue live in `.polaris/memory/constitution.md` and the
spec; the producer interface is `IAuditLog` in
`HomeMaintenance.Application.Common.Interfaces`. Production swaps in a
managed sink behind the same interface.

## Project Structure

```
home-maintenance/
├── ARCHITECTURE.md             - project rules & decisions
├── .polaris/memory/constitution.md - durable governance (security baseline, model selection, license compliance)
├── docker-compose.yml
├── audit-trail/                - local audit-log sink (gitignored)
├── backend/
│   ├── HomeMaintenance.sln
│   ├── src/
│   │   ├── HomeMaintenance.Domain/
│   │   ├── HomeMaintenance.Application/
│   │   ├── HomeMaintenance.Infrastructure/
│   │   └── HomeMaintenance.API/
│   │       └── api-calls.rest  - manual smoke tests for the REST Client extension
│   └── tests/
│       ├── HomeMaintenance.Unit.Tests/
│       └── HomeMaintenance.Integration.Tests/
└── frontend/                   - Next.js 15 + TypeScript + Tailwind CSS
```
