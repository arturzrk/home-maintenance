## Autopilot Context (unattended ship)

When invoked with `AUTOPILOT_RUN` and an approved `autopilot:` spec block, ship runs unattended. Every human wait below is replaced by a recorded-default behaviour; interactive (non-autopilot) runs keep the existing prompts unchanged. Load `@references/escalation.md` for the escalation contract.

- **PR-merge waits (Steps 2A.6 and 2A.8, "Tell me when you have merged the PR")**: do NOT wait for a human. If config `ship.auto_merge: true`, run `gh pr merge <n> --auto` right after creating the PR. Then POLL:
  ```bash
  gh pr view <n> --json state,mergedAt
  ```
  every 30s up to `ship.merge_timeout_minutes` (default 30). Proceed when `state` is `MERGED`. On timeout, run `polaris agent escalate --feature <slug> --stage ship --reason "PR #<n> not merged within <T>m"` and STOP (resume with `--resume`).
- **Prisma migration continue/stop (Step 2A.8b)**: do NOT wait. If a migration was generated (or there is nothing to migrate), treat as `continue`. If `PrepareForDeployment.ps1` soft-failed and no migration could be produced, escalate (`--stage ship --reason "migration generation failed"`) rather than proceeding blindly.
- **Env-progression "Deploy to QA?" (Step 2A.10)**: do NOT ask. Follow `autopilot.ship.env_progression` from spec frontmatter (`dev` / `tst` / `all`).

## Step 2K - Kubernetes approach (complex apps)


### Step 2K.0 - nginx HTTPS redirect hardening

Before detecting pipeline state or invoking `/polaris.devops`/`/polaris.deploy`, follow the procedure in **Step 2A.5f** to harden `nginx/map.conf`. If the user's app embeds nginx as its container's reverse proxy (common for K8s apps that route Kong -> nginx -> upstream), this neutralises the `X-Forwarded-Proto: http` redirect bug that causes AppCentral iframes to fail with mixed-content errors. The patch is idempotent.


### Step 2K.1 - Detect Pipeline State


Scan `.github/workflows/` for files matching `docker-build-push-*.yml`.


- If NO matching files found: state is **NO_PIPELINES**
- If files found: state is **PIPELINES_EXIST**


### Step 2K.2 - Route Based on State


**If NO_PIPELINES:**


Display: "No CI/CD pipelines found. Running DevOps scaffolding..."


Load and follow the `/polaris.devops` command template (`.claude/commands/polaris.devops.md`).
Read the entire file and execute ALL steps.


After devops completes, display: "Scaffolding complete. Configuring deployment..."


Then load and follow the `/polaris.deploy` command template (`.claude/commands/polaris.deploy.md`).
Pass `--scaffold-only` in the arguments. The deploy command will execute through Step 6 (merge devops PR) and then stop - no build or deploy pipelines will be triggered.


**If PIPELINES_EXIST:**


Display: "Pipelines found. Starting deployment..."


Load and follow the `/polaris.deploy` command template (`.claude/commands/polaris.deploy.md`).
Pass `--scaffold-only` in the arguments. The deploy command will execute through Step 6 (merge devops PR if needed) and then stop - no build or deploy pipelines will be triggered.
The deploy command will detect if a PR needs merging and handle it automatically.


STOP after this path completes. Do NOT execute Step 2A.


---


## Step 2A - ACR approach (simple apps)

Follow ALL sub-steps below in order.

### Step 2A.0 - Pre-flight checks

Verify tool availability:
- Run: `gh auth status`. If not authenticated, STOP with "GitHub CLI not authenticated. Run `gh auth login` first."
- Detect the repo's default branch: `gh repo view --json defaultBranchRef --jq .defaultBranchRef.name`. Store as `<default-branch>`.

### Step 2A.0b - Check branch and uncommitted changes

**Detect current branch:**
Run: `git branch --show-current`. Store as `<current-branch>`.

**Check for uncommitted changes:**
Run: `git status --porcelain`

If there are uncommitted changes:
- Display: "Uncommitted changes detected. Committing..."
- Stage all changes: `git add -A`
- Commit: `git commit -m "chore: prepare for deployment"`

Store whether the user is on the default branch or a feature branch. This affects how Steps 2A.6 and 2A.8 behave:
- If `<current-branch>` equals `<default-branch>`: set **ON_DEFAULT=true**
- Otherwise: set **ON_DEFAULT=false**

Do NOT switch branches here. Stay on whichever branch the user is on. All scaffolding and prep work happens on the current branch.

### Step 2A.1 - Detect ACR pipeline state

Check for the Scaffold New App workflow on the default branch:
`gh api repos/{owner}/{repo}/contents/.github/workflows/scaffold.yml --jq .name 2>/dev/null`

- If the file is found on the default branch: state is **REDEPLOY** -> continue to Step 2A.1b.
- Otherwise: state is **FIRST_TIME** -> continue to Step 2A.2.

### Step 2A.1b - Pre-flight: SP role coverage on target subscription (REDEPLOY only, best-effort)

This pre-flight catches the most common deploy failure: someone edited
`infra/environments/<env>.json` to repoint at a different subscription or resource
group, but the GitHub Environment's `AZURE_CREDENTIAL_AUTOMATION` secret still
references a Service Principal that has no role on the new target. Without this
check, the deploy-all pipeline burns ~70s and fails with the misleading error
`ERROR: Subscription '<guid>' not found` - that string actually means "the SP
authenticated, but has zero visible role assignments on the subscription", not
"the subscription does not exist".

This step is **best-effort**: if any of the lookups below fail (file missing,
variable missing, az read denied), log the reason and CONTINUE. Do NOT block the
ship; the user can still trigger the deploy and read pipeline logs the old way.
Only STOP when the role check definitively shows zero role coverage.

Skip this entire step when Step 2A.1 reports FIRST_TIME - the scaffold pipeline
is what grants roles, so the SP has nothing yet by definition.

For each environment the user is about to deploy to (`dev` if argument is `dev`
or `tst`; both `dev` and `tst` if argument is `tst` or `all`):

**Step 2A.1b.1 - Read target subscription, resource group, and ACR from the env JSON**

```
python -c "
import json, pathlib, sys
env = '<env>'
p = pathlib.Path(f'infra/environments/{env}.json')
if not p.exists():
    print(f'SKIP: {p} not found'); sys.exit(0)
try:
    data = json.loads(p.read_text(encoding='utf-8'))
except Exception as e:
    print(f'SKIP: cannot parse {p}: {e}'); sys.exit(0)
def pick(d, *keys):
    for k in keys:
        v = d.get(k)
        if isinstance(v, dict) and 'value' in v:
            return v['value']
        if isinstance(v, str) and v:
            return v
    return ''
params = data.get('parameters', data) if isinstance(data, dict) else {}
sub = pick(params, 'subscriptionId', 'subscription', 'subId') or pick(data, 'subscriptionId', 'subscription', 'subId')
rg = pick(params, 'resourceGroup', 'resourceGroupName', 'rg') or pick(data, 'resourceGroup', 'resourceGroupName', 'rg')
acr = pick(params, 'acrName', 'containerRegistry', 'acr') or pick(data, 'acrName', 'containerRegistry', 'acr')
print(f'SUB={sub}')
print(f'RG={rg}')
print(f'ACR={acr}')
"
```

If `SUB` is empty, log "Cannot determine target subscription from infra/environments/<env>.json; skipping SP role check for <env>" and continue to the next environment (or the next step if no more envs).

**Step 2A.1b.2 - Resolve the SP object ID from the GitHub Environment**

The GitHub Environment `AZURE_CREDENTIAL_AUTOMATION` secret value is masked and
unreadable from the laptop. By convention, the simple-app-template stores the SP
object ID alongside the secret as an environment **variable** named
`AZURE_CREDENTIAL_AUTOMATION_SP_ID`.

