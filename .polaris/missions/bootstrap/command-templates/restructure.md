---
description: Restructure a codebase to follow organization monorepo standards and best practices.
---

## User Input

**Telemetry**: Run: `polaris telemetry record restructure --feature <slug> --phase start --agent {{AGENT_NAME}}`


```text
$ARGUMENTS
```

You **MUST** consider the user input before proceeding (if not empty).

## Goal

Analyze the current project structure against organization best practices (monorepo layout, CalVer versioning, health endpoints, documentation) and guide the user through restructuring with a non-destructive migration plan.

## Execution Steps

### 1. Analyze Current Structure

Examine the project layout and identify:

- **Source code location**: Where does code live? (src/, lib/, app/, root-level?)
- **Test location**: Where are tests? (tests/, __tests__/, spec/, alongside source?)
- **Documentation**: Is there docs/? README quality?
- **CI/CD**: Any workflows? (.github/workflows/, .gitlab-ci.yml, azure-pipelines.yml)
- **Containerization**: Dockerfile? docker-compose.yml?
- **Versioning**: How is the version tracked? (package.json, pyproject.toml, VERSION file?) Read the `VERSION` file's stripped contents (or `""` if missing) and run `polaris calver check "<contents>"` (see ship.md Step 0b for why this must be a `polaris` subcommand, not a direct `specify_cli` import) to validate against the AppCentral CalVer standard `YY.MM.RR[.HH]` - flag both "missing" and "legacy `YYYY.MM.PATCH` format" as distinct gaps.
- **Configuration**: Environment management? (.env, config files)

### 2. Gap Analysis Report

Compare against the organization standard monorepo layout. The security/audit directories are mandatory for every project; flag missing ones as FAIL, not WARN:

```
Expected structure:
  src/                          - Application source code
  tests/                        - Test suite
  docs/                         - Documentation
    security/                   - REQUIRED
      threat-model.md           - REQUIRED
      data-classification.md    - REQUIRED
  audit-trail/                  - REQUIRED (append-only audit logs sink)
    .gitignore                  - REQUIRED (ignores *.jsonl / *.gz)
    README.md                   - REQUIRED (retention, sink, schema)
  security/                     - REQUIRED
    authz-policy.md             - REQUIRED (default-deny rule)
    secrets.md                  - REQUIRED (no .env* committed)
  .github/                      - CI/CD workflows
  docker/                       - Docker configurations (optional)
  VERSION                       - CalVer version file
  CHANGELOG.md                  - Change log
  README.md                     - Project documentation
  Dockerfile                    - Container definition
```

Generate a gap report:

```
Structure Gap Analysis
======================

Current vs Standard Layout:

  [PASS] src/ directory exists
  [FAIL] No VERSION file (AppCentral CalVer YY.MM.RR[.HH] not configured)
  [FAIL] No CHANGELOG.md
  [FAIL] No audit-trail/ directory (security baseline)
  [FAIL] No docs/security/threat-model.md (security baseline)
  [FAIL] No security/authz-policy.md (security baseline)
  [WARN] Tests in src/__tests__/ instead of tests/
  [PASS] Dockerfile exists
  [FAIL] No health endpoints detected
  [WARN] No .editorconfig

Total: 2 passed, 6 failed, 2 warnings
```

The three security-baseline FAILs are blockers: a project that ships without `audit-trail/`, `security/`, and `docs/security/` is not compliant with the Aptean baseline regardless of other quality signals.

If a `VERSION` file exists but is in the legacy `YYYY.MM.PATCH` format, report `[FAIL] VERSION file uses legacy format YYYY.MM.PATCH, expected YY.MM.RR[.HH]` in its place instead - never both lines together, since the file is either missing or legacy-format, never both.

### 3. Migration Plan

Propose specific changes, grouped by priority:

**Blocker priority** (security baseline - never skipped):
- Create `audit-trail/` with `.gitignore` (`*.jsonl`, `*.gz`) and a `README.md` documenting retention, sink, and event schema (actor, action, target, timestamp ISO-8601 UTC, outcome, correlation id)
- Create `security/authz-policy.md` declaring the default-deny rule and policy enforcement point
- Create `security/secrets.md` documenting how secrets enter the app and the rule that `.env*` files are never committed
- Create `docs/security/threat-model.md` (DRAFT) and `docs/security/data-classification.md` seeded from the project constitution's Phase 2 answers, or from a fresh interview if no constitution exists
- Add an `audit-trail/*.jsonl` line to the project's root `.gitignore`

**High priority** (structural):
- Move test files to standard location
- Create or migrate `VERSION` to the AppCentral CalVer standard `YY.MM.RR[.HH]`: reuse the `polaris calver check` result from the versioning-analysis step above - its `suggested` field is already the migrated value (legacy) or a freshly seeded one (missing/invalid); same shared command `standards.md`'s auto-fix uses, so both stay consistent
- Add CHANGELOG.md

**Medium priority** (operational):
- Add health endpoints
- Add or update CI/CD workflows
- Add .editorconfig

**Low priority** (nice-to-have):
- Reorganize documentation
- Add missing Docker configurations

### 4. Interactive Confirmation

For each proposed change, ask:

> Apply this change?
> - Move tests/__tests__/ to tests/ (y/n)
> - Create/migrate VERSION file to AppCentral CalVer YY.MM.RR[.HH] (y/n)
> - Add CHANGELOG.md template (y/n)

**Never delete files without explicit confirmation.** Offer to move rather than delete.

### 5. Apply Approved Changes

Execute only the changes the user approved. For file moves:

```bash
# Always use git mv for tracked files
git mv old/path new/path

# Update imports/references after moves
# Show the user what imports changed
```

### 6. Post-Restructure Validation

1. Run existing tests to verify nothing broke
2. Verify import paths are updated
3. Show a before/after structure comparison

### 7. Summary

```
Restructure Complete!

Changes applied:
  - Moved tests to tests/
  - Created VERSION (26.07.01)
  - Added CHANGELOG.md
  - Added .editorconfig

Skipped (user declined):
  - Health endpoints
  - CI/CD workflows

Run tests to verify: <detected test command>
```

## Operating Principles

- **Non-destructive**: Never delete files without explicit confirmation
- **Git-aware**: Use `git mv` for tracked files to preserve history
- **Incremental**: Apply changes one at a time, verify after each
- **Preserve behavior**: Restructuring should not change how the code runs
- **Show diffs**: Display what will change before applying

## Context

{ARGS}


**Telemetry**: Run: `polaris telemetry record restructure --feature <slug> --phase complete --agent {{AGENT_NAME}}`
