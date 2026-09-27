# Deploy Phase Rules

## PR Auto-Merge Rules

Used by Step 6 below. These rules govern how the agent safely auto-merges the devops scaffolding PR before triggering pipelines.

**Pre-merge validation (MANDATORY):**
1. Verify the PR exists and is open: `gh pr view <number> --json state --jq .state`
2. Verify the PR contains only infrastructure files. Allowed paths: `.github/workflows/*.yml`, `helm/**`, `infrastructure/**`, `manage-appConfig-secrets/**`, `config/MainManifest.yml`, Dockerfile fixes. If the PR contains application source code changes: STOP with "PR contains non-infrastructure changes. Please review and merge manually."
3. Verify the PR was created by the devops scaffolding (branch matches `feature-*-devops` or `automation/scaffold-pipelines`). If not, warn but allow (user may have renamed the branch).

**Merge command:** `gh pr merge <pr-number> --squash`

**Post-merge verification (MANDATORY):**
1. Wait 5 seconds for GitHub propagation.
2. Verify workflow files exist on default branch: `gh api repos/{owner}/{repo}/contents/.github/workflows/docker-build-push-<ServiceName>.yml --jq .name`
3. If verification fails: STOP with "Workflow files not found on default branch after merge."

**Merge failure handling:**
- Required reviewers -> "PR requires reviewer approval. Approve in GitHub then re-run /polaris.deploy."
- Branch protection -> "Branch protection prevents auto-merge. Merge the PR manually then re-run."
- Merge conflicts -> "PR has merge conflicts. Resolve conflicts and re-run."
- On ANY merge failure: do NOT proceed to pipeline triggering.

**Safety rules:**
- ONLY merge PRs created by `/polaris.devops`.
- Use `--squash` to keep main branch history clean.
- Do NOT use `--delete-branch` (keep the branch for reference).
- The agent MUST NOT force-merge or bypass branch protection.

---

## Pipeline Trigger Rules

Used by every `gh workflow run` call in Steps 7 and 8 below.

**Trigger pattern:** `gh workflow run "<workflow-file>.yml" [-f <input>=<value>]`

| Workflow | Command | Cardinality |
|---|---|---|
| Build | `gh workflow run "docker-build-push-<ServiceName>.yml"` | Once, env-agnostic |
| Infra | `gh workflow run "infra.<ServiceName>.deploy.yml" -f environments=<env>` | Per environment |
| Helm | `gh workflow run "helm.<ServiceName>.deploy.yml" -f environments=<env>` | Per environment |

**Polling for run completion (MANDATORY after every trigger):**
1. After triggering, wait 5 seconds before polling for run ID (GitHub needs time to register).
2. Poll: `gh run list --workflow "<workflow-file>.yml" --limit 1 --json databaseId,status --jq ".[0]"`
3. If no run ID found: retry up to 6 times with 10-second intervals.
4. Once run ID obtained, watch until NATURAL completion: `gh run watch <run-id>`. Do NOT set an artificial timeout.
5. Check the exit code of `gh run watch`:
   - Exit code 0: pipeline SUCCEEDED. Move to the next step IMMEDIATELY.
   - Non-zero: pipeline FAILED. See failure handling below.

**Completion detection (CRITICAL):**
- When `gh run watch` exits with code 0, the pipeline is DONE. The agent MUST trust the exit code as the definitive signal.
- Do NOT re-trigger the same workflow after success.
- Do NOT poll `gh run list` again after a successful watch.
- Do NOT re-check status. Mark the step completed and proceed.

**Failure handling:**
- On non-zero exit: display "Pipeline <name> failed.", display the run URL, ask "Retry this step? (Yes/No)". If Yes: re-trigger from step 1 above. If No: stop deployment and report which steps completed.

**Step tracking (MANDATORY):**
- Maintain `[build, infra-dev, keyvault-dev, helm-dev, infra-tst, keyvault-tst, helm-tst]`.
- On retry, skip all steps already marked completed.
- The build step completes ONCE for all environments.
- A step is marked completed the moment `gh run watch` exits with code 0.
- NEVER re-execute a step that is already marked completed.

**Execution order (STRICT):**
1. `docker-build-push-<ServiceName>.yml` (once, env-agnostic)
2. `infra.<ServiceName>.deploy.yml` (per environment)
3. KeyVault update (per environment, not a pipeline)
4. `helm.<ServiceName>.deploy.yml` (per environment)