```
gh api repos/{owner}/{repo}/environments/<env>/variables/AZURE_CREDENTIAL_AUTOMATION_SP_ID --jq .value 2>/dev/null
```

If the API returns an error or empty body, log "AZURE_CREDENTIAL_AUTOMATION_SP_ID variable not set on GitHub environment <env>; skipping SP role pre-flight (the scaffold pipeline can populate this variable on the next FIRST_TIME run)." and continue to the next environment. Absence is NOT a hard stop.

If the variable IS set, store the value as `<SPObjectId>`.

**Step 2A.1b.3 - Verify role coverage on the target subscription**

```
az role assignment list --assignee <SPObjectId> --scope /subscriptions/<SUB> --query "[].roleDefinitionName" -o tsv
```

Interpret the result:

- If `az` exits non-zero with `AuthorizationFailed`, `InsufficientPrivileges`, or `does not have authorization to perform action 'Microsoft.Authorization/roleAssignments/read'`: the **user's** local az login lacks read on the target subscription. Log "Cannot read role assignments on subscription <SUB> as the current az user; skipping SP role check for <env> (this is a limitation of your az login, not a deploy blocker)." and continue.
- If `az` exits non-zero with any other error: log "az role assignment list failed for <env>: <error>; continuing without pre-flight." and continue.
- If `az` exits 0 with **empty output**: STOP with the message below.
- If `az` exits 0 with one or more role names that include either `Contributor`, `Owner`, or a role granting both `Microsoft.Resources/deployments/*` and `Microsoft.Authorization/roleAssignments/write`: log "SP role coverage verified for <env>: <comma-separated-roles>" and continue.
- If `az` exits 0 with role names but none of the privileged roles above: STOP with the message below, including the existing roles for context.

**STOP message (when role coverage is missing):**

```
Pre-flight failed: SP <SPObjectId> has insufficient role coverage on subscription <SUB> for environment <env>.

Visible roles: <comma-separated-roles or "(none)">
Target RG:    <RG>

Without Contributor + User Access Administrator on the target RG, the
deploy-all pipeline will fail with the misleading error:
  ERROR: Subscription '<SUB>' not found

Grant the missing roles, then re-run /polaris.ship:

  az role assignment create --assignee <SPObjectId> \
    --role "Contributor" \
    --scope /subscriptions/<SUB>/resourceGroups/<RG>

  az role assignment create --assignee <SPObjectId> \
    --role "User Access Administrator" \
    --scope /subscriptions/<SUB>/resourceGroups/<RG>

The User Access Administrator grant is required because the Bicep template
assigns AcrPush during deployment; Contributor alone is not sufficient.
```

Repeat the check for each target environment before proceeding to Step 2A.8.

### Step 2A.2 - Clone the simple-app template (FIRST_TIME)

Clone the template into a SYSTEM TEMP directory OUTSIDE the user's repo. The clone must NOT live inside the user's project folder.

**Compute the clone path (deterministic, system temp):**

Run this bash block. It prints one absolute path that is valid on Linux, macOS, and Windows (git-bash). Every later step in this section uses the same value by re-running the same `python -c` expression.

```
CLONE_DIR="$(python -c "import tempfile, os; print(os.path.join(tempfile.gettempdir(), 'polaris-ship-clone'))")"
echo "Clone dir: $CLONE_DIR"
```

Every bash block below that references `$CLONE_DIR` MUST start by computing it with the same `python -c` expression, so the value is consistent across fresh shells.

**Pre-clone cleanup** (remove any stale clone from a previous ship attempt):

```
CLONE_DIR="$(python -c "import tempfile, os; print(os.path.join(tempfile.gettempdir(), 'polaris-ship-clone'))")"
python -c "import shutil, pathlib, sys; p=pathlib.Path(sys.argv[1]); (shutil.rmtree(p) if p.exists() else None)" "$CLONE_DIR"
```

**Clone:**

```
CLONE_DIR="$(python -c "import tempfile, os; print(os.path.join(tempfile.gettempdir(), 'polaris-ship-clone'))")"
git clone --depth 1 --filter=blob:none https://github.com/Shared-Technology-Group/simple-app-template "$CLONE_DIR"
```

If the clone fails with an auth error, STOP and ask the user to run `gh auth login` or configure git credentials for Shared-Technology-Group. Do NOT silently substitute a different source.

**Verify the clone contents:**

```
CLONE_DIR="$(python -c "import tempfile, os; print(os.path.join(tempfile.gettempdir(), 'polaris-ship-clone'))")"
python -c "
import pathlib, sys
root = pathlib.Path(sys.argv[1])
if not root.is_dir():
    sys.exit(f'ERROR: {root} was not created by git clone')
required = [
    '.github/workflows/scaffold.yml',
    '.github/workflows/deploy-all.yml',
    '.github/workflows/deploy-infra.yml',
    '.github/workflows/deploy-container.yml',
    '.github/workflows/deploy-migrations.yml',
    'Dockerfile', 'docker-entrypoint.sh', '.dockerignore', '.gitignore',
    'Manifest/MainManifest.yml',
    'infra/main.bicep', 'infra/environments', 'infra/modules',
    'nginx/nginx.conf', 'nginx/map.conf',
    'scripts/PrepareForDeployment.ps1', 'scripts/scaffold.sh',
    'docs', 'README.md', 'CLAUDE.md',
]
missing = [r for r in required if not (root / r).exists()]
if missing:
    print('MISSING in', root)
    for m in missing: print(' -', m)
    sys.exit(1)
print('all clone paths verified under', root)
" "$CLONE_DIR"
```

The expected layout inside `$CLONE_DIR` matches the upstream repo at `https://github.com/Shared-Technology-Group/simple-app-template` exactly. Scripts live at `scripts/PrepareForDeployment.ps1` and `scripts/scaffold.sh` relative to the repo root.

If any path is missing, STOP and report the exact paths printed by the verification script. Do NOT retry the clone into a different directory.

### Step 2A.3 - Auto-detect backend and frontend folders (FIRST_TIME)

Determine which top-level folders in the user's repo contain the backend and frontend source code.

1. **Preferred**: if `./backend/` and `./frontend/` both exist, use them.
2. **Otherwise**: scan each top-level folder (excluding `.git`, `.github`, `.polaris`, `node_modules`, `dist`, `build`, `infra`, `nginx`, `scripts`, `docs`, `Manifest`, `apps`, `helm`, `migrations`, `alembic`):
   - Read `package.json` if present. Classify as **backend** if any of `express`, `fastify`, `nest`, `@nestjs/*`, `koa`, `hapi` appear in dependencies. Classify as **frontend** if any of `react`, `vite`, `next`, `vue`, `svelte`, `@angular/*` appear, or if a `vite.config.*`, `next.config.*`, `vue.config.*`, `svelte.config.*` file exists.
   - For Python/other stacks without package.json, ask the user explicitly.
3. If exactly one backend folder and one frontend folder are found, use them.
4. If zero or ambiguous matches, ASK the user:
   ```
   Could not unambiguously detect backend/frontend folders.
   Which folder contains the backend source code? (e.g. ./server)
   Which folder contains the frontend source code? (e.g. ./web)
   ```

Confirm the detected paths with the user before continuing:
```
Detected backend: ./<backend>
Detected frontend: ./<frontend>
Proceed with moving these to apps/backend/ and apps/frontend/? (Yes/No)
```

If No, ask for corrected paths.

### Step 2A.4 - Gather app metadata (FIRST_TIME)

Ask the user for three values (required by the Scaffold New App pipeline):
- **app_code**: lowercase alphanumeric, 2-12 chars, starts with a letter (e.g. `billing`). Must NOT be `simpleapp`.
- **app_name**: human-readable name (e.g. `Billing App`).
- **description**: one-line description of the application.

