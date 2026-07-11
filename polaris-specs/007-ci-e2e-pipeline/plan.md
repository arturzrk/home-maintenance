---
feature: 007-ci-e2e-pipeline
title: "CI: E2E pipeline -- Implementation Plan"
created_at: "2026-07-11"
---

# Implementation Plan: CI -- E2E pipeline

**Branch**: `007-ci-e2e-pipeline-WP01` | **Spec**: [spec.md](spec.md)

## Summary

Add one `e2e` job to `.github/workflows/ci.yml`: MongoDB service
container, published .NET API with dev-stub auth, production Next.js
build, `npx playwright test`, artifact upload on failure.

## Technical Context

**CI**: GitHub Actions, ubuntu-latest
**Stack**: mongo:7.0 service, .NET 9 API, Node 22 / Next.js 15, Playwright Chromium
**Scale/Scope**: 1 workflow file change; no test or production code changes

## Constitution Check

No violations.

## Verified Facts

- `ci.yml` already has `backend` (unit + Testcontainers integration),
  `frontend` (lint/unit/build), `validators` jobs -- e2e is the only gap.
- API dev-stub: `Auth:UseStub=true` is the `appsettings.Development.json`
  default; the guard in `AuthenticationExtensions` requires
  `ASPNETCORE_ENVIRONMENT=Development`. Mongo connection overridable via
  `MongoDB__ConnectionString` (defaults to `mongodb://localhost:27017`,
  which matches a service container mapped to 27017).
- Frontend dev-stub: `process.env.NEXTAUTH_DEV_STUB === "true"` -- works in
  a production build. Needs `NEXTAUTH_SECRET`, `NEXTAUTH_URL`,
  `API_BASE_URL`, `NEXT_PUBLIC_API_URL` (inlined at build time).
- e2e helpers hit the API directly via `API_BASE_URL` (default
  `http://localhost:5000`); Playwright baseURL is `http://localhost:3000`.

## Work Packages

### WP01 -- `e2e` job in ci.yml

Job sketch:

```yaml
e2e:
  name: e2e
  runs-on: ubuntu-latest
  timeout-minutes: 15
  services:
    mongodb:
      image: mongo:7.0
      ports: ["27017:27017"]
  steps:
    - checkout
    - setup-dotnet 9.0.x (+ NuGet cache, same key scheme as backend job)
    - setup-node 22 (+ npm cache on frontend/package-lock.json)
    - dotnet publish backend API (Release) -> ./publish
    - start API in background:
        ASPNETCORE_ENVIRONMENT=Development
        ASPNETCORE_URLS=http://localhost:5000
        (Auth:UseStub + Mongo conn come from Development defaults)
    - npm ci (frontend)
    - npx playwright install --with-deps chromium
    - next build with stub env (NEXTAUTH_DEV_STUB=true, NEXTAUTH_SECRET=ci-secret,
      NEXTAUTH_URL=http://localhost:3000, API_BASE_URL/NEXT_PUBLIC_API_URL=http://localhost:5000)
    - start `next start` in background (same env)
    - wait for http://localhost:5000/health and http://localhost:3000/signin
      (bounded curl retry loop, fail after ~60s each)
    - npx playwright test
    - on failure: upload playwright-report/ and test-results/ as artifact
```

Notes:

- Use `nohup ... &` + `curl --retry` or an explicit `for` loop for
  readiness; fail the job on timeout (FR-05).
- The API publish step reuses the NuGet cache; total job budget ~10 min
  (suite itself runs in ~10-15 s locally once the stack is warm).
- Playwright HTML report: set `reporter: [["list"], ["html", { open: "never" }]]`?
  NOT needed -- keep config unchanged (SC-03); `test-results/` (error
  context) still gets uploaded, which is enough to debug. If an HTML
  report is wanted later, pass `--reporter=html` via CLI flag instead of
  editing the config.
- Chromium install is the slowest step (~1 min with --with-deps).

## Definition of Done

- [ ] `e2e` job green on the PR introducing it, 27/27 tests
- [ ] Artifact upload wired for failures (verify wiring by inspection)
- [ ] No changes outside `.github/workflows/ci.yml`
- [ ] PR merged to main
