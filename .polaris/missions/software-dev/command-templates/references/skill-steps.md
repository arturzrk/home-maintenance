## Step 2. Analyze Project

Scan with Glob/Grep (parallel where possible, NO bash):

- **Entry points**: `**/main.py`, `**/app.py`, `**/index.ts`, `**/Program.cs`
- **Package manager**: `poetry.lock`, `uv.lock`, `yarn.lock`, `pnpm-lock.yaml`, `pyproject.toml`, `package.json`
- **Env files**: `.env`, `.env.example` -- Read any found
- **Structure**: Check `frontend/`, `client/`, `web/` for `package.json` and `.git` (separate repos?)
- **Active skills**: `.polaris/skills/*.md`
- **Existing skill code** (Grep for skill-specific patterns):
  - `iam`: `Depends\(get_current_user\)|require_auth|JWTBearer|passport\.authenticate`
  - `studio`: `APTEAN_STUDIO|AzureOpenAI|studio_client`
  - `devops`: Glob `.github/workflows/*.yml`, `Dockerfile`
  - `workflow`: `APTEAN_WORKFLOW|workflow_client`
  - `mcp-server`: `FastMCP|McpServer|@mcp\.tool`
  - `appcentral-shell`: `useAppCentralLocale|useAppCentralTheme|APPCENTRAL_LOCALE_CHANGE|APPCENTRAL_THEME_CHANGE|AppCentralShellProvider`
- **Routes**: `@app\.(get|post|put|delete)|@router\.|app\.use\(`
- **Frontend code**: Glob `frontend/src/**/*.{ts,tsx,js}`, `src/**/*.{ts,tsx,jsx}`, `static/**/*.{html,js}`
- **Tests**: Glob `tests/**/*.py` or `**/*.test.ts`, Read 1-2

Determine: framework, package manager, project structure, frontend root, existing skill code, consumer field expectations, frontend auth gaps.

## Step 3. Read SKILL.md, Config Questions, Deploy Guardrail

**3a.** Read `<BASE_PATH>/<skill-name>/SKILL.md`. Parse `config:` frontmatter entries (key, type, prompt, default, options).

**3b.** Pre-fill answers from Step 2 analysis. Tell user what was detected.

**3c.** Ask remaining config questions interactively. **For `iam`: ALWAYS ask identity provider -- never silently default to Entra. Company default is Aptean IAM (Keycloak).**

**3d.** Read `<BASE_PATH>/<skill-name>/guardrail.md`, replace `{{PROJECT_NAME}}` with project dir name, write to `.polaris/skills/<skill-name>.md`.

## Step 4. Read Guardrail

Read the deployed guardrail. Its Patterns, Anti-Patterns, Testing Rules, and File Conventions are non-negotiable constraints.

## Step 5. Cross-Skill Awareness

Read `.polaris/skills/` for active skills. Note interactions (e.g., IAM+Studio share tokens, DevOps CI should test all skills).

## Step 6. Present Integration Plan -- WAIT FOR APPROVAL

Do NOT modify files until approved. Present: framework, package manager, config, project structure, frontend root, consumer field expectations, existing code found, active skill interactions, files to create/modify, frontend changes, auto-actions.

Wait for: approve all / skip specific files / cancel / questions.

## Step 7. Execute Integration

Apply config-driven implementation following guardrail patterns:
- Wire imports (match project style, no duplicates)
- Register middleware (correct order, integrate with existing)
- Protect routes per guardrail spec (skip already-protected)
- **Auto-wire consumer fields**: match existing frontend field names
- Environment variables: append to `.env.example`, copy to `.env` if missing
- Tests: follow project patterns, include positive + negative cases
- Frontend: follow guardrail's Frontend section; inject auth headers, wire UI components

## Step 8. Install Dependencies

Use detected package manager (`pip`/`poetry add`/`uv add`/`npm`/`yarn`/`pnpm`/`dotnet restore`). Stop on failure.

## Step 9. Validate and Test

Syntax check modified files, run skill tests, fix failures, verify guardrail compliance (no anti-patterns).

## Step 10. Summary

Past-tense summary: config used, guardrail deployed, files created/modified, deps installed, test results, brownfield/frontend notes. Only manual step: fill `.env` credentials.