Validate `app_code` locally using the same regex the scaffold workflow uses: `^[a-z][a-z0-9]{1,11}$`. If invalid, re-prompt.

### Step 2A.5 - Restructure the repo (FIRST_TIME)

Perform these operations in the user's repo root. Never touch files inside the detected backend/frontend folders other than moving them.

1. Create the `apps/` directory if it does not exist:
   ```
   python -c "from pathlib import Path; Path('apps').mkdir(exist_ok=True)"
   ```
2. Move the detected backend folder to `apps/backend/`:
   ```
   python -c "import shutil; shutil.move('<backend>', 'apps/backend')"
   ```
3. Move the detected frontend folder to `apps/frontend/`:
   ```
   python -c "import shutil; shutil.move('<frontend>', 'apps/frontend')"
   ```
4. Copy every file from the clone into the user's repo, EXCEPT the `apps/` directory (user source must be preserved), the `.git/` directory, and the user-owned documentation files `README.md`, `CLAUDE.md`, and `docs/` (the user's project docs MUST NOT be overwritten by the template's reference content). Use Python for cross-platform safety. `$CLONE_DIR` is the absolute system temp path computed in Step 2A.2 (recompute it with the same `python -c` expression at the start of this bash block):
   ```
   CLONE_DIR="$(python -c "import tempfile, os; print(os.path.join(tempfile.gettempdir(), 'polaris-ship-clone'))")"
   python -c "
   import shutil, pathlib, sys
   src = pathlib.Path(sys.argv[1])
   dst = pathlib.Path('.')
   SKIP_TOP = {'.git', 'apps', 'docs', 'README.md', 'CLAUDE.md'}
   for item in src.rglob('*'):
       rel = item.relative_to(src)
       parts = rel.parts
       if parts and parts[0] in SKIP_TOP:
           continue
       target = dst / rel
       if item.is_dir():
           target.mkdir(parents=True, exist_ok=True)
       else:
           target.parent.mkdir(parents=True, exist_ok=True)
           shutil.copy2(item, target)
   " "$CLONE_DIR"
   ```
   This copies `.github/`, `Dockerfile`, `docker-entrypoint.sh`, `nginx/`, `infra/`, `Manifest/`, `scripts/`, `.dockerignore`, `.gitignore` and overwrites any existing non-source pipeline files in the repo root with the template versions. The user's `README.md`, `CLAUDE.md`, and `docs/` are intentionally NOT copied so the user's own project documentation is preserved (or absent if the user has none); none of these files are read by any deploy workflow.
5. Delete the cloned temp directory:
   ```
   CLONE_DIR="$(python -c "import tempfile, os; print(os.path.join(tempfile.gettempdir(), 'polaris-ship-clone'))")"
   python -c "import shutil, pathlib, sys; p=pathlib.Path(sys.argv[1]); (shutil.rmtree(p) if p.exists() else None)" "$CLONE_DIR"
   ```

Verify after copy: `.github/workflows/scaffold.yml`, `.github/workflows/deploy-all.yml`, `Dockerfile`, `apps/backend/`, `apps/frontend/` all exist.

### Step 2A.5b - Replace `simpleapp` references with the user's app_code (FIRST_TIME)

The template files that were just copied contain `simpleapp` hardcoded in several places. The agent MUST rewrite them locally to the user's `<app_code>` BEFORE committing and BEFORE triggering any pipeline. Do not rely on the scaffold pipeline to do this - pre-renaming locally guarantees the first commit and every pipeline run see correct values.

Run this Python block from the user's repo root. It performs the same replacements that the template's `scripts/scaffold.sh` would do, but tolerates files that are absent because the user's `apps/backend/` and `apps/frontend/` are their own code (not the template's sample code):

```
python -c "
import os, re, sys, pathlib
app_code = '<app_code>'
app_name = '''<app_name>'''
description = '''<description>'''

# Resolve repo URL for the Manifest replacement
import subprocess
try:
    repo_url = subprocess.check_output(['gh', 'repo', 'view', '--json', 'url', '-q', '.url'], text=True).strip()
except Exception:
    repo_url = ''

def patch(path, ops, required=False):
    p = pathlib.Path(path)
    if not p.exists():
        if required:
            print('MISSING REQUIRED FILE:', path); sys.exit(1)
        print('  skip missing:', path)
        return
    text = p.read_text(encoding='utf-8')
    for old, new in ops:
        if hasattr(old, 'sub'):
            text = old.sub(new, text)
        elif callable(new):
            text = re.sub(old, new, text)
        else:
            text = text.replace(old, new)
    p.write_text(text, encoding='utf-8')
    print('  updated:', path)

S = 'simpleapp'

# Files the user explicitly listed - all must exist after template overlay
for p in [
    'nginx/nginx.conf',
    'Dockerfile',
    'docker-entrypoint.sh',
    'infra/main.bicep',
    '.github/workflows/deploy-container.yml',
    '.github/workflows/deploy-all.yml',
]:
    patch(p, [(S, app_code)], required=True)

# MainManifest.yml - multiple fields, all required
patch('Manifest/MainManifest.yml', [
    ('name: \"Simple App\"', f'name: \"{app_name}\"'),
    (re.compile(r'description: \"[^\"]*\"'), f'description: \"{description}\"'),
    ('url: https://github.com/Shared-Technology-Group/vp-simple-app.git', f'url: {repo_url}' if repo_url else 'url: https://github.com/Shared-Technology-Group/vp-simple-app.git'),
    ('- name: sampleapp', f'- name: {app_code}'),
    (S, app_code),
], required=True)

# CLAUDE.md is intentionally not patched here: the overlay step does not copy it
# from the template, so any existing CLAUDE.md belongs to the user and must not
# be modified. Skipping the patch keeps user-authored content untouched.

# Template sample app files - only patch if present (user's apps/ likely replaces them)
for p in [
    'apps/frontend/vite.config.ts',
    'apps/backend/src/index.ts',
]:
    patch(p, [(S, app_code)], required=False)
patch('apps/frontend/src/App.tsx', [
    (S, app_code),
    ('>Simple App</h1>', f'>{app_name}</h1>'),
], required=False)
"
```

After replacement, verify no `simpleapp` references remain in the 7 critical files:
```
git grep -n "simpleapp" -- nginx/ Dockerfile docker-entrypoint.sh infra/main.bicep .github/workflows/deploy-container.yml .github/workflows/deploy-all.yml Manifest/MainManifest.yml || echo "OK: no simpleapp references remain"
```

If `git grep` finds any matches, display them, STOP, and report which file still contains `simpleapp`. Do not proceed to commit until the list is empty.

The `simpleapp` -> `<app_code>` substitution is **identity only** - it renames the
app, it does not set the route. Step 2A.5b-2 sets the route, because `simpleapp`
appears in both roles and prefixing all of them would corrupt resource names.

### Step 2A.5b-2 - Set the AppCentral `/app/` base path (FIRST_TIME)

AppCentral serves deployed applications under an `/app/` prefix. The public URL is
`https://appcentral.<env>.apteancloud.dev/app/<app_code>/`, so the app must be
served from `/app/<app_code>/`, not `/<app_code>/`. Without this the ship summary
prints a URL the deployment does not answer on.

Apply the prefix ONLY where the value is a route or a URL. Leave every place
`<app_code>` is an identifier - container, image, resource and Key Vault names -
exactly as Step 2A.5b left it.

