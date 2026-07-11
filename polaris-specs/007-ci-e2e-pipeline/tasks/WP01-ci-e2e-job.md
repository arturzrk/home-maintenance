---
work_package_id: WP01
title: "CI e2e job in ci.yml"
lane: "for_review"
dependencies: []
base_branch: main
base_commit: 39fe224b873ca62916d75904025cb8de1b013ead
created_at: '2026-07-11T09:22:37.312623+00:00'
subtasks: [T001, T002, T003, T004, T005, T006]
test_status: required
test_file: .github/workflows/ci.yml
domain: devops-infra
shell_pid: "78557"
---

# WP01 - CI e2e job in ci.yml

## Objective

Add an `e2e` job to `.github/workflows/ci.yml` that provisions MongoDB,
the .NET API (dev-stub auth), and a production Next.js build, then runs
the full 27-test Playwright suite. The PR introducing the job is the
validation (the check must pass on it).

## Context (verified facts)

- Existing jobs: `backend`, `frontend`, `validators` -- do not modify.
- API: `Auth:UseStub=true` is the appsettings.Development.json default;
  the auth guard requires `ASPNETCORE_ENVIRONMENT=Development`. Mongo
  connection defaults to `mongodb://localhost:27017`.
- Frontend env (all required): `NEXTAUTH_DEV_STUB=true`,
  `NEXTAUTH_SECRET` (any value in CI), `NEXTAUTH_URL=http://localhost:3000`,
  `API_BASE_URL=http://localhost:5000`,
  `NEXT_PUBLIC_API_URL=http://localhost:5000` (inlined at `next build`).
- e2e helpers call the API via `API_BASE_URL` (default already correct);
  Playwright baseURL is `http://localhost:3000` (config unchanged).
- Health endpoints: API `GET /health` (anonymous), frontend `GET /signin`.

## Subtasks

### T001 --- Job skeleton

`e2e` job, `runs-on: ubuntu-latest`, `timeout-minutes: 15`,
`services: mongodb: image: mongo:7.0, ports: ["27017:27017"]`.
Checkout, setup-dotnet 9.0.x with the same NuGet cache key scheme as the
backend job, setup-node 22 with npm cache on frontend/package-lock.json.

### T002 --- API build + start

```yaml
- name: Publish API
  run: dotnet publish backend/src/HomeMaintenance.API -c Release -o api-publish
- name: Start API
  run: |
    nohup dotnet api-publish/HomeMaintenance.API.dll > api.log 2>&1 &
  env:
    ASPNETCORE_ENVIRONMENT: Development
    ASPNETCORE_URLS: http://localhost:5000
```

Note: publish output must include appsettings.Development.json (default
publish behavior includes all appsettings*.json).

### T003 --- Frontend build + start

```yaml
- name: Install frontend deps
  working-directory: frontend
  run: npm ci
- name: Install Playwright browsers
  working-directory: frontend
  run: npx playwright install --with-deps chromium
- name: Build frontend
  working-directory: frontend
  run: npm run build
  env: { NEXTAUTH_DEV_STUB: "true", NEXTAUTH_SECRET: ci-e2e-secret, NEXTAUTH_URL: "http://localhost:3000", API_BASE_URL: "http://localhost:5000", NEXT_PUBLIC_API_URL: "http://localhost:5000" }
- name: Start frontend
  working-directory: frontend
  run: nohup npm start > ../web.log 2>&1 &
  env: (same as build)
```

### T004 --- Readiness waits (FR-05)

```yaml
- name: Wait for stack
  run: |
    for url in http://localhost:5000/health http://localhost:3000/signin; do
      for i in $(seq 1 60); do
        if curl -sf -o /dev/null "$url"; then echo "$url ready"; break; fi
        [ "$i" = "60" ] && { echo "$url never became ready"; tail -50 api.log web.log; exit 1; }
        sleep 1
      done
    done
```

### T005 --- Run tests + failure artifact

```yaml
- name: Playwright e2e
  working-directory: frontend
  run: npx playwright test
- name: Upload test results
  if: failure()
  uses: actions/upload-artifact@v4
  with:
    name: playwright-results
    path: |
      frontend/test-results/
      api.log
      web.log
    retention-days: 7
```

Also print api.log/web.log tails on failure for quick triage.

### T006 --- Self-verification

Open the PR; the `e2e` check must pass with 27 tests. Iterate on the
branch until green (each push re-runs the job).

## Definition of Done

- [ ] `e2e` check green on the introducing PR, 27/27 tests
- [ ] Failure path uploads test-results + service logs
- [ ] No changes outside `.github/workflows/ci.yml`
- [ ] PR reviewed and merged to main

## Risks

- **NEXT_PUBLIC_ inlining**: must be set on the build step, not only the
  start step.
- **Port collisions**: none expected on a fresh runner.
- **Chromium install time**: --with-deps ~1 min; acceptable within the
  15-minute budget.
- **Readiness flakes**: 60 s per service bound; logs tailed on failure.

## Run Command

```bash
polaris implement WP01
```

## Activity Log

- 2026-07-11T09:52:56Z – unknown – lane=doing – Implementing e2e CI job
- 2026-07-11T09:52:59Z – unknown – lane=testing – Self-verifying via PR checks
- 2026-07-11T09:53:00Z – unknown – lane=for_review – e2e check green on PR #75: 27/27 in 2m27s; 2 CI-env fixes (content root, AUTH_TRUST_HOST) + mongo health check
