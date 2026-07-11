# Tasks: 007-ci-e2e-pipeline

## Subtask List

| ID | WP | Description | Parallel |
|----|----|-------------|----------|
| T001 | WP01 | Add `e2e` job skeleton to ci.yml (mongo service, dotnet+node setup, caches) | |
| T002 | WP01 | Publish + background-start the API with dev-stub env | |
| T003 | WP01 | Build + background-start the frontend with stub env | |
| T004 | WP01 | Bounded readiness waits for :5000/health and :3000/signin | |
| T005 | WP01 | Run `npx playwright test`; upload test-results/ artifact on failure | |
| T006 | WP01 | Self-verify: e2e check green on the introducing PR (27/27) | |

## Work Packages

### WP01 --- `e2e` job in `.github/workflows/ci.yml`

**Goal**: Full Playwright suite runs on every PR/push to main against a
freshly provisioned stack. Single workflow-file change.

**Subtasks**: T001-T006 (sequential; one YAML block).

**Dependencies**: none.

**Implementation sketch**: see plan.md "Job sketch" (verified env vars,
ports, and auth gates). Validation is the Actions run on the PR itself.

**DoD**: e2e check green with 27/27 on the introducing PR; failure-path
artifact upload wired; no changes outside ci.yml.

## Parallelization

Single WP.

## MVP Scope

The one job. No phasing.

## Next Command

```bash
polaris implement WP01
```