| File | What carries the route |
|---|---|
| `nginx/nginx.conf` | `location` blocks, plus any `sub_filter`, `rewrite`, `proxy_pass` or `try_files` embedding the prefix |
| `docker-entrypoint.sh` | the base path substituted into the built frontend assets |
| `Dockerfile` | any `ENV`/`ARG` carrying the base path (`BASE_PATH`, `NEXT_PUBLIC_BASE_PATH`, `PUBLIC_URL`) |
| `infra/main.bicep` | the ingress route / path mapping |
| `.github/workflows/deploy-container.yml`, `deploy-all.yml` | the route registered with AppCentral and any printed URL |
| `Manifest/MainManifest.yml` | the route/path entry |

Also set the frontend build's own base path (Next.js `basePath` + `assetPrefix`,
Vite `base`, or the router `basename`) to `/app/<app_code>`. A route that moved
without the build's base path moving builds cleanly and then 404s every asset at
runtime, so a green build is NOT evidence this worked.

Verify:
```
git grep -n "/<app_code>" -- nginx/ Dockerfile docker-entrypoint.sh infra/main.bicep .github/workflows/ Manifest/ | grep -v "/app/<app_code>" || echo "OK: every route carries the /app/ prefix"
```

Classify each remaining hit - a missed route, or an identifier that correctly has
no prefix. Never produce `/app/app/<app_code>`.

### Step 2A.5c - Patch the copied `scripts/scaffold.sh` (FIRST_TIME)

The template's `scripts/scaffold.sh` expects `apps/frontend/vite.config.ts`, `apps/backend/src/index.ts`, and `apps/frontend/src/App.tsx` to exist. In the user's repo these files may not exist because `apps/backend/` and `apps/frontend/` hold the user's own code. Without a guard, the Scaffold New App pipeline's `scaffold.sh` run crashes with `FileNotFoundError`.

Make a small edit to the LOCAL copy of `scripts/scaffold.sh` (not the upstream template) to skip files that do not exist. Use your file-editing tool to replace this block:

```python
def patch(path, ops):
    """Apply a list of (old_str, new_str) replacements or (regex, repl_fn) to a file."""
    with open(path) as f:
```

with this block (adds an existence check before the `open` call):

```python
def patch(path, ops):
    """Apply a list of (old_str, new_str) replacements or (regex, repl_fn) to a file."""
    import os
    if not os.path.exists(path):
        print(f"  skip missing: {path}")
        return
    with open(path) as f:
```

If your tooling does not support direct file edits, write the replacement using this helper script saved to a temporary file (this avoids nested-quote escaping, which is error-prone when invoked through `python -c`):

```
cat > /tmp/polaris-patch-scaffold.py << 'POLARIS_EOF'
import pathlib, sys
p = pathlib.Path('scripts/scaffold.sh')
s = p.read_text(encoding='utf-8')
needle = 'def patch(path, ops):\n    """Apply a list of (old_str, new_str) replacements or (regex, repl_fn) to a file."""\n    with open(path) as f:'
repl = 'def patch(path, ops):\n    """Apply a list of (old_str, new_str) replacements or (regex, repl_fn) to a file."""\n    import os\n    if not os.path.exists(path):\n        print(f"  skip missing: {path}")\n        return\n    with open(path) as f:'
if needle not in s:
    sys.exit("scaffold.sh layout changed; aborting")
p.write_text(s.replace(needle, repl), encoding='utf-8')
print('patched scripts/scaffold.sh')
POLARIS_EOF
python /tmp/polaris-patch-scaffold.py
rm -f /tmp/polaris-patch-scaffold.py
```

Verify the edit landed by grepping for the new guard:
```
git grep -n "skip missing:" -- scripts/scaffold.sh
```
Expected output: one match inside `patch()`. If there is no match, STOP and report that the edit did not apply.

If the helper script reports `scaffold.sh layout changed`, STOP and ask the user to inspect `scripts/scaffold.sh` - the upstream template changed the `patch()` signature and the guard needs to be re-targeted. Do NOT commit or trigger any pipeline until the guard is in place and verified.

### Step 2A.5d - Detect database usage and prune DB-dependent pipeline paths (FIRST_TIME)

The template's `deploy-container.yml` unconditionally calls `deploy-migrations.yml`, which runs Prisma migrations and fails with `Could not find Prisma Schema` when the user's app has no database. When the user's app has no DB, the agent MUST:

1. Prune `product.infrastructure.databases` from `Manifest/MainManifest.yml`.
2. Remove any `configuration[]` entries named `DATABASE_URL` from each deployment in the manifest.
3. Gate the `migrate` job in `.github/workflows/deploy-container.yml` so it is skipped when no `apps/backend/prisma/schema.prisma` is present.
4. Also gate the `deploy` job so it still runs when `migrate` is skipped. By GitHub Actions default, a downstream job with `needs:` on a skipped job is itself skipped - without this, the whole deploy-all pipeline would appear green but silently no-op.

Skip the whole step if the user's app DOES have a database (see detection below).

**Detect database usage** in the user's app (now living under `apps/backend/` after Step 2A.5):

```
python -c "
import json, pathlib, re, sys
be = pathlib.Path('apps/backend')
has_db = False
reasons = []

# Prisma
if (be / 'prisma' / 'schema.prisma').exists():
    has_db = True; reasons.append('prisma/schema.prisma exists')

# package.json dependencies
pkg = be / 'package.json'
if pkg.exists():
    try:
        data = json.loads(pkg.read_text(encoding='utf-8'))
        deps = {**data.get('dependencies', {}), **data.get('devDependencies', {})}
        for lib in ('prisma', '@prisma/client', 'typeorm', 'sequelize', 'knex', 'mongoose', 'pg', 'mysql', 'mysql2', 'mongodb'):
            if lib in deps:
                has_db = True; reasons.append(f'package.json uses {lib}')
                break
    except Exception:
        pass

# Python backends
for marker in ('alembic.ini', 'manage.py'):
    if (be / marker).exists():
        has_db = True; reasons.append(f'{marker} found')

# Migration directories
for d in ('migrations', 'alembic', 'db/migrate'):
    if (be / d).is_dir():
        has_db = True; reasons.append(f'{d}/ directory present')

print('HAS_DB=' + ('1' if has_db else '0'))
for r in reasons:
    print('  reason:', r)
"
```

If the script prints `HAS_DB=1`: log the reasons to the user and SKIP the rest of this step (leave the manifest and workflows untouched).

If the script prints `HAS_DB=0`: log "No database detected; pruning DB-dependent pipeline paths" and apply the three modifications below:

**Modification 1: Strip `infrastructure.databases` and `DATABASE_URL` config entries from `Manifest/MainManifest.yml`.**

```
cat > /tmp/polaris-prune-manifest.py << 'POLARIS_EOF'
import pathlib, sys
try:
    from ruamel.yaml import YAML
    yaml = YAML()
    yaml.preserve_quotes = True
    yaml.indent(mapping=2, sequence=4, offset=2)
except Exception:
    import yaml as pyyaml  # pyyaml fallback (loses formatting but still works)
    yaml = None

path = pathlib.Path('Manifest/MainManifest.yml')
if yaml is not None:
    data = yaml.load(path.read_text(encoding='utf-8'))
else:
    data = pyyaml.safe_load(path.read_text(encoding='utf-8'))

product = data.get('product', {}) if isinstance(data, dict) else {}
infra = product.get('infrastructure', {}) if isinstance(product, dict) else {}
if isinstance(infra, dict) and 'databases' in infra:
    infra['databases'] = []
    print('  pruned: product.infrastructure.databases -> []')

deployments = product.get('deployments', []) if isinstance(product, dict) else []
if isinstance(deployments, list):
    for dep in deployments:
        if not isinstance(dep, dict):
            continue
        cfg = dep.get('configuration')
        if not isinstance(cfg, list):
            continue
        before = len(cfg)
        dep['configuration'] = [c for c in cfg if not (isinstance(c, dict) and c.get('name') == 'DATABASE_URL')]
        if len(dep['configuration']) != before:
            print(f'  pruned DATABASE_URL from deployment {dep.get(\"name\", \"?\")}')

if yaml is not None:
    with path.open('w', encoding='utf-8') as f:
        yaml.dump(data, f)
else:
    path.write_text(pyyaml.safe_dump(data, sort_keys=False), encoding='utf-8')

print('manifest updated:', path)
POLARIS_EOF
python /tmp/polaris-prune-manifest.py
rm -f /tmp/polaris-prune-manifest.py
```

