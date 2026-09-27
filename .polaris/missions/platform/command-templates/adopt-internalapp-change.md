---
description: Adopt pending AppCentral platform changes - the IAM Internal App token change, and the move of deployed app URLs under /app/.
---

## User Input

**Telemetry**: Run: `polaris telemetry record adopt-internalapp-change --feature <slug> --phase start --agent {{AGENT_NAME}}`


```text
$ARGUMENTS
```

You **MUST** consider the user input before proceeding (if not empty). Arguments may include a service path (for monorepos) or notes about the app's identity (poid, internal/workspace status).

## Completion Contract (read first)

This command has **12 steps** and produces **two verdicts and two summaries** -
one for Part A (steps 1-6) and one for Part B (steps 7-12). A response
containing only one verdict is an INCOMPLETE run, not a finished one.

Before you reply, check: have you printed the Part B verdict? If not, you have
stopped halfway. Part A concluding `not-applicable` or `already-adopted` is
extremely common - most projects have no IAM token logic - and says nothing
about whether Part B applies. They are unrelated changes that happen to ship
together.

## Goal

This command adopts the pending AppCentral platform changes. They are **independent** - a project may need one, both, or neither. Detect each separately and report each separately; never skip Part B because Part A was not applicable, or vice versa.

- **Part A - IAM Internal App token change** (below): token permission inclusion ends 2026-09-04.
- **Part B - AppCentral `/app/` URL move** (see "Part B" further down): deployed apps move from `/<app_code>/` to `/app/<app_code>/`.

### Part A - IAM Internal App token change

Aptean IAM is changing what access tokens contain (token permission inclusion ends **September 4, 2026**). Part A applies EXACTLY three adoptions to a project and nothing more: (1) **API as source of truth** - stop deriving "which apps a user has" from token roles; such logic migrates to the IAM User Available Apps API (`GET /iam/api/v1/clients/{coid}/users/{uoid}/apps`), whose response is the authoritative apps list; (2) **Workspace apps** - remove every dependency on a Workspace app's token role for access or visibility (those roles are permanently removed from tokens) and switch that logic to the API; (3) **Status-driven availability** - drive app visibility/availability from `appcentral_status` (treat `Activated` as ready/usable), never from token role presence. Token roles are kept ONLY where they remain valid: fine-grained checks inside role-bearing (non-workspace) apps that are Activated. Everything else relies on the API.

## Critical Semantics (apply throughout)

- **Access = presence.** An app PRESENT in the API response means the user has access, regardless of its status. Only ABSENCE from the response is a denial.
- **Activated = ready/usable.** `appcentral_status == "Activated"` drives visibility and readiness, never access denial. Present-but-not-Activated is "not ready", NEVER "no access".
- Internal Apps and Workspace apps NEVER have roles in tokens; their access is determined solely via the API. Absence of a role claim in a token MUST NOT be interpreted as access denial.

## Run Order (mandatory)

1. Run **Part A** (steps 1-6 below).
2. Run **Part B** (steps 7-12, further down) - **always**, whatever Part A concluded.
3. Print both summaries.

Part A's verdicts end Part A only. `not-applicable`, `already-adopted`, and a
declined Part A gate all mean "no Part A changes" - none of them end the
command. A project with no IAM token handling can still be a deployed
AppCentral app whose URL must move, and that is the common case. If you find
yourself about to finish after Part A, you are not done: go to Part B.

## Execution Steps 1-6 (Part A)

### 1. Detect Applicability (read-only)

Check, in order:

1. `.polaris/metadata.yaml` and `.polaris/config.yaml` - app identity, IAM integration markers.
2. Auth configuration - `appsettings*.json`, `.env*` files, helm values, docker-compose: IAM authority/base URL, client IDs.
3. Code signals - token validation middleware, JWT libraries, IAM/AppCentral SDK references. Use agent search tools (Read/Grep/Glob) or `python -c` one-liners only; never `find`/`grep`/`sed` shell commands.

Produce exactly one verdict:

