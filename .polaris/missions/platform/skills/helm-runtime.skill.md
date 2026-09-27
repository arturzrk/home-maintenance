---
name: Helm Runtime Skill
description: |
  Governs Kubernetes runtime structure.
---

  ----------------------------------------------------------------------
  ✔ HELM PLACEHOLDER NORMALIZATION
  ----------------------------------------------------------------------

    Helm values files use a special triple-brace placeholder format for image tags.
  
    After token replacement, the agent MUST normalize these placeholders.
  
    Supported pattern (EXACT):
  
      {{{ .ServiceName | replace "-" "_" | upper }}_IMAGE_TAG}
  
    Step 1: Apply ServiceName transform:
      If ServiceName = test-repo, then:
      {{{ .ServiceName | replace "-" "_" | upper }}_IMAGE_TAG}
      → {{{TEST_REPO_IMAGE_TAG}
  
    Step 2: Normalize triple braces to hash format:
      {{{TEST_REPO_IMAGE_TAG}
      → #{TEST_REPO_IMAGE_TAG}#
  
    Final result:
      tag: #{TEST_REPO_IMAGE_TAG}#
  
    Rules:
    - Applies to Helm values files (helm/*/values-*.yml)
    - The opening {{{ (triple brace) MUST be replaced with #{
    - The closing } MUST be replaced with }#
    - Final placeholder MUST be wrapped in #{ ... }#
    - This format allows Azure DevOps variable replacement
    - Any deviation from this pattern → STOP with clear error

  ----------------------------------------------------------------------  
  ✔ HELM RUNTIME CONFIGURATION RULES (MANDATORY)
  ---------------------------------------------------------------------- 
  
  For ALL services (including micro-frontends):
  
  - The Helm chart represents a SINGLE runtime unit.
  
  Rules:
  
  1. `envFrom` MUST be defined ONCE per release.
  2. Secrets and AppConfig references MUST NOT be duplicated
     per container.
  3. Micro-frontend classification MUST NOT result in:
       - Multiple envFrom blocks
       - Multiple secret references
       - Multiple AppConfig references

  4. envFrom MUST use {{ .DeploymentName }} token (MANDATORY):
     The envFrom section in Helm values MUST reference the deployment name
     from config/MainManifest.yml for configMapRef and secretRef names.

     Required structure:
     ```yaml
     envFrom:
       - type: configMapRef
         name: {{ .DeploymentName }}
         fullnameOverride: ""
       - type: secretRef
         name: {{ .DeploymentName }}
         fullnameOverride: ""
     ```

     Where {{ .DeploymentName }} is replaced with the actual deployment name
     from config/MainManifest.yml (e.g., "api", "ui", "worker").

     Example (for a repo with deployments[].name = "api"):
     ```yaml
     envFrom:
       - type: configMapRef
         name: api
         fullnameOverride: ""
       - type: secretRef
         name: api
         fullnameOverride: ""
     ```

     Rules:
     - The name field MUST use DeploymentName, NOT ServiceName
     - fullnameOverride MUST be set to "" (empty string)
     - Both configMapRef and secretRef MUST be present
     - For multi-deployment repos, each deployment's envFrom uses
       its own DeploymentName

  ----------------------------------------------------------------------
  ✔ NAME OVERRIDE RULES (MANDATORY)
  ----------------------------------------------------------------------

  The agent MUST use `nameOverride` by default for all deployments.

  Rules:

  1. `nameOverride` is the DEFAULT behavior.
     - Use: nameOverride: <service-name>
     - This allows Helm release name to be prepended
     - Resource names follow pattern: <release-name>-<nameOverride>

  2. `fullnameOverride` MUST ONLY be used when:
     - Explicitly requested in the user prompt
     - User says "use fullnameOverride" or similar

  3. If fullnameOverride is NOT explicitly requested:
     - ALWAYS use nameOverride
     - Do NOT set fullnameOverride at all

  4. Example values structure:

     # Correct (default behavior):
     nextApplication:
       nameOverride: my-service

     # Only if explicitly requested:
     nextApplication:
       fullnameOverride: exact-resource-name

  ----------------------------------------------------------------------
  ✔ INGRESS PATH STRUCTURE RULES (MANDATORY)
  ----------------------------------------------------------------------

  Ingress path MUST be deterministic and consistent with service type.

  AppCentral serves deployed applications under an `/app/` prefix, so every
  ingress path below begins with `/app/`. A path missing that prefix routes
  nowhere: `https://appcentral.<env>.apteancloud.dev/app/<ServiceName>/...`
  is the address AppCentral publishes.

  1️⃣ Backend Service Ingress Rule:

  If application is classified as Backend Service:
  Ingress path MUST follow:
    /app/{{ .ServiceName }}/api


  Examples:

  /app/<service-name>/api
  /app/<another-service>/api

  Rules:
  - `/api` suffix is mandatory
  - No trailing slash
  - Lowercase only
  - No underscores
  - Must match Helm ingress exactly

  2️⃣ Frontend Service Ingress Rule:

  If application is classified as Frontend Service:
  Ingress path MUST follow:
    /app/{{ .ServiceName }}/<frontend-segment>


  Examples:

  /app/<service-name>/admin
  /app/<service-name>/employee
  /app/<service-name>/dashboard


  Rules:
  - Must NOT contain `/api`
  - Segment must reflect frontend module name
  - Lowercase only
  - No underscores
  - Must match Helm ingress exactly
  - The frontend build MUST be told the same base path: Next.js `basePath`
    (and `assetPrefix`), Vite `base`, or the router `basename`, plus any
    `BASE_PATH` / `NEXT_PUBLIC_BASE_PATH` / `PUBLIC_URL` env the app reads.
    An ingress path that moved without the build's base path moving builds
    cleanly and then 404s every asset at runtime, so this is not optional.

  3️⃣ Micro-Frontend Ingress Rule

  If service is classified as Micro-Frontend:
  Discover ALL ingress paths from Dockerfile.

  Each ingress path MUST:
  - Follow frontend OR backend rule above
  - Be validated
  - Not be auto-generated blindly

  The agent MUST NOT assume:
  - Only /ServiceName exists
  - Only one ingress path exists
  - Only FE or only BE exists

  Each ingress path is processed independently.

  If ingress path violates structure rules:
  → STOP with clear error

  ----------------------------------------------------------------------
  ✔ INGRESS CLASS NAME RULES (MANDATORY)
  ----------------------------------------------------------------------

  The `ingressClassName` field in the Helm values file MUST be a token,
  never hardcoded. The reference template generates `kong-shr` for all
  environments by default. The agent MUST patch this after scaffolding.

  CANONICAL MAPPING (authoritative):
    dev   -> kong-dev
    tst   -> kong-tst
    uat-A -> kong-shr
    prd-A -> kong-prda

  VALUES FILE RULE:
  - `ingressClassName` MUST be `#{ingress_class_name}#` in
    `helm/{{ .ServiceName }}/values-{{ .ServiceName }}.yml`
  - NEVER hardcode `kong-shr` or any other class name directly

  WORKFLOW FILE RULE (applied post-copy, during Step 16):
  - `helm.{{ .ServiceName }}.deploy.yml`:
    - dev matrix entry:   set `ingress_class_name: 'kong-dev'`
    - tst matrix entry:   set `ingress_class_name: 'kong-tst'`
  - `helm.upr.{{ .ServiceName }}.deploy.yml`:
    - uat-A matrix entry: set `ingress_class_name: 'kong-shr'`
    - prd-A matrix entry: set `ingress_class_name: 'kong-prda'`

  VERIFICATION:
  - After patching, confirm no matrix entry in either workflow file
    still reads `kong-shr` for dev, or `kong-shr` for prd-A
  - Confirm values file has `#{ingress_class_name}#` not a literal class name

  ----------------------------------------------------------------------
  ✔ MICRO-FRONTEND BUILD JOB GATING (MANDATORY)
  ----------------------------------------------------------------------
  
  For Micro-Frontend services:
  The agent MUST treat:
   - Build Units = multiple Docker images
   - Runtime Unit = a single Helm release
  
  Build unit rules:
  1. The agent MUST discover ALL micro-frontend build units by inspecting:
   - Repository folder structure
   - Dockerfile stages / arguments
   - Known micro-frontend patterns
  
  2. Each discovered unit MUST have:
   - A dedicated build job
   - A dedicated image name
  
  3. The agent MUST NOT collapse multiple units into a single build job.
  
  Build execution rules (STRICT)
  1. All micro-frontend build jobs MUST be defined statically in the workflow.
  
  2. Build jobs MUST execute conditionally based on:
   - Source code changes (folder-based detection), OR
   - Explicit workflow inputs (e.g. force build all)
  
  3. Default behavior:
     Build ONLY micro-frontends whose source has changed
  
  4. Override behavior:
     A manual input MUST allow forcing ALL builds
  
  5. The agent MUST NOT:
   - Default to building a single primary target
   - Require pipeline regeneration to add or remove build units
  
  Runtime & config rules
  Regardless of the number of micro-frontends:
  - Exactly ONE Helm release
  - Exactly ONE values.yaml
  - Exactly ONE envFrom
  - Shared AppConfig and KeyVault entries
  
  Safety rules
  The agent MUST NOT introduce breaking changes to existing workflows.
  Build selection MUST be achieved via conditions, not pipeline mutation.

  ----------------------------------------------------------------------
  ✔ HELM DEPLOY FAILURE DIAGNOSTICS (MANDATORY WHEN MIGRATION JOB PRESENT)
  ----------------------------------------------------------------------

  Problem this solves:
  A Helm pre-install / pre-upgrade hook Job (the migration job) that fails
  only ever reports `BackoffLimitExceeded` to `helm upgrade`. That string is
  ALL that reaches the workflow log and `gh run view --log-failed`. The
  migration pod's real stdout/stderr (the actual error) is never surfaced,
  so every migrate failure looks identical and is undebuggable from CI.

  Rule:
  When the deployment INCLUDES a migration job (database detected), the agent
  MUST append a failure-diagnostics step to the deploy job in BOTH:
    - helm.{{ .ServiceName }}.deploy.yml
    - helm.upr.{{ .ServiceName }}.deploy.yml

  The step MUST be the LAST step of the job that runs the helm deploy action,
  placed AFTER the `helm.deploy.from-chart-url` (or equivalent helm upgrade)
  step, and MUST be gated on failure so it never runs on success.

  The kubeconfig is already on the runner from the earlier
  `az aks get-credentials` step, so the diagnostics step reuses it directly.

  Required step (Linux runner, bash is correct here - this is generated CI
  YAML that runs on the GitHub-hosted runner, NOT an agent command template):

  ```yaml
  - name: Capture Helm hook job logs on failure
    if: failure()
    continue-on-error: true
    shell: bash
    run: |
      # NS MUST be the SAME namespace value the helm deploy step uses
      # (the per-env namespace, e.g. from the matrix entry). Reuse that
      # expression here - do NOT hardcode a namespace.
      NS="${NAMESPACE}"
      echo "=== Jobs in $NS ==="
      kubectl -n "$NS" get jobs -o wide || true
      echo "=== Failed-job describe + pod logs ==="
      FAILED=$(kubectl -n "$NS" get jobs \
        -o jsonpath='{range .items[?(@.status.failed)]}{.metadata.name}{"\n"}{end}')
      if [ -z "$FAILED" ]; then
        echo "No jobs in failed state; dumping all job pods for context."
        FAILED=$(kubectl -n "$NS" get jobs -o jsonpath='{range .items[*]}{.metadata.name}{"\n"}{end}')
      fi
      for j in $FAILED; do
        echo "----- describe job/$j -----"
        kubectl -n "$NS" describe job "$j" || true
        echo "----- logs job/$j (all containers, full) -----"
        kubectl -n "$NS" logs -l job-name="$j" --all-containers --tail=-1 || true
      done
      echo "=== Recent namespace events ==="
      kubectl -n "$NS" get events --sort-by=.lastTimestamp | tail -40 || true
  ```

  Rules:
  - The step MUST use `if: failure()` so it is a no-op on success.
  - The step MUST use `continue-on-error: true` so diagnostics never mask the
    original failure or change the job's final conclusion.
  - `NS` MUST resolve to the SAME namespace the helm deploy step targets.
    Bind it to the matrix/env namespace variable the workflow already defines.
  - This step is ADDITIVE and non-breaking. It MUST NOT alter the helm deploy
    step, the chart, the matrix, or any existing step.
  - If NO migration job is present (no database detected), this step is NOT
    required (no pre-upgrade hook can fail this way).

  CRITICAL - hook-delete-policy interaction (READ THIS):
  A Helm hook Job whose `helm.sh/hook-delete-policy` includes `hook-failed` is
  DELETED by Helm the instant it fails - synchronously, before `helm upgrade`
  returns its error. The pod (and its logs) are then GONE by the time this
  post-failure step runs, so the diagnostics step captures NOTHING. This is the
  single most common reason a migrate failure is undebuggable.

  Therefore, for the migration Job in the helm values file
  (`helm/{{ .ServiceName }}/values-{{ .ServiceName }}.yml`, the
  `nextApplication.jobs[]` entry):
  - The migration Job's `helm.sh/hook-delete-policy` MUST NOT include
    `hook-failed`. Use `before-hook-creation` (optionally with
    `hook-succeeded`) ONLY. `before-hook-creation` still cleans up the prior
    failed Job at the next deploy, so nothing leaks - but a failed Job SURVIVES
    long enough for this diagnostics step (and `kubectl logs`) to read it.
  - Recommended migration-job policy:
    `helm.sh/hook-delete-policy: before-hook-creation`
  - If the values file ships `hook-failed` in that policy, the agent MUST
    remove `hook-failed` from the migration Job's delete-policy during
    scaffolding so failures stay inspectable.
