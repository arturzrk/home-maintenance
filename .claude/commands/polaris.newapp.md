---
description: Scaffold a new application from organization monorepo template with guided setup.
---


## User Input

**Telemetry**: Run: `polaris telemetry record newapp --feature <slug> --phase start --agent claude`


```text
$ARGUMENTS
```

You **MUST** consider the user input before proceeding (if not empty).

## Quick Mode

If user passes `--quick` or arguments contain "quick": Skip discovery questions EXCEPT the Security & Compliance block (Step 1B) - those answers are non-negotiable. Use Aptean defaults: fullstack app, Aptean branding yes, AKS deployment. Require only project name (from arguments or ask once) and the four security answers, then scaffold.

## Goal

Scaffold a new application following the organization monorepo template. ALL version floors are sourced from `src/specify_cli/scaffolds/version_pins.yaml` - read the manifest at scaffold time and use whatever it says. The Aptean standard tech stack today is **Django 5.1+ on Python 3.12+ backend, Vite / Next 16+ / React 19+ frontend on Node 22+ LTS, PostgreSQL 17+** (these values match the manifest as of 2026-05-07; do not hard-code them in the scaffold output, always read from the manifest). Supports frontend, backend, worker, MCP server, and full-stack application types. Every scaffolded app includes audit-log infrastructure, a security folder layout, and a seeded threat-model placeholder; no app ships without these.

## Execution Steps

### 1. Discovery

Ask the user about their project requirements:

- **Application type**: frontend, backend, worker, mcp, or fullstack
- **Project name**: kebab-case identifier (e.g., `my-new-service`)
- **Description**: Brief one-liner for README and package metadata
- **Aptean branding**: Should AppCentral design system be applied? (default: yes)
  - Yes: Aptean dark theme, AppCentral typography (Fira Sans / Inter / Fira Mono), teal accent, `--aptean-*` CSS variables
  - No: neutral default theme
- **Deployment target**:
  - Kubernetes (AKS) for AppCentral (default) - Helm charts, AKS manifests, ACR registry
  - Standalone / feature app - Docker Compose only, no K8s

The default stack reads from `src/specify_cli/scaffolds/version_pins.yaml` - **Django 5.1+ / 6+ on Python 3.12+ backend, Vite / Next 16+ / React 19+ frontend on Node 22 LTS, PostgreSQL 17+**. Do NOT ask the user to choose a language or framework - these are the Aptean standard. If the user explicitly requests a different stack in their arguments, honor it but note it deviates from the Aptean standard. Never recommend Node 18 or 20, Next 14 or 15, React 18, Python 3.10 or 3.11, Java 11 or 17, .NET 6 or 7, PostgreSQL below 17, or any framework version below the manifest floor.

If the user provided arguments, extract answers from there first.

### 1B. Security & Compliance Discovery (MANDATORY - never skipped)

Before any files are written, capture the four security baseline answers. These are non-negotiable; even in `--quick` mode they must be answered. If the user has already run `/polaris.constitution`, inherit the answers from `.polaris/memory/constitution.md` (Phase 2) and confirm; otherwise ask:

- **S1 Data tier**: What is the most sensitive data tier the application will store or process? (Public | Internal | Confidential | Restricted - PII/PHI/payment/secrets). The answer drives database encryption defaults, log redaction rules, and the threat-model template.
- **S2 Audit log policy**: Which event classes MUST be logged to an append-only audit trail, and for how long?
  - Default minimum (always enforced): authentication outcomes, authorization denials, all writes/deletes against Confidential or Restricted data, configuration changes, admin actions.
  - Default retention: 1 year. Confirm or override.
- **S3 AuthN / AuthZ model**: How do callers authenticate (none, API key, OIDC/OAuth2, SSO, mTLS), and how is authorization decided (RBAC, ABAC, ownership-based)? Default-deny is required - the scaffold seeds a deny-by-default policy point that the user wires up.
- **S4 Threat surface**: Where does untrusted input enter (public HTTP, webhook, file upload, queue, scheduled job consuming external data, third-party API)? Multi-tenant?

Persist these into `.polaris/memory/constitution.md` (Phase 2 - Security & Audit Baseline) at the end of scaffolding. They also seed the `security/threat-model.md` placeholder created in Step 4c.

### 2. Scaffold from Template

**Online mode** (preferred): Clone the organization template repository:

```bash
git clone --depth 1 https://github.com/Shared-Technology-Group/workspaces-sdd-repo-template.git <project-name>
cd <project-name>
python -c "import shutil; shutil.rmtree('.git')"
git init --initial-branch main
```

**Offline mode** (fallback): Generate the standard monorepo structure (every directory below is required - do not omit `audit-trail/`, `security/`, or `docs/security/` even for prototype apps):