**Modification 2: Gate the `migrate` AND `deploy` jobs in `.github/workflows/deploy-container.yml`.** The migrate gate alone would skip the deploy job too (GHA default behavior on skipped upstream), so the deploy job needs its own `if:` that tolerates a skipped migrate.

Use your file-editing tool to replace the `migrate` job block:

```yaml
  migrate:
    name: Migrate (${{ inputs.environment }})
    needs: build
    uses: ./.github/workflows/deploy-migrations.yml
    with:
      environment: ${{ inputs.environment }}
    secrets: inherit
```

with this block (adds `if:` to skip the job when no Prisma schema exists):

```yaml
  migrate:
    name: Migrate (${{ inputs.environment }})
    needs: build
    if: hashFiles('apps/backend/prisma/schema.prisma') != ''
    uses: ./.github/workflows/deploy-migrations.yml
    with:
      environment: ${{ inputs.environment }}
    secrets: inherit
```

AND replace the first three lines of the `deploy` job:

```yaml
  deploy:
    name: Deploy (${{ inputs.environment }})
    needs: [build, migrate]
```

with (adds `if:` so deploy still runs when migrate is skipped, but not when migrate/build fail):

```yaml
  deploy:
    name: Deploy (${{ inputs.environment }})
    needs: [build, migrate]
    if: ${{ !cancelled() && needs.build.result == 'success' && needs.migrate.result != 'failure' }}
```

If your tooling cannot perform direct YAML edits, use this equivalent heredoc script:

```
cat > /tmp/polaris-gate-migrate.py << 'POLARIS_EOF'
import pathlib, sys
p = pathlib.Path('.github/workflows/deploy-container.yml')
s = p.read_text(encoding='utf-8')

# Gate 1: migrate job skips when no schema.prisma
m_needle = '  migrate:\n    name: Migrate (${{ inputs.environment }})\n    needs: build\n    uses: ./.github/workflows/deploy-migrations.yml'
m_repl = '  migrate:\n    name: Migrate (${{ inputs.environment }})\n    needs: build\n    if: hashFiles(\'apps/backend/prisma/schema.prisma\') != \'\'\n    uses: ./.github/workflows/deploy-migrations.yml'

# Gate 2: deploy job runs when migrate is skipped or succeeded, not when it failed
d_needle = '  deploy:\n    name: Deploy (${{ inputs.environment }})\n    needs: [build, migrate]\n    runs-on:'
d_repl = '  deploy:\n    name: Deploy (${{ inputs.environment }})\n    needs: [build, migrate]\n    if: ${{ !cancelled() && needs.build.result == \'success\' && needs.migrate.result != \'failure\' }}\n    runs-on:'

changed = False
if 'hashFiles(\'apps/backend/prisma/schema.prisma\')' in s:
    print('migrate gate already present')
elif m_needle in s:
    s = s.replace(m_needle, m_repl); changed = True
    print('gated migrate job')
else:
    sys.exit('deploy-container.yml migrate block changed; aborting')

if 'needs.build.result ==' in s:
    print('deploy gate already present')
elif d_needle in s:
    s = s.replace(d_needle, d_repl); changed = True
    print('gated deploy job')
else:
    sys.exit('deploy-container.yml deploy block changed; aborting')

if changed:
    p.write_text(s, encoding='utf-8')
    print('wrote deploy-container.yml')
POLARIS_EOF
python /tmp/polaris-gate-migrate.py
rm -f /tmp/polaris-gate-migrate.py
```

**Modification 3: Verify the edits landed.**

```
git grep -n "databases:" -- Manifest/MainManifest.yml
git grep -n "DATABASE_URL" -- Manifest/MainManifest.yml
git grep -n "schema.prisma" -- .github/workflows/deploy-container.yml
git grep -n "needs.build.result" -- .github/workflows/deploy-container.yml
```

Expected:
- `databases: []` in the manifest (or absent).
- No `DATABASE_URL` matches in the manifest (or zero matches in `configuration[]` entries).
- Exactly one `schema.prisma` hit in `deploy-container.yml`, inside the `if:` expression on the migrate job.
- Exactly one `needs.build.result` hit in `deploy-container.yml`, inside the `if:` expression on the deploy job.

If any verification fails, STOP and report the mismatch. Do NOT commit or trigger any pipeline until the manifest + workflow edits are confirmed.

### Step 2A.5f - nginx HTTPS redirect hardening (SHARED by ACR FIRST_TIME, ACR REDEPLOY, and Kubernetes paths)

If `nginx/map.conf` exists in the working tree, harden the `$redirect_scheme` map so nginx never emits `Location: http://...` 301 redirects when Kong (the platform gateway in front of both ACA and AKS) forwards `X-Forwarded-Proto: http` even on HTTPS-originated requests. Without this patch, AppCentral iframes (e.g. `/<app_code>` -> `/<app_code>/`) end up redirecting to `http://` and the browser blocks them as mixed content.

The procedure is idempotent: if the file does not exist, or it is already hardened, it returns immediately.

**Sub-step 1: Detect state**

```
python -c "
import pathlib, sys
p = pathlib.Path('nginx/map.conf')
if not p.exists():
    print('NO_FILE'); sys.exit(0)
if 'host_default_scheme' in p.read_text(encoding='utf-8'):
    print('ALREADY_HARDENED'); sys.exit(0)
print('NEEDS_PATCH')
"
```

- `NO_FILE`: log "nginx/map.conf not found - skipping HTTPS hardening" and return.
- `ALREADY_HARDENED`: log "nginx/map.conf already hardened - skipping" and return.
- `NEEDS_PATCH`: continue to sub-step 2.

**Sub-step 2: Apply the patch**

Write a temporary patch script and run it. The script strips any existing `$redirect_scheme` map (and its leading comment block) and appends two hardened maps: a host-based default that pins any `*.aptean*` host to `https`, and a `$redirect_scheme` map that honours `X-Forwarded-Proto` only when it explicitly says `https`.

```
cat > /tmp/polaris-patch-nginx-https.py << 'POLARIS_EOF'
import pathlib, re, sys

p = pathlib.Path('nginx/map.conf')
if not p.exists():
    print('nginx/map.conf not found - skipping')
    sys.exit(0)

s = p.read_text(encoding='utf-8')
if 'host_default_scheme' in s:
    print('nginx/map.conf already hardened - skipping')
    sys.exit(0)

pattern = re.compile(
    r'(?:^[ \t]*#[^\n]*\n)*'
    r'[ \t]*map\s+\S+\s+\$redirect_scheme\s*\{[^}]*\}\s*\n?',
    re.MULTILINE,
)
s_new = pattern.sub('', s)

hardened = (
    '# Determine scheme for redirect Location headers.\n'
    '#\n'
    '# Kong sometimes forwards X-Forwarded-Proto: http even when the original\n'
    '# browser request was HTTPS. Trusting that value blindly caused\n'
    '# mixed-content blocks when AppCentral embeds the app in an iframe.\n'
    '#\n'
    '# Policy:\n'
    '#   1. Honour X-Forwarded-Proto only when it explicitly says https.\n'
    '#   2. Otherwise, any *.aptean* host always resolves to https.\n'
    '#   3. Fall back to $scheme for local development only.\n'
    'map $host $host_default_scheme {\n'
    '    "~*\\.aptean"  https;\n'
    '    default        $scheme;\n'
    '}\n'
    '\n'
    'map $http_x_forwarded_proto $redirect_scheme {\n'
    '    "https"  https;\n'
    '    default  $host_default_scheme;\n'
    '}\n'
)
p.write_text(s_new.rstrip() + '\n\n' + hardened, encoding='utf-8')
print('nginx/map.conf hardened against Kong X-Forwarded-Proto bug')
POLARIS_EOF
python /tmp/polaris-patch-nginx-https.py
python -c "import pathlib; pathlib.Path('/tmp/polaris-patch-nginx-https.py').unlink(missing_ok=True)"
```

