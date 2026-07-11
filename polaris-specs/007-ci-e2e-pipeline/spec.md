---
feature: 007-ci-e2e-pipeline
title: "CI: E2E pipeline"
status: draft
created_at: "2026-07-11"
---

# CI: E2E pipeline

## Objective

Run the full Playwright e2e suite (27 tests) automatically on every pull
request and push to main, so browser-level regressions block merges instead
of only being caught by a locally started stack. Backend unit/integration
tests and frontend lint/unit/build already run in CI (`ci.yml` jobs
`backend` and `frontend`); this feature adds the missing `e2e` job.

## Actors

- **Contributor** -- opens a PR; sees the e2e check pass or fail.
- **Maintainer** -- relies on the check as a merge gate.

## User Scenarios

### US1 -- PR runs the e2e suite
A contributor opens a PR. CI provisions MongoDB, starts the API with
dev-stub auth, builds and serves the frontend, and runs all Playwright
tests. The check appears alongside the existing backend/frontend checks.

### US2 -- Failing e2e test blocks the PR
If any Playwright test fails, the `e2e` check fails and the PR shows the
failure with the Playwright report available for download.

### US3 -- Green on main
Pushes to main run the same job, keeping the badge/history green.

## Functional Requirements

| ID | Requirement |
|----|-------------|
| FR-01 | An `e2e` job runs in `ci.yml` on `pull_request` and `push` to main. |
| FR-02 | The job provisions MongoDB, the API (dev-stub auth), and the frontend, then runs `npx playwright test`. |
| FR-03 | All 27 existing tests run unmodified (no test file changes). |
| FR-04 | On failure, the Playwright report/traces are uploaded as a build artifact. |
| FR-05 | The job fails if any service fails to become healthy within a bounded wait. |

## Success Criteria

| ID | Criterion |
|----|-----------|
| SC-01 | The `e2e` check passes on the PR that introduces it (self-verifying). |
| SC-02 | 27/27 tests run in CI; total job time under ~10 minutes. |
| SC-03 | No changes to test files or production code. |

## Assumptions

- GitHub-hosted `ubuntu-latest` runners (Docker available for the MongoDB
  service container).
- Dev-stub auth is CI-safe: the API requires `Auth:UseStub=true` +
  `ASPNETCORE_ENVIRONMENT=Development` (already the Development default),
  and the frontend stub is gated by `NEXTAUTH_DEV_STUB=true` only.
- The frontend runs as a production build (`next build` + `next start`)
  with stub env vars set at build time (NEXT_PUBLIC_* is inlined).
- Chromium only (matches local playwright.config.ts).
- Estimation skipped (consistent with features 003-006).

## Out of Scope

- Sharding/parallelizing the Playwright run.
- Running e2e against deployed environments (staging smoke tests).
- Modifying the existing backend/frontend/validators jobs.