```
<project-name>/
  src/
    <app-type>/
  tests/
  docs/
    security/
      threat-model.md            # seeded from Step 1B answers
      data-classification.md     # records the S1 answer + per-entity tier
  audit-trail/                   # append-only JSONL audit logs (logs themselves gitignored)
    .gitignore                   # ignores *.jsonl and *.gz; itself tracked, keeps the dir in git
    README.md                    # explains retention, sink, schema, and PII handling
  security/
    authz-policy.md              # default-deny rules (S3 answer)
    secrets.md                   # how secrets are sourced (env, vault, KMS)
  .github/
    workflows/
      ci.yml
  docker-compose.yml
  Dockerfile
  README.md
  VERSION
  CHANGELOG.md
  .gitignore                     # must include audit-trail/*.jsonl
```

### 3. Configure for Default Stack

Set up the Aptean standard stack. Read floors from `src/specify_cli/scaffolds/version_pins.yaml`:

- **Backend**: `pyproject.toml` (Django >= floor, Python >= floor), `manage.py`, Django settings module, virtual environment setup. `requires-python` matches the manifest's `runtimes.python.min`.
- **Frontend**: `package.json` with `"engines": {"node": ">=22"}`, `tsconfig.json`, Vite or Next.js 16+ config with React 19+. Pin Node base image tag from `container_base_images.node` (`node:22-alpine`).
- **Database**: PostgreSQL 17+ connection in Django settings, initial migration. Compose service uses the `container_base_images.postgres` tag.
- **Docker**: multi-stage `Dockerfile`, `docker-compose.yml` with PostgreSQL service. Base image tags MUST come from the manifest - never hard-code `node:20`, `python:3.10`, `postgres:14`, etc.

