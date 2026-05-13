# Quickstart: 001-property-job-step

A developer reading this should be able to bring up the full local stack
and exercise Slice 1 end-to-end. Steps are ordered; each block is
self-contained.

## Prerequisites

- .NET 9 SDK (`dotnet --version` >= 9.0.x)
- Node.js 22+ (`node --version` >= 22.0.0)
- Docker Desktop (or any Docker engine; required for MongoDB and
  Testcontainers)
- A Google Cloud project with an OAuth 2.0 Client ID (Web Application).
  Authorized redirect URI: `http://localhost:3000/api/auth/callback/google`.
  Save the Client ID and Client Secret somewhere safe.

## 1. Clone and set up

```bash
git clone git@github.com:arturzrk/home-maintenance.git
cd home-maintenance
docker compose up mongodb -d
```

## 2. Backend configuration

Create `backend/src/HomeMaintenance.API/appsettings.Development.json`
overrides:

```json
{
  "Auth": {
    "UseStub": true,
    "Google": {
      "Authority": "https://accounts.google.com",
      "ClientId": "your-google-client-id.apps.googleusercontent.com"
    }
  },
  "MongoDB": {
    "ConnectionString": "mongodb://localhost:27017",
    "DatabaseName": "home-maintenance-dev"
  },
  "Cors": {
    "AllowedOrigins": "http://localhost:3000"
  }
}
```

`Auth:UseStub = true` activates the header-based stub (see
`research.md` R4); production builds refuse to start with this set.

Then start the API:

```bash
cd backend
dotnet run --project src/HomeMaintenance.API --launch-profile https
```

The API listens on `http://localhost:5000` (per `docker-compose.yml`) or
the dev URL printed by `dotnet run`. Confirm:

```bash
curl http://localhost:5000/health
# -> Healthy

curl -H "Authorization: Bearer dev-alice" http://localhost:5000/api/properties
# -> {"properties": []}
```

## 3. Frontend configuration

Create `frontend/.env.local`:

```bash
NEXTAUTH_SECRET=                       # generate with: openssl rand -base64 32
NEXTAUTH_URL=http://localhost:3000
GOOGLE_CLIENT_ID=your-google-client-id.apps.googleusercontent.com
GOOGLE_CLIENT_SECRET=your-google-client-secret
API_BASE_URL=http://localhost:5000
```

Install and start:

```bash
cd frontend
npm install
npm run dev
```

Open `http://localhost:3000/properties`. The middleware redirects to the
Google sign-in page; complete the consent flow. After redirect you land
on an empty Properties list with a "Create Property" button.

## 4. Happy-path walkthrough

1. **Create a Property**: click "Create Property", enter "Main House",
   submit. The new Property appears in the list.
2. **Open the Property**: click the card to navigate to
   `/properties/{id}`. Empty Jobs list.
3. **Create a Job**: click "Create Job". Enter:
   - Name: "Service boiler"
   - Due date: pick a date 2 weeks out
   - Steps: add 3 ("Shut off gas", "Drain system", "Replace filter")
4. **Open the Job**: click the card to navigate to `/jobs/{id}`. See the
   checklist with all three steps unchecked.
5. **Tick steps**: tick the steps in any order. Each tick fires a POST
   to `/api/jobs/{id}/steps/{stepId}/tick`. The UI updates without a
   full reload.
6. **Complete the Job**: with all three steps ticked, the "Complete Job"
   button enables. Click it. The Job transitions to read-only and shows
   the completion timestamp. Try ticking a step now: the checkbox is
   disabled.

## 5. Cross-owner sanity check

Open an incognito window, sign in as a different Google account, and
visit `/jobs/{id}` for the Job you created above. You should get a
"Not found" page (the API returned 404). Switch back to the first
account; the Job is still there.

## 6. Running tests

```bash
# Backend unit tests
cd backend
dotnet test tests/HomeMaintenance.Unit.Tests

# Backend integration tests (requires Docker)
dotnet test tests/HomeMaintenance.Integration.Tests

# Frontend tests
cd frontend
npm test
```

CI runs all three on every push. The CI definition is at
`.github/workflows/ci.yml`.

## 7. Audit log verification

While Slice 1 is local-only, the audit log lands at
`audit-trail/property-job-step.jsonl` (gitignored). Tail it during the
happy-path walkthrough:

```bash
tail -f audit-trail/property-job-step.jsonl | jq .
```

You should see records like:

```json
{"eventType":"property.created","actor":"alice@google","target":"property:01J...","timestamp":"2026-05-12T14:21:09.123Z","correlationId":"..."}
{"eventType":"job.created","actor":"alice@google","target":"job:01J...","timestamp":"2026-05-12T14:22:01.005Z","correlationId":"...","payload":{"propertyId":"01J...","name":"Service boiler","stepCount":3}}
{"eventType":"step.ticked","actor":"alice@google","target":"job:01J.../step:01J...","timestamp":"...","correlationId":"..."}
{"eventType":"job.completed","actor":"alice@google","target":"job:01J...","timestamp":"...","correlationId":"..."}
```

## 8. Common pitfalls

- **Mongo not reachable**: ensure `docker compose up mongodb -d` is
  running (`docker ps | grep mongo`). The API health check returns
  Unhealthy if the connection fails.
- **`Auth:UseStub` left on in non-Dev**: startup throws explicitly.
- **NextAuth session has no `idToken`**: confirm `frontend/src/lib/auth.ts`
  overrides the `jwt` and `session` callbacks; the API gets the token
  via `session.idToken`.
- **Stale Google JWKS during dev**: restart the API; the cache refresh
  is on first 401 due to unknown `kid`.
- **Step reorder rejected**: the request must list every existing step
  id exactly once. A partial list is treated as a validation error.

## 9. Where to next

- Read `spec.md` if you need the requirement detail behind a behaviour.
- Read `plan.md` for the implementation plan and constitution check.
- Read `data-model.md` for the canonical entity shapes.
- Look in `polaris-specs/001-property-job-step/tasks/` for the work
  packages once `/polaris.tasks` has been run.