Steps 2-4 repeat for each environment. The agent MUST NOT start step N+1 until step N succeeds.

**Environment ordering (STRICT):**
- dev MUST complete fully (infra + keyvault + helm) before tst is offered.
- The agent MUST NOT deploy to tst before dev succeeds.
- After dev completes, ask: "Deploy to QA (tst)? (Yes/No)".
- If the user passes `all`: deploy dev then tst without prompting.

---

## KeyVault Helm Values Update Rules

Used by Step 8b below. The agent does NOT call `az` locally. Instead it
triggers the central **Update Helm Values DATABASE_URL** workflow at
`Aptean-Labs/polaris-devops`, which authenticates as the per-environment
Service Principal and updates the helm-values secret in the env's Key
Vault. The agent MUST NOT ask the user for the connection string and MUST
NOT display it.

**Trigger pattern:**

```
gh workflow run update-helm-values.yml \
  -R Aptean-Labs/polaris-devops \
  -f target_owner=<owner> \
  -f target_repo=<repo> \
  -f service_name=<ServiceName> \
  -f environment=<env>
```

`<owner>` and `<repo>` come from `gh repo view --json owner,name`.
`<ServiceName>` comes from Step 2 (the build pipeline filename pattern).
`<env>` is `dev`, `tst`, or `prd-a` (matching the current deploy iteration).

**What the workflow does (informational; the agent does not need to know
the internals):**

1. Logs into Azure as the per-env SPN (`<PFX>_AZURE_CREDENTIAL_AUTOMATION`,
   org-level secret in Aptean-Labs).
2. Reads the postgres connection-string template from the per-env vault
   (`<PFX>_AZURE_KEYVAULT_URL` selects the vault; the secret name is
   `vars.POSTGRES_TEMPLATE_SECRET_NAME` on `polaris-devops`, typically
   `postgres-master-template`).
3. Substitutes `{db-name}` -> `<ServiceName>-db-<env>`.
4. Reads the existing `helm-values-<ServiceName>-eastus-<env>` secret in
   the same vault (creates if absent), merges `DATABASE_URL` into the
   YAML, preserves all other fields, writes back.

**Polling and watching (MANDATORY after every trigger):**

1. Wait 5 seconds, then resolve the run ID:
   ```
   gh run list --workflow update-helm-values.yml \
     --repo Aptean-Labs/polaris-devops \
     --limit 1 --json databaseId --jq ".[0].databaseId"
   ```
   If no run ID, retry up to 3 times with 5-second intervals.

2. Watch:
   ```
   gh run watch <run-id> --repo Aptean-Labs/polaris-devops
   ```

3. On non-zero exit, fetch logs and surface them:
   ```
   gh run view <run-id> --log-failed --repo Aptean-Labs/polaris-devops
   ```
   Then STOP with the run URL. Do NOT retry automatically. Common causes:
   missing org secret, missing repo secret, missing repo variable
   (`POSTGRES_TEMPLATE_SECRET_NAME`), SPN lacks Secrets Officer role on
   the env's vault. None are fixable from the agent.

**Security rules (CRITICAL):**

- The agent MUST NOT display the connection string. The workflow runner
  masks it with `::add-mask::`; the agent never sees it either.
- The agent MUST NOT log the resolved DATABASE_URL.
- The agent MAY display: "DATABASE_URL resolved and updated for <env> via
  central workflow run #<run-id>".

**Preservation rules:**

The workflow's merge step preserves every existing field in the
helm-values secret and only adds or updates `DATABASE_URL`. The agent
does not need to enforce this; the workflow does.

**Failure modes the agent surfaces (does NOT auto-fix):**

- Workflow run fails with `Repo variable 'POSTGRES_TEMPLATE_SECRET_NAME' is not set` -> platform team fixes polaris-devops repo variables.
- Workflow run fails with `Repo secret '<PFX>_AZURE_KEYVAULT_URL' is missing` -> platform team fixes polaris-devops repo secrets.
- Workflow run fails with `Failed to read template secret` -> SPN lacks Secrets User on the env vault, or the template secret does not exist yet. Platform team fixes RBAC and/or creates the secret.
- Workflow run fails with `Failed to write helm-values secret` -> SPN lacks Secrets Officer on the env vault. Platform team fixes RBAC.
- Workflow run fails with `Template ... does not contain the {db-name} placeholder` -> the template secret in the env vault was set incorrectly. Platform team re-creates the secret with the correct format.