**Sub-step 3: Verify**

```
git grep -n "host_default_scheme" -- nginx/map.conf
```

Expected: exactly 2 matches (one in the `$host_default_scheme` map, one in the `$redirect_scheme` map). If 0 matches and the file exists, STOP and report.

**Sub-step 4: Do NOT commit here**

The parent step's existing `git add -A` and commit handle staging. The patched file is left modified in the working tree for the parent step to pick up.

### Step 2A.6 - Commit, push, and ensure changes are on default branch (FIRST_TIME only)

Stage and commit all scaffolding changes:

```
git add -A
git commit -m "scaffold: adopt simple-app template"
```

**If ON_DEFAULT=true** (user is on default branch):
```
git push origin <default-branch>
```
If push is rejected due to divergence, STOP and display the exact git error; do NOT force-push.
Continue to Step 2A.6b.

**If ON_DEFAULT=false** (user is on a feature branch):
1. Push the feature branch:
   ```
   git push origin <current-branch>
   ```
2. Create a PR:
   ```
   gh pr create --base <default-branch> --head <current-branch> --title "scaffold: adopt simple-app template for <app_code>" --body "Auto-created by /polaris.ship. Includes template scaffolding, restructured apps/ layout, and pipeline workflows."
   ```
3. Store the PR number from the output.
4. Display:
   ```
   PR #<number> created: <pr-url>
   This PR includes the scaffolding changes. Please review and merge it.
   Tell me when you have merged the PR so I can continue with deployment.
   ```
5. WAIT for the user to respond (do NOT exit, do NOT proceed). **Under `AUTOPILOT_RUN`: do NOT wait - apply the bounded PR-merge poll from the Autopilot Context section (auto-merge if `ship.auto_merge`, poll `gh pr view <number> --json state,mergedAt` every 30s up to `ship.merge_timeout_minutes`, escalate on timeout).**
6. When the user confirms (says "done", "merged", "yes", etc.) - or, under autopilot, when the poll observes `state == MERGED`:
   - Verify the PR is merged: `gh pr view <number> --json state --jq .state`
   - If state is "MERGED":
     - Switch to default branch and pull: `git checkout <default-branch> && git pull origin <default-branch>`
     - Display: "PR merged. Continuing with deployment..."
     - Continue to Step 2A.6b.
   - If state is NOT "MERGED":
     - Display: "PR #<number> is not merged yet. Please merge it first."
     - WAIT again for user confirmation (autopilot: keep polling until the timeout, then escalate).

### Step 2A.6b - Pipeline failure auto-fix loop (SHARED by Steps 2A.7 and 2A.9)

Whenever a pipeline run in Step 2A.7 or Step 2A.9 fails, follow this loop instead of asking the user to retry manually. The agent MUST attempt to diagnose and fix the failure autonomously up to 3 times per pipeline before giving up.

**Loop** (applies to each failing run):

1. Record the attempt number (start at 1, max 3 per pipeline).
2. Fetch the failing run's logs:
   ```
   gh run view <run-id> --log-failed > run-<run-id>.log 2>&1
   ```
   If `--log-failed` is unavailable, use `gh run view <run-id> --log`.
3. Fetch the structured job/step metadata:
   ```
   gh run view <run-id> --json jobs,conclusion,displayTitle
   ```
4. Identify the failing job and step by name from the JSON, then locate the error message inside `run-<run-id>.log`.
5. Classify the failure:

   | Category | Examples | Agent action |
   |---|---|---|
   | **File content** | `simpleapp` reference missed, YAML syntax error in a template file, wrong port number, typo in Bicep, missing COPY source, Dockerfile builder stage missing file | Edit the file, commit, push, re-trigger the same workflow, increment attempt counter |
   | **Manifest content** | missing field in `Manifest/MainManifest.yml`, wrong `iamDetails.code`, missing deployment entry, invalid `configuration[].type` | Fix the manifest, commit, push, re-trigger |
   | **Missing repo secret or variable** | ACR flow: `SCAFFOLD_PAT not set`, `SCAFFOLD_KV_CREDENTIAL not found`, `CREDENTIALS_KV_NAME variable missing`. Kubernetes flow: `AZURE_CREDENTIAL_AUTOMATION missing` in target repo env (bootstrap workflow may have failed silently). | STOP with clear instructions for the user to configure the secret/variable, or to re-run the bootstrap workflow if Kubernetes. Do NOT retry. |
   | **Bootstrap workflow failed** (Kubernetes only) | `gh workflow run bootstrap.yml` fails or `gh run watch` returns non-zero, errors like `POLARIS_DEVOPS_PAT org secret missing`, `Org secret '<PFX>_AZURE_CREDENTIAL_AUTOMATION' is empty`, `gh secret set: 403 Forbidden`, target repo not in PAT allow list | STOP and surface the bootstrap workflow's run URL. Common root causes: org secrets not configured in Aptean-Labs, target repo not in 'Selected repositories' for the relevant org secrets, PAT lacks write on target repo. Contact the platform team. Do NOT retry the bootstrap workflow. |
   | **RBAC / permission** | `AuthorizationFailed`, `The client does not have authorization to perform action`, `AcrPush is required`, `KeyVault access denied`, `ERROR: Subscription '<guid>' not found`, `WARNING: Subscription '<guid>' not recognized` | STOP and report the exact missing role plus the target resource. The two "subscription not found / not recognized" strings are misleading: they mean the SPN authenticated successfully but has zero visible role assignments on the target subscription (NOT that the subscription is missing). Emit the **RBAC remediation block** below verbatim, with the SP object id, subscription, and RG substituted from `infra/environments/<env>.json` and the GitHub Environment's `AZURE_CREDENTIAL_AUTOMATION_SP_ID` variable. Do NOT retry. |
   | **Missing Azure resource** | `ACR not found`, `ResourceGroupNotFound`, `server not found` for Postgres | STOP and report the missing resource plus the infra/environments JSON entry that referenced it. Do NOT retry. Note: do NOT classify `Subscription '<guid>' not found` as missing-resource - that string is RBAC, not missing-resource. |
   | **Transient** | `429 Too Many Requests`, `timeout`, `ETIMEDOUT`, `network is unreachable`, `registry unavailable`, Docker Hub rate limit | Wait 30 seconds, re-trigger the same workflow, increment attempt counter. Do NOT edit files. |
   | **Unknown** | anything else | STOP with the failing step name, the last 30 lines of the log, and the run URL. Do NOT retry. |

**RBAC remediation block** (emit verbatim when the failure is classified as RBAC,
substituting `<SPObjectId>`, `<SUB>`, and `<RG>` from `infra/environments/<env>.json`
and the GitHub Environment's `AZURE_CREDENTIAL_AUTOMATION_SP_ID` variable; if the
SP id is unknown, leave the placeholder and tell the user to run
`gh api repos/{owner}/{repo}/environments/<env>/variables/AZURE_CREDENTIAL_AUTOMATION_SP_ID --jq .value`):

