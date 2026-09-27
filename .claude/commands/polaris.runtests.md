---
description: Run E2E tests for work packages with automatic lane transitions (doing -> testing -> for_review/doing).
---


# /polaris.runtests - Run Tests with Auto Lane Transitions

## Model Guidance

Model: impl tier (see references/model-selection.md); routing is enforced by the launcher.

## Tool Preferences

Prefer local `Read`/`Grep`/`Glob`, then `WebFetch`, then a headless browser; use a screenshot browser only as a last resort, and extract PDFs as text.

## Output Style

One-line summary per test file run: `<file> -> PASS/FAIL`. After all tests: totals + lane outcome. No per-test narration. Do NOT use extended thinking.

---

## Location Pre-flight

The CLI enforces location: WP-specific runs require a feature worktree, `--all` regression runs accept the main repo.

**Telemetry**: Run: `polaris telemetry record runtests --feature <slug> --phase start --agent claude --wp <WP_ID>`

## Test Modes

Two layers, run in order: unit/integration (`run_tests.py` directly, after implementation) then E2E browser (`polaris runtests` on the `.e2e.js` files, after unit/integration pass). `polaris runtests` runs the E2E layer only.

## Lane Transition Flow

```
doing -> testing -> for_review   (tests pass)
doing -> testing -> doing        (tests fail)
```

## Usage

```bash
polaris runtests                     # eligible WPs (doing/testing)
polaris runtests --wp WP01           # one WP
polaris runtests --feature 001-slug  # one feature
polaris runtests --all               # regression, no lane changes
```

## Unit and Integration Tests

Run these directly before E2E. The runner detects the framework (pytest / npm / cargo / go) and emits JSON:

```bash
python .polaris/scripts/tasks/run_tests.py --project-root . --json
python .polaris/scripts/tasks/run_tests.py --project-root . --wp WP01 --json
```

A passing run writes the test-evidence gate under `.polaris/test-evidence/` that `move-task` consumes; agents never create or edit gate records by hand. On failure, follow the reported remediation and retry once fixed.

## E2E Browser Tests

`polaris runtests` runs Playwright-compatible `.e2e.js` files in `polaris-specs/{feature}/tests/e2e/` and transitions WP lanes automatically. The default `agent-browser` runner drives `npx agent-browser`; `--runner playwright` uses `npx playwright test`. It falls back to the other runner if the default is unavailable.

## Options

| Flag | Purpose |
|---|---|
| `--wp` | Single work package |
| `--feature` | One feature |
| `--all` | Regression, no lane changes |
| `--json` | Machine-readable (CI) |

More (`--runner`, `--headed/--headless`, `--base-url`, `--api-url`, `--all-repos`): `--help`.

## Failure Classification

Load `@references/runtests-failure-classification.md` for the full classification table, auto-retry workflow, and programmatic usage.

## Mutation Testing

Optional manual technique, not a gate. Load `@references/runtests-mutation-testing.md` to hand-check assertion strength.

## Troubleshooting

- **"No test framework detected"**: ensure a `pyproject.toml`/`package.json`/`Cargo.toml`/`go.mod` exists at the project root.
- **"No test files found" (E2E)**: run `/polaris.tasks` first to generate test skeletons.
- **"run_tests.py not found"**: run `polaris upgrade`, or check `.polaris/scripts/tasks/`.
- **Runner not available**: install it (`npm i -g @anthropic-ai/agent-browser`, or `npm init playwright@latest`) or switch with `--runner`.


**Telemetry**: Run: `polaris telemetry record runtests --feature <slug> --phase complete --agent claude --wp <WP_ID>`