| Verdict | Meaning | Action |
|---------|---------|--------|
| `applicable` | IAM integration present, token-claim access logic may exist | Continue to Step 2 |
| `not-applicable` | No IAM integration or token handling at all | Report the evidence checked, make zero Part A changes, then **go to Part B** |
| `already-adopted` | Project already uses the User Available Apps API with no remaining token-claim access checks | Report the evidence, make zero Part A changes, then **go to Part B** |
| `unknown-app-type` | IAM present but internal/workspace status cannot be determined from code or config | Ask the user for the app's `poid` and whether it is an Internal/Workspace app, then re-evaluate |

### 2. Scan and Classify (read-only)

Scan the codebase for token-claim-based access logic. Minimum detection patterns per stack:

| Stack | Patterns |
|-------|----------|
| C#/.NET | `ClaimsPrincipal`, `User.Claims`, `User.IsInRole`, `[Authorize(Roles=...)]`, `[Authorize(Policy=...)]` where the policy reads permission/role claims |
| Python | `jwt.decode`, claims dict access for `permissions`/`roles`/`scope`, FastAPI/Django/Flask auth dependencies reading those claims |
| Node/TS | `express-jwt`, `passport`, `jsonwebtoken` verify + claim reads, NestJS guards reading roles |
| Frontend | JWT parsing in the browser; conditional rendering or route guards keyed on token roles for app links or app entry |

Classify every hit as exactly ONE of:

- **(a) app-access-gating**: decides whether the user may use this app at all. Will move to the API presence helper.
- **(b) app-visibility**: decides which apps/links a user sees. Will move to `appcentral_status`-based data.
- **(c) fine-grained-role-check**: in-app authorization inside an Activated role-bearing (non-workspace) app - the ONLY context where token roles remain valid. REPORT-ONLY, never rewritten.

**Classification rule**: any token-role read for a Workspace or Internal app classifies as (a) or (b) - the role can never appear in the token, so it is by definition an access or visibility dependency. Classification (c) is valid ONLY inside Activated role-bearing non-workspace apps.

Monorepos: scan each service separately and group findings per service; the user chooses which services to migrate.

### 3. Findings Report Gate (no Part A writes before confirmation)

Emit the Findings Report:

1. **Verdict** and the evidence that produced it.
2. **App identity**: poid, internal/workspace vs customer-facing, source (config or user-provided).
3. **Findings table**: `file:line | stack | classification | proposed change` - grouped by service in monorepos. Category (c) rows are marked "still valid - no change".
4. **Migration plan**: the ordered steps the apply phase will execute.

Then WAIT for explicit user confirmation. Do not write, edit, or create any file before the user confirms.

- `not-applicable` / `already-adopted`: Part A already concluded in Step 1 with zero changes - go straight to Part B.
- User declines: record the decision, make zero Part A changes, and go to Part B. Declining Part A is not declining Part B.

### 4. Apply the Migration (only after confirmation)

#### 4a. Generate the IAM apps client and two helpers

In each affected stack's language, generate a client for `GET /iam/api/v1/clients/{coid}/users/{uoid}/apps` exposing TWO distinct checks:

```
userHasAppAccess(coid, uoid, poid) -> bool   # app PRESENT in API response = access (any status)
isAppActivated(coid, uoid, poid) -> bool     # present AND appcentral_status == "Activated" = ready/usable
```

Consumer obligations (all generated code):