```
The pipeline failed an RBAC check. The SP that GitHub Actions logs in as does
not have the role assignments needed on the target resource group.

Note on the error string: `ERROR: Subscription '<SUB>' not found` does NOT
mean the subscription is missing. It means the SP authenticated but has zero
visible role assignments on that subscription.

Grant the missing roles, then re-trigger the deploy:

  az role assignment create --assignee <SPObjectId> \
    --role "Contributor" \
    --scope /subscriptions/<SUB>/resourceGroups/<RG>

  az role assignment create --assignee <SPObjectId> \
    --role "User Access Administrator" \
    --scope /subscriptions/<SUB>/resourceGroups/<RG>

The User Access Administrator grant is required because the Bicep template
assigns AcrPush during deployment; Contributor alone is not sufficient.
```

6. Re-trigger the same workflow ONLY for `File content`, `Manifest content`, and `Transient` categories. Use the same inputs as the original run.
7. Watch the new run with `gh run watch <new-run-id>`.
8. If the new run succeeds, exit the loop and continue the parent step.
9. If the new run fails, loop back to step 1 with attempt counter incremented.
10. If attempt counter exceeds 3, STOP. Report:
    - Every attempt's run URL and failing step name.
    - The last fix the agent applied (if any).
    - The final log excerpt.
    - A one-line recommendation (e.g. "Most likely root cause: SP missing `User Access Administrator` on `<rg>`. Grant the role and re-run `polaris ship`.").

Delete `run-<run-id>.log` files after use.

### Step 2A.7 - Trigger Scaffold New App pipeline (FIRST_TIME only)

Trigger the scaffold workflow with the collected inputs:
```
gh workflow run scaffold.yml -f app_code=<app_code> -f app_name="<app_name>" -f description="<description>"
```

Poll for the run ID (wait 5 seconds, then):
```
gh run list --workflow scaffold.yml --limit 1 --json databaseId --jq ".[0].databaseId"
```

Watch until completion:
```
gh run watch <run-id>
```

If the run fails, enter the **pipeline failure auto-fix loop** defined in Step 2A.6b. Do NOT ask the user to retry manually - the agent owns the retry/fix decision up to 3 attempts.

After success, pull the committed scaffold changes into the local clone:
```
git pull origin <default-branch>
```

---

### Skip gate - scaffolding complete (FIRST_TIME path)

`/polaris.ship` is a provisioning command. All deployment artifacts have been generated and committed by the scaffold pipeline. Do NOT proceed to Step 2A.9 or any subsequent deploy step.

Display the completion message:

```
Application scaffolding and deployment configuration completed successfully.
Use the Polaris Ship App to deploy the application to the target environment.
```

Then run the **IAM Validation** checks and STOP.

#### IAM Validation

**Check 1 - IAM skill applied:**
```
python -c "import sys,pathlib; sys.exit(0 if pathlib.Path('.polaris/skills/iam.md').exists() else 1)"
```

**Check 2 - IAM integration in codebase:**
```
python -c "
import sys,pathlib
p=pathlib.Path('.')
found=((p/'src'/'auth').is_dir() or (p/'apps'/'backend'/'src'/'auth').is_dir() or (p/'.env.iam.example').exists() or (p/'apps'/'backend'/'.env.iam.example').exists())
sys.exit(0 if found else 1)
"
```

**Check 3 - IAM configuration present:**
```
python -c "
import sys
from pathlib import Path
found=any(f.exists() and any(k in f.read_text('utf-8','ignore') for k in ['IAM_JWKS_URL','APTEAN_IAM_','IAM_TOKEN_ISSUER','IAM_AUDIENCE']) for f in [Path('.env.example'),Path('.env'),Path('apps/backend/.env.example'),Path('apps/backend/.env')])
sys.exit(0 if found else 1)
"
```

If all 3 return exit code 0: STOP cleanly.

If ANY check fails, display and then STOP:
```
⚠️ IAM validation failed.

IAM is not integrated or a valid IAM registration was not found for this application.

Integrate IAM and complete IAM registration before deploying to AppCentral.
```

---

### Step 2A.8 - Re-deploy preparation (REDEPLOY only)

The purpose of this step is: if the user edited
`apps/backend/prisma/schema.prisma` since the last commit on the default branch,
generate a new Prisma migration before triggering the deploy pipeline. If the
schema is unchanged, this step is a no-op.

**Prerequisite: nginx HTTPS redirect hardening.** Before any sub-step below, follow **Step 2A.5f** to harden `nginx/map.conf` against Kong's `X-Forwarded-Proto: http` header. The patch is idempotent and a no-op if the file is missing or already hardened from a prior ship.

**Step 2A.8a - Cheap pre-check: did schema.prisma actually change?**

Run:
```
git diff origin/<default-branch>...HEAD -- apps/backend/prisma/schema.prisma
```
Also check uncommitted local changes:
```
git diff -- apps/backend/prisma/schema.prisma
git diff --cached -- apps/backend/prisma/schema.prisma
```

If all three outputs are empty, log "No schema.prisma changes since
<default-branch>; skipping migration generation." and skip directly to Step 2A.9.

If schema.prisma is absent entirely (the app has no Prisma schema), log
"No apps/backend/prisma/schema.prisma; nothing to prepare." and skip to Step 2A.9.

**Step 2A.8b - Run the migration generator (only if Step 2A.8a found changes)**

The simple-app-template overlay ships `scripts/PrepareForDeployment.ps1`. Try it,
but treat failure as soft - the script has known portability problems (UTF-8 BOM
missing, hardcoded SQLite shadow DB) that block legitimate Postgres-backed apps.

Pick the interpreter:

- macOS/Linux: use `pwsh` if installed.
- Windows: prefer `pwsh` (PowerShell 7+) over `powershell.exe` (Windows
  PowerShell 5.1). Windows PowerShell 5.1 reads BOM-less UTF-8 files as ANSI;
  the script contains UTF-8 glyphs (right-pointer, check, cross) and parser-errors at
  line 35 with `Missing closing '}' in statement block`. If only `powershell.exe`
  is available, attempt it but expect this failure mode.

Try (Windows):
```
where pwsh >nul 2>&1 && pwsh -ExecutionPolicy Bypass -File scripts/PrepareForDeployment.ps1 || powershell -ExecutionPolicy Bypass -File scripts/PrepareForDeployment.ps1
```

Try (macOS/Linux):
```
pwsh scripts/PrepareForDeployment.ps1
```

If the script exits 0, continue to Step 2A.8c.

If the script exits non-zero, do NOT hard stop. Display the soft-failure message
below and ask the user how to proceed:

```
WARNING: scripts/PrepareForDeployment.ps1 exited non-zero. Two failure modes are
common and known:

  (a) Encoding: the script contains UTF-8 glyphs but lacks a UTF-8 BOM. Windows
      PowerShell 5.1 (the default `powershell.exe` shipped on every Windows 11
      box) reads it as ANSI and parser-errors at line 35 with
      `Missing closing '}' in statement block`.
      Workaround: install pwsh 7+ via `winget install Microsoft.PowerShell` and
      re-run, or rewrite the script with ASCII glyphs.

  (b) Shadow DB provider mismatch: the script hardcodes
      `--shadow-database-url "file:/tmp/shadow.db"` which only works for SQLite.
      For PostgreSQL-backed apps (provider = "postgresql" in schema.prisma), the
      script fails with `P1010: User '' was denied access on the database
      'tmp.public'.`
      Workaround: generate the migration manually:

          cd apps/backend
          npx prisma migrate dev --create-only --name <descriptive-name>
          git add prisma/migrations
          git commit -m "feat: add <descriptive-name> migration"
          cd -

If you have already generated the migration manually (or there is genuinely
nothing to migrate), type `continue` to proceed to deploy.

If you want to STOP and run the migration before deploying, type `stop`.
```