If the user explicitly requested a non-default stack, use the appropriate language patterns instead (Python: pyproject.toml; TypeScript/JS: package.json, tsconfig.json; Go: go.mod; Rust: Cargo.toml; C#: .csproj; Java: pom.xml or build.gradle). The version floor for any non-default stack still comes from `version_pins.yaml`.

### 4. Initialize Polaris

```bash
polaris init --here --merge
```

### 4a. Apply Aptean Branding (if selected)

If the user selected Aptean branding (default: yes):

- Typography is already handled for a TypeScript frontend: `polaris init` (step 4) auto-applies the `appcentral-shell` skill, which writes
  `public/fonts/*.woff2` and `styles/appcentral-typography.css`. Import that stylesheet at the root and the app is on the AppCentral stack.
- For a backend-rendered or non-TypeScript stack, copy the woff2 files from `.polaris/skills/aptean-brand/fonts/` into `static/fonts/` and declare the `@font-face` block from the brand guardrail
- Generate base CSS with `--aptean-*` design tokens (dark theme, teal accent `#54B3BE`) plus the `--font-heading` / `--font-body` / `--font-mono` type tokens
- Apply dark-theme-first color system
- Add Aptean logo SVG to static assets
- Reference `aptean-style/SKILL.md` for the full token set

### 4a1. Wire AppCentral Language & Theme (mandatory for any UI)

Every Aptean app embeds in the AppCentral shell, whose global header owns the language
dropdown (`EN - English`) and the theme selector. An app that ignores those signals looks
broken the moment a user switches language: they pick Dansk and nothing changes.

`polaris init` (step 4) auto-applies the `appcentral-shell` skill, so `lib/appcentral/`
already exists with `shell-context.tsx`, `i18n.ts`, `messages.ts`, and `i18n-context.tsx`.
Do NOT re-create those files. Your job is to wire them up and then USE them.

If the app has no UI at all (pure API, CLI, worker), skip this section.

**1. Nest the providers in the root layout.** `AppCentralI18nProvider` reads the locale from
`AppCentralShellProvider`, so the order is load-bearing:

```tsx
// app/layout.tsx
import { AppCentralShellProvider } from "@/lib/appcentral/shell-context";
import { AppCentralI18nProvider } from "@/lib/appcentral/i18n-context";
import { AppCentralDevShellBar } from "@/lib/appcentral/dev-shell-bar";

export default function RootLayout({ children }: { children: React.ReactNode }) {
  return (
    <html>
      <body>
        <AppCentralDevShellBar />
        <AppCentralShellProvider>
          <AppCentralI18nProvider>{children}</AppCentralI18nProvider>
        </AppCentralShellProvider>
      </body>
    </html>
  );
}
```

**2. Every user-visible string MUST go through `t()`.** This is the whole point - the
providers alone change nothing. A hardcoded literal is invisible to the language selector
forever, and converting literals later is far more expensive than writing them correctly
now. Generate this:

```tsx
const { t } = useTranslation();
return <button>{t("action.save")}</button>;   // CORRECT
```

Never this:

```tsx
return <button>Save</button>;                 // WRONG - never translates
```

That applies to buttons, labels, headings, placeholders, empty states, validation messages,
`aria-label`s, and `<title>`. Dates and numbers use `formatDate` / `formatNumber` from the
same hook, never `toLocaleString()` with a hardcoded locale.

**3. Add every new string to all nine locale catalogs.** `messages.ts` ships nine locales
matching the header dropdown - `en, de, fr, nl, es, pt, da, no, sv`. When you add a key,
add it to all nine. A key present only in `en` renders English for everyone; a locale
missing from `SUPPORTED_LOCALES` never resolves at all. Both fail silently.

Use dotted namespaced keys (`checkout.submit`, not `submitButton`) and `{placeholder}`
interpolation (`t("greeting.hello", { name })`) rather than string concatenation, which
breaks in languages with different word order.

**4. Never add a language or theme control to the app UI.** The AppCentral header owns both
exclusively. No language dropdowns, no dark-mode toggles, no `localStorage` locale keys.
See `guardrail.md` in the skill for the full rule set (14 rules).

**Validation before you report done**: switch the dev shell bar to `DA - Dansk` and confirm
visible text changes. If anything stays English, that string is still a hardcoded literal.

### 4b0. Seed Resource Lifecycle Scaffolding (mandatory)

Greenfield apps consistently leak DB connections, leaked threads, and unbounded queues because the lifecycle questions were never answered explicitly. Polaris now bakes the answers into every scaffold from day one.

Read `src/specify_cli/scaffolds/resource_lifecycle.md` (the canonical Polaris pattern library) and apply the matching stack section verbatim. Do NOT invent alternative patterns - the reference patterns are deliberately concrete because past leaks have come from "we'll do something like a pool" hand-waves.

For each scaffolded backend stack, the agent MUST produce:

1. **Connection pool singleton** at module/process scope:
   - FastAPI: `create_async_engine` in `app/db.py` with explicit `pool_size`, `max_overflow`, `pool_pre_ping`, `pool_recycle`. Session factory via `async_sessionmaker`. Per-request session via FastAPI dependency that closes in `finally`.
   - Express / Node: `pg.Pool` in `src/db.js` with explicit `max`, `idleTimeoutMillis`, `connectionTimeoutMillis`. Module-scope singleton.
   - Next.js: `globalThis._pgPool` singleton pattern in `lib/db.ts` for hot-reload safety on the Node runtime. Edge runtime requires HTTP-based DB clients.
   - Django: `CONN_MAX_AGE`, `CONN_HEALTH_CHECKS=True`, `DISABLE_SERVER_SIDE_CURSORS=True` if PgBouncer; explicit `OPTIONS` for keepalive.
   - Spring Boot: HikariCP with explicit `maximum-pool-size`, `minimum-idle`, `connection-timeout`, `idle-timeout`, `max-lifetime`, `leak-detection-threshold` in `application.yml`. Never accept Spring Boot's defaults.

2. **HTTP client singleton** at module/process scope:
   - Python: `httpx.AsyncClient` (or `httpx.Client` for sync) wired into FastAPI lifespan / Django app config `ready()`.
   - Node: `undici.Pool` or `axios.create()` with `keepAlive: true` at module scope.
   - Java: Singleton `RestClient` / `OkHttpClient` bean with `destroyMethod = "close"`.
   - .NET: `IHttpClientFactory` registered via DI.
   - NEVER instantiate HTTP clients per request.

3. **Graceful shutdown handler** with explicit drain timeout:
   - FastAPI: lifespan context manager that disposes engine and HTTP client in order.
   - Express: `process.on('SIGTERM', shutdown)` with `server.close()` then `pool.end()` then a force-exit safety net (`setTimeout(..., 30_000).unref()`).
   - Django: Gunicorn `--graceful-timeout 30` + `worker_exit` hook to close singleton clients.
   - Spring Boot: `server.shutdown=graceful` + `spring.lifecycle.timeout-per-shutdown-phase=30s` in `application.yml`.

4. **Bounded background-task pool** if the app spawns workers:
   - FastAPI: `BoundedWorker` pattern (asyncio.Semaphore + tracked task set + drain on shutdown).
   - Express: `BullMQ` / similar with bounded concurrency, never raw `setInterval` for production work.
   - Spring: `ThreadPoolTaskExecutor` bean with explicit `corePoolSize`, `maxPoolSize`, `queueCapacity`, `setWaitForTasksToCompleteOnShutdown(true)`.

5. **Liveness + readiness endpoints** at `/healthz/live` and `/healthz/ready`. Readiness flips to NotReady when shutdown begins so the load balancer drains the pod cleanly.

6. **Observability hooks**: pool metrics (in-use, waiters, wait time histogram) and background-task metrics (queue depth, in-flight, p99 task duration) exposed via Prometheus / OpenTelemetry per the constitution's Phase 2B Q14 answer.

After the code is written, run the generic checklist at the bottom of `resource_lifecycle.md` and add a `docs/architecture/resource-lifecycle.md` doc that records which patterns the app uses, which sizes were chosen, and the drain timeout.

### 4b. Seed Audit & Security Scaffolding (mandatory)

Write the following content to the directories created in Step 2 - never empty placeholders, the user should be able to read them and understand the intent without further guidance:

- `audit-trail/README.md`: documents retention (defaults to 1 year, override per S2), sink (filesystem / Splunk / Sentinel / S3+Object-Lock), event schema (actor, action, target, ts ISO-8601 UTC, outcome, correlation id), and the gitignore line that excludes the `.jsonl` files but tracks the README. Reference the constitution's Phase 2 baseline.
- `audit-trail/.gitignore`: contains `*.jsonl` and `*.gz` so logs never land in the repo while the directory itself stays tracked.
- `security/authz-policy.md`: states the default-deny rule, names the policy enforcement point, and shows a worked example matching the S3 answer (e.g., "Express middleware checking JWT role" or "Django `permission_classes` requiring an explicit allow rule").
- `security/secrets.md`: documents how secrets enter the app (env, mounted file, KMS, Key Vault) and explicitly forbids committing `.env*` to the repo.
- `docs/security/threat-model.md`: seeded with the four S1-S4 answers, an OWASP Top 10 checklist, and a "trust boundaries" diagram placeholder. Marked DRAFT so it gets updated as the app evolves.
- `docs/security/data-classification.md`: records the S1 answer and provides a small table where each persisted entity is classified.
- A baseline audit-log middleware/decorator stub appropriate to the stack (Django: a small `audit_log_middleware.py`; Express: a `requestAudit.ts` Pino transform; Next.js: a `lib/audit.ts` helper). The stub writes one event per state-changing request and points at `audit-trail/<feature>.jsonl` by default.

### 4c. Configure Deployment Target

**Kubernetes (AKS) for AppCentral** (default):

- Create `helm/` directory with:
  - `Chart.yaml`, `values.yaml`, `values-staging.yaml`, `values-production.yaml`
  - Templates: `deployment.yaml`, `service.yaml`, `ingress.yaml`, `hpa.yaml`
  - Health probes (liveness, readiness, startup)
- Create `.github/workflows/deploy.yml` for AKS deployment
- Default container registry: Azure Container Registry (ACR)

**Standalone / feature app**:

- Create `docker-compose.yml` only (no Helm or K8s manifests)
- Create `.github/workflows/ci.yml` for test + build
- No Kubernetes resources

### 5. Post-Scaffold Validation

1. Install dependencies using detected package manager
2. Run initial test suite to verify scaffold works
3. Run a structural check: every directory listed in Step 2 must exist; `audit-trail/.gitignore` must contain `*.jsonl`; `docs/security/threat-model.md` must reference the S1-S4 answers (not be a literal template).
4. Create initial git commit. Do NOT add `Co-Authored-By` trailers - the project's `commit-msg` hook appends `Co-Authored-By: Aptean Polaris <polaris@aptean.com>` automatically.

### 6. Summary

Display what was created and suggest next steps:

```
Application scaffolded!

  Type:       <app-type>
  Stack:      <Django/Vite/Next>+<React>+PostgreSQL (versions read from version_pins.yaml)
  Branding:   Aptean AppCentral (yes/no)
  Deployment: AKS for AppCentral / Standalone
  Security:   data tier=<S1>, audit retention=<S2>, authn=<S3>, surfaces=<S4>
  Location:   <project-name>/

Next steps:
  1. cd <project-name>
  2. Review generated structure (especially docs/security/threat-model.md)
  3. /polaris.specify to define your first feature - the spec template now
     includes a mandatory Audit & Security section that inherits these answers
```

## Operating Principles

- **Ask before acting**: Confirm choices before generating files
- **Detect connectivity**: Try online template first, fall back to offline patterns
- **CalVer versioning**: Initialize `VERSION` with the AppCentral CalVer standard `YY.MM.RR[.HH]` via `polaris calver check ""` (see ship.md Step 0b for why this must be a `polaris` subcommand, not a direct `specify_cli` import) and write its JSON `suggested` field to the file (release 1, no hotfix segment for a first release) - never a literal `YYYY.MM.PATCH` example.
- **Never overwrite**: If target directory has existing files, warn and confirm

## Context

$ARGUMENTS


**Telemetry**: Run: `polaris telemetry record newapp --feature <slug> --phase complete --agent claude`