- **Singleton HTTP client** per process (`IHttpClientFactory` in .NET, module-level `httpx.AsyncClient` in Python, shared axios/undici instance in Node) - never a client per request.
- **Bounded cache**, default TTL 60 seconds (configurable), keyed `(coid, uoid)`.
- **Fail closed** on non-2xx or network errors (deny) by default; the policy is configurable and documented in the generated code. Log failures with a correlation ID where the project has one.
- **Token type by context**: User Delegated token when a user token is in scope; POID-Service token (via the app's default service account) for backend/machine-to-machine.
- **Reuse existing config**: per-environment IAM base URL, the app's `poid`, and existing service-account credentials from the project's own conventions. NEVER introduce new secret material.
- **Never call IAM from browser code with service credentials.** Frontend consumes availability data from its own backend (backend-for-frontend); if the project is frontend-only, report the backend dependency instead of inventing direct IAM calls.

Generated-code sketches (adapt names to project conventions):

C#:

```csharp
public sealed class IamAppsClient(IHttpClientFactory factory, IMemoryCache cache, IamOptions opts)
{
    public async Task<bool> UserHasAppAccessAsync(string coid, string uoid, string poid)
        => (await GetAppsAsync(coid, uoid)).Any(a => a.Poid == poid); // presence = access, any status

    public async Task<bool> IsAppActivatedAsync(string coid, string uoid, string poid)
        => (await GetAppsAsync(coid, uoid)).Any(a => a.Poid == poid && a.AppcentralStatus == "Activated");

    private async Task<IReadOnlyList<AppRecord>> GetAppsAsync(string coid, string uoid)
        => await cache.GetOrCreateAsync($"iam-apps:{coid}:{uoid}", async e =>
        {
            e.AbsoluteExpirationRelativeToNow = opts.CacheTtl; // default 60s
            var resp = await factory.CreateClient("iam")
                .GetAsync($"/iam/api/v1/clients/{coid}/users/{uoid}/apps");
            if (!resp.IsSuccessStatusCode) { /* log failure */ return []; } // fail closed by default (configurable)
            return await resp.Content.ReadFromJsonAsync<List<AppRecord>>() ?? [];
        }) ?? [];
}
```

Python:

```python
_client = httpx.AsyncClient(base_url=settings.IAM_BASE_URL)  # process singleton
_cache = TTLCache(maxsize=1024, ttl=settings.IAM_APPS_CACHE_TTL)  # default 60s, key (coid, uoid)

async def _get_apps(coid: str, uoid: str) -> list[dict]:
    key = (coid, uoid)
    if key in _cache:
        return _cache[key]
    try:
        resp = await _client.get(f"/iam/api/v1/clients/{coid}/users/{uoid}/apps")
        resp.raise_for_status()
    except httpx.HTTPError:
        logger.warning("IAM apps lookup failed", extra={"coid": coid, "uoid": uoid})
        return [] if settings.IAM_FAIL_CLOSED else _cache.get(key, [])  # fail closed by default
    _cache[key] = resp.json()
    return _cache[key]

async def user_has_app_access(coid: str, uoid: str, poid: str) -> bool:
    return any(a["poid"] == poid for a in await _get_apps(coid, uoid))  # presence = access

async def is_app_activated(coid: str, uoid: str, poid: str) -> bool:
    return any(a["poid"] == poid and a["appcentral_status"] == "Activated"
               for a in await _get_apps(coid, uoid))
```

Node/TS:

```typescript
const cache = new TTLCache<string, AppRecord[]>({ ttl: config.iamAppsCacheTtlMs ?? 60_000 });

async function getApps(coid: string, uoid: string): Promise<AppRecord[]> {
  const key = `${coid}:${uoid}`;
  const hit = cache.get(key);
  if (hit) return hit;
  try {
    const { data } = await iamHttp.get(`/iam/api/v1/clients/${coid}/users/${uoid}/apps`); // shared axios instance
    cache.set(key, data);
    return data;
  } catch (err) {
    logger.warn({ err, coid, uoid }, "IAM apps lookup failed");
    return config.iamFailClosed !== false ? [] : (cache.get(key) ?? []); // fail closed by default
  }
}

export const userHasAppAccess = async (coid: string, uoid: string, poid: string) =>
  (await getApps(coid, uoid)).some((a) => a.poid === poid); // presence = access, any status

export const isAppActivated = async (coid: string, uoid: string, poid: string) =>
  (await getApps(coid, uoid)).some((a) => a.poid === poid && a.appcentral_status === "Activated");
```

#### 4b. Rewrite classified call sites

- **(a) app-access-gating** -> `userHasAppAccess`. Deny ONLY when the app is absent from the API response. When the app is present but not Activated, the user HAS access - surface a "not ready" state appropriate to the app (never a 403/denial).
- **(b) app-visibility** -> `isAppActivated` and/or `appcentral_status` data from the API response (which also carries `app_url`, display names, grouping and ordering fields for UI).
- **(c) fine-grained-role-check** -> NO rewrite, in any mode. These token roles remain valid (Activated role-bearing non-workspace app); include them in the report as "still valid".

Previously migrated sites (already calling the helpers) are recognized and skipped, keeping the command idempotent.

### 5. Verify

1. Add or update tests covering the four scenarios:

| Scenario | Roles in token | Expected outcome |
|----------|----------------|------------------|
| App Activated | present (role-bearing app) | access granted AND usable |
| App present, non-Activated status (Setup/InProgress/Inactive) | absent | access EXISTS (not a denial); app reported as not ready |
| App absent from API response | absent | access denied |
| Internal/Workspace app | never present | access granted via API presence alone |

2. Run the project's test suite and report results honestly - never claim success without a passing run. Never claim frontend behavior works without Playwright (or equivalent) proof.
3. Idempotency check: state that immediately re-running this command must yield verdict `already-adopted` with zero findings pending.

### 6. Summary

```
Internal App Token Change Adoption

  Verdict:     applicable (poid: <poid>, internal app)
  Findings:    <n> total - <a> access-gating, <b> visibility, <c> fine-grained (kept, still valid)
  Migrated:    <m> call sites -> userHasAppAccess / isAppActivated
  Generated:   IAM apps client (<stacks>), 60s TTL cache, fail-closed
  Tests:       <t> scenario tests added, suite <pass/fail>

Reminder: token permission inclusion ends 2026-09-04.
```

Part A is now complete - that was step 6 of 12. **Continue to step 7 (Part B).** It has not run yet.

## Execution Steps 7-12 (Part B) - AppCentral `/app/` URL move

AppCentral is relocating deployed applications from

```
https://appcentral.<dev|qa|int>.apteancloud.dev/<app_code>/
```

to

```
https://appcentral.<dev|qa|int>.apteancloud.dev/app/<app_code>/
```

Insert the `app/` segment **everywhere the base path appears** so nothing breaks: routing, ingress, frontend build config, runtime env vars, auth redirect URIs, manifests, tests and docs. Apps deployed via **ACA (Azure Container Apps)** and via **Kubernetes (Helm + AppCentral)** must both be able to adopt this.

**This is a prefix insertion, never a rewrite of the whole path.** `/<app_code>/api` becomes `/app/<app_code>/api`. Sub-segments, `/api` suffixes and per-container micro-frontend segments are preserved exactly.

### 7. Detect applicability and deployment path (read-only)

Resolve `app_code`, in order:

1. From `$ARGUMENTS` if supplied.
2. From `config/MainManifest.yml` -> `iamDetails.code` (lowercased).
3. From the ACA scaffold: the value that replaced `simpleapp` in `nginx/nginx.conf` / `infra/main.bicep`.
4. Otherwise ask. Do NOT proceed on a guess - every edit keys off this value.

Determine the deployment path by evidence. A monorepo may show both; handle each service independently:

| Signal | Deployment path |
|---|---|
| `infra/main.bicep`, `.github/workflows/deploy-container.yml`, `deploy-all.yml`, `nginx/nginx.conf`, `docker-entrypoint.sh` | **ACA** |
| `helm/**`, `helm.*.deploy.yml`, `config/MainManifest.yml` with `deployments[]` | **Kubernetes** |

Verdict `not-applicable` when neither is present, or `already-adopted` when every occurrence is already under `/app/`.

### 8. Scan and classify (read-only)

Search for the base path, then classify every hit. Report counts per bucket before changing anything.

**Both deployment paths:**

| What | Where | Change |
|---|---|---|
| AppCentral routing registration | `config/MainManifest.yml` | route/path entries gain the `app/` prefix |
| Frontend base path | `next.config.js\|ts` (`basePath`, `assetPrefix`), `vite.config.*` (`base`), `svelte.config.*` (`paths.base`) | `/<app_code>` -> `/app/<app_code>` |
| Client router | React Router `basename`, Vue/Angular base href, `<base href>` in `index.html` | same |
| Runtime env | `BASE_PATH`, `NEXT_PUBLIC_BASE_PATH`, `PUBLIC_URL`, `VITE_BASE_PATH` in `.env*`, workflow env blocks, Helm/ACA env | same |
| Auth | IAM redirect URI, post-logout redirect URI, allowed origins/callbacks | same |
| Tests | Playwright `baseURL`, Cypress `baseUrl`, E2E fixtures | same |
| Docs | `README.md`, `docs/**` public URLs | same |

**ACA only:**

| File | What to change |
|---|---|
| `nginx/nginx.conf` | `location /<app_code>/` -> `location /app/<app_code>/`; also `sub_filter`, `rewrite`, `proxy_pass`, `try_files` that embed the prefix |
| `docker-entrypoint.sh` | the base-path substitution written into the built assets |
| `Dockerfile` | any `ENV`/`ARG` carrying the base path |
| `infra/main.bicep` | ingress route / path mapping |
| `.github/workflows/deploy-container.yml`, `deploy-all.yml` | the route registered with AppCentral and any printed URL |

**Kubernetes only:**

| File | What to change |
|---|---|
| `helm/<service>/values-<service>.yml` | **every** ingress path entry - micro-frontends have one per container, change all of them, not just the first |
| `helm/<service>/templates/ingress.yaml` | path templates that hardcode the prefix |
| `helm.*.deploy.yml`, `helm.upr.*.deploy.yml` | any path/route inputs in the matrix |
| HAProxy / Kong route registration steps | the registered route |

Leave `ingressClassName` and the `#{ingress_class_name}#` token alone - routing class is unrelated to this change.

**Never touch**: `/iam/...` paths, `https://studio.apteancloud.dev`, `https://workflow.apteancloud.dev`, or any other service's routes. Those are separate hosts and are NOT moving.

### 9. Findings gate (no Part B writes before confirmation)

Print a table of file, line, current value, new value. Then STOP and ask for confirmation. Nothing is written before an explicit yes.

### 10. Apply

For each edit:

- Skip if the value already contains `/app/` - re-running must never yield `/app/app/<app_code>/`.
- Preserve everything after the app code (`/api`, `/admin`, per-container segments, trailing slash or its absence).
- Preserve quoting, indentation and file encoding.
- Anything whose pattern is unfamiliar goes to "Needs manual review" rather than being edited on a guess.

### 11. Verify

1. No remaining `/<app_code>` occurrence that is not preceded by `/app`.
2. No `/app/app/` anywhere.
3. Helm values, `config/MainManifest.yml`, and the frontend config still parse.
4. The manifest route matches the ingress paths.

Run the build and the E2E suite if the project has them. A frontend whose `basePath` moved but whose router `basename` did not will build cleanly and 404 at runtime, so a passing build alone is NOT sufficient - state explicitly whether tests were run.

### 12. Summary

```
AppCentral /app/ URL Move

  Verdict:      applicable | already-adopted | not-applicable
  App code:     <app_code>
  Deployment:   ACA | Kubernetes | both
  Files changed: <n>
  Old URL:      https://appcentral.dev.apteancloud.dev/<app_code>/
  New URL:      https://appcentral.dev.apteancloud.dev/app/<app_code>/

  Needs manual review:
    <file>:<line>  <reason>

  Verification:  build <passed|failed|not run>, tests <passed|failed|not run>
```

The URL only changes once the app is redeployed. Tell the user to run `/polaris.ship` (or their normal deploy workflow) after merging, and that the code change and the redeploy should land together.

## Operating Principles

- **Zero writes before each gate**: not-applicable, already-adopted, and declined runs modify nothing - but they never end the command early. Part A and Part B each get their own detection, gate and summary.
- **Access is presence, Activated is readiness**: never conflate them; present-but-not-Activated is never a denial.
- **Part A is exactly three adoptions**: no in-app authorization redesign, no new secrets, no browser-direct IAM calls, no auth changes beyond the Scope rules.
- **Part B is a prefix insertion only**: never a broader routing redesign, and never a change to IAM/Studio/Workflow hostnames.
- **Cross-platform only**: agent tools (Read/Grep/Glob) or `python -c` one-liners; no `find`/`grep`/`sed`/bash-isms.
- **Report category (c) but never touch it**: fine-grained role checks in Activated role-bearing apps stay as-is.

## Context

{ARGS}


**Telemetry**: Run: `polaris telemetry record adopt-internalapp-change --feature <slug> --phase complete --agent {{AGENT_NAME}}`