WAIT for the user to respond. **Under `AUTOPILOT_RUN`: do NOT wait (see Autopilot Context) - if a migration was generated or there is nothing to migrate, treat as `continue`; if no migration could be produced, escalate via `polaris agent escalate --stage ship` and STOP.**

- If the user types `continue` (or any case-insensitive variant): proceed to Step 2A.8c.
- If the user types `stop`: STOP cleanly with "Re-run /polaris.ship after committing the migration."
- Any other response: re-ask once, then STOP if still ambiguous.

**Step 2A.8c - Commit any new migration files**

After the script completes, check for changes:
```
git status --porcelain
```

If there are no changes, display "No migration changes detected; skipping commit." and proceed to **Step 2A.8d** below.

If there are changes:

**If ON_DEFAULT=true** (user is on default branch):
```
git add -A
git commit -m "chore: prepare for deployment"
git push origin <default-branch>
```
Proceed to **Step 2A.8d** below.

**If ON_DEFAULT=false** (user is on a feature branch):
1. Commit to the feature branch:
   ```
   git add -A
   git commit -m "chore: prepare for deployment"
   git push origin <current-branch>
   ```
2. Create a PR:
   ```
   gh pr create --base <default-branch> --head <current-branch> --title "chore: prepare for deployment" --body "Auto-created by /polaris.ship. Includes code changes and deployment preparation."
   ```
3. Store the PR number from the output.
4. Display:
   ```
   PR #<number> created: <pr-url>
   This PR includes your code changes. Please review and merge it.
   Tell me when you have merged the PR so I can continue with deployment.
   ```
5. WAIT for the user to respond (do NOT exit, do NOT proceed). **Under `AUTOPILOT_RUN`: do NOT wait - apply the bounded PR-merge poll from the Autopilot Context section (auto-merge if `ship.auto_merge`, poll `gh pr view <number> --json state,mergedAt` every 30s up to `ship.merge_timeout_minutes`, escalate on timeout).**
6. When the user confirms - or, under autopilot, when the poll observes `state == MERGED`:
   - Verify the PR is merged: `gh pr view <number> --json state --jq .state`
   - If state is "MERGED":
     - Switch to default branch and pull: `git checkout <default-branch> && git pull origin <default-branch>`
     - Display: "PR merged. Scaffolding artifacts are committed."
     - Proceed to **Step 2A.8d** below.
   - If state is NOT "MERGED":
     - Display: "PR #<number> is not merged yet. Please merge it first."
     - WAIT again for user confirmation (autopilot: keep polling until the timeout, then escalate).

### Step 2A.8d - Scaffolding complete (REDEPLOY path) - `/polaris.ship` stops here

All deployment artifacts and migration files have been generated and committed. Do NOT proceed to Step 2A.9 or any subsequent deploy step.

Display the completion message:

```
Application scaffolding and deployment configuration completed successfully.
Use the Polaris Ship App to deploy the application to the target environment.
```

Then run the **IAM Validation** checks and STOP.

#### IAM Validation

**Check 1 - IAM skill applied:**
```
python -c "import sys,pathlib; sys.exit(0 if pathlib.Path('.polaris/skills/iam.md').exists() else 1)"
```

**Check 2 - IAM integration in codebase:**
```
python -c "
import sys,pathlib
p=pathlib.Path('.')
found=((p/'src'/'auth').is_dir() or (p/'apps'/'backend'/'src'/'auth').is_dir() or (p/'.env.iam.example').exists() or (p/'apps'/'backend'/'.env.iam.example').exists())
sys.exit(0 if found else 1)
"
```

**Check 3 - IAM configuration present:**
```
python -c "
import sys
from pathlib import Path
found=any(f.exists() and any(k in f.read_text('utf-8','ignore') for k in ['IAM_JWKS_URL','APTEAN_IAM_','IAM_TOKEN_ISSUER','IAM_AUDIENCE']) for f in [Path('.env.example'),Path('.env'),Path('apps/backend/.env.example'),Path('apps/backend/.env')])
sys.exit(0 if found else 1)
"
```

If all 3 return exit code 0: STOP cleanly.

If ANY check fails, display and then STOP:
```
⚠️ IAM validation failed.

IAM is not integrated or a valid IAM registration was not found for this application.

Integrate IAM and complete IAM registration before deploying to AppCentral.
```

---

### Step 2A.9 - Trigger Deploy All (One-Click) for dev *(SKIPPED when run via `/polaris.ship` - see skip gate at Step 2A.8d and end of Step 2A.7)*

Trigger the deploy-all pipeline for dev:
```
gh workflow run deploy-all.yml -f environment=dev
```

Poll for run ID (wait 5 seconds):
```
gh run list --workflow deploy-all.yml --limit 1 --json databaseId --jq ".[0].databaseId"
```

Watch until completion:
```
gh run watch <run-id>
```

If the run fails, enter the **pipeline failure auto-fix loop** defined in Step 2A.6b. The agent owns the retry/fix decision up to 3 attempts per environment.

### Step 2A.10 - Environment progression

Based on `$ARGUMENTS`:

- `dev` (default): After dev succeeds, ask:
  "Dev deployed successfully. Deploy to QA (tst)? (Yes/No)"
  If Yes, repeat Step 2A.9 for tst (`-f environment=tst`). If No, go to Step 2A.11.
  **Under `AUTOPILOT_RUN`: do NOT ask - follow `autopilot.ship.env_progression` from spec frontmatter (`dev` stops after dev; `tst`/`all` proceed to tst).**

- `tst`: After dev succeeds, automatically proceed to tst. The agent MUST NOT skip dev.

- `all`: After dev succeeds, automatically proceed to tst with no prompt.

Environment ordering is STRICT: dev MUST complete fully before tst begins.

### Step 2A.11 - Display summary

Compute the public URL for each deployed environment using the AppCentral hostname mapping below, then render it inline in the summary. Use `<app_code>` from Step 2A.4.

**AppCentral hostname mapping (per environment):**

| Environment | Hostname | URL pattern |
|---|---|---|
| `dev` | `appcentral.dev.apteancloud.dev` | `https://appcentral.dev.apteancloud.dev/app/<app_code>/` |
| `tst` | `appcentral.qa.apteancloud.dev` | `https://appcentral.qa.apteancloud.dev/app/<app_code>/` |
| `prod` | `appcentral.aptean.com` | `https://appcentral.aptean.com/app/<app_code>/` |

If the user deployed to an environment not listed above (custom env name), omit the `Public URL` line for that env and instead print `Public URL: see GitHub Actions run summary` so the user is not given a fabricated hostname.

Render the summary in this exact form (only include the `Public URL` line for environments that were actually deployed):

```
SHIP SUMMARY (ACR approach):
  Repository: <owner>/<repo>
  Default branch: <default-branch>
  Mode: FIRST_TIME / REDEPLOY
  App code: <app_code> (first-time only)
  Scaffold run: Completed (run #<id>) / Skipped
  Dev:
    Deploy All (One-Click): Completed (run #<id>)
    Public URL: https://appcentral.dev.apteancloud.dev/app/<app_code>/
  QA (tst):
    Deploy All (One-Click): Completed (run #<id>) / Skipped
    Public URL: https://appcentral.qa.apteancloud.dev/app/<app_code>/

Note: the GitHub Actions run summary may show a different (prod) URL because the simple-app-template's `deploy-all.yml` hardcodes `appcentral.aptean.com`. The URLs above are the correct per-environment HAProxy endpoints. The actual HAProxy route is registered per environment by `deploy-infra.yml`'s `register-haproxy-route` action, so the URLs above route correctly even when the workflow's printed URL does not.
```

STOP after the summary is displayed.
