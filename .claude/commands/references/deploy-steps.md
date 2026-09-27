## Step 5 - Ensure code is committed, pushed, and on main

**5a. Check for uncommitted changes:**

Run: `git status --porcelain`

If there are uncommitted changes (output is not empty):
- Display: "Uncommitted changes detected. Committing for deployment..."
- Stage all changes: `git add -A`
- Commit: `git commit -m "chore: prepare for deployment"`
- Display: "Changes committed."

**5b. Check current branch:**

Run: `git branch --show-current`

If NOT on main (or the default branch):
- Check if there are commits ahead of main: `git log main..HEAD --oneline`
- If commits exist on the feature branch:
  - Push the current branch: `git push origin <branch-name>`
  - Check if a PR exists for this branch: `gh pr list --head <branch-name> --state open --json number --jq '.[0].number'`
  - If no PR exists: create one: `gh pr create --base main --head <branch-name> --title "Deploy code changes" --body "Auto-created by /polaris.deploy for deployment."`
  - Merge the PR: `gh pr merge <number> --squash`
  - Switch to main: `git checkout main && git pull`
  - Display: "Code merged to main."
- If no commits ahead of main: just switch to main: `git checkout main && git pull`

If already on main:
- Check for unpushed commits: `git log origin/main..HEAD --oneline`
- If unpushed commits exist: push them: `git push origin main`
- Display: "Code is up to date on main."

---

## Step 6 - Detect and merge open devops PR (if needed)


Check if workflow files exist on the default branch:
`gh api repos/{owner}/{repo}/contents/.github/workflows/docker-build-push-<ServiceName>.yml --jq .name 2>/dev/null`

If workflow files already exist on the default branch: skip this step (PR already merged or workflows were added directly). Proceed to Step 7.

If workflow files do NOT exist on the default branch:

1. Search for open PRs from the devops scaffolding (sorted by newest first):
   `gh pr list --state open --json number,headRefName,title,createdAt --jq '[.[] | select(.headRefName | test("devops|scaffold"))] | sort_by(.createdAt) | reverse'`
   This filters for PRs with branch names containing "devops" or "scaffold" and sorts newest first.

2. If exactly ONE matching PR is found:
   Display: "Found open PR #<number>: <title>. Workflow files must be on the default branch before pipelines can be triggered."
   Ask: "Merge PR #<number> to enable deployment? (Yes/No)"

3. If MULTIPLE matching PRs are found:
   Display all matches in a numbered list:
   ```
   Multiple devops PRs found:
     1. PR #<number> - <title> (created <date>) [LATEST]
     2. PR #<number> - <title> (created <date>)
     3. PR #<number> - <title> (created <date>)
   ```
   Recommend the latest one (item 1).
   Ask: "Which PR should be merged? Enter the number (1-N), or 'skip' to merge manually."
   Use the selected PR for the merge step.

4. If a PR is selected for merge:
   Follow the **PR Auto-Merge Rules** from `@references/deploy-rules.md`.
   - Merge: `gh pr merge <number> --squash`
   - Wait 5 seconds for GitHub propagation
   - Verify workflow files now exist on default branch
   - If merge fails: display error with instructions and STOP

5. If no matching PRs found and workflows not on default branch:
   STOP with "Workflow files not found on default branch and no open devops PR. Run /polaris.devops first."

---

## BGM Detection

Scan `.github/workflows/` for files matching `bgm.*.deploy.yml` or `bgm.*.swap.yml`.

If found, display:
```
Note: This repository uses Blue-Green (BGM) deployment. BGM automated
deployment is not supported in this version. Standard deployment will
proceed. Trigger BGM workflows manually from GitHub Actions after completion.
```

Continue with standard deployment regardless.

---

## Failure Recovery

The agent MUST track completed pipeline steps throughout execution.
Completed steps: [build, infra-dev, keyvault-dev, helm-dev, infra-tst, keyvault-tst, helm-tst]

On any pipeline failure:
1. Display: "Step '<step-name>' failed. Run URL: <url>"
2. Ask: "Retry this step? (Yes/No)"
3. If Yes: re-run only the failed step, then continue from there
4. If No: display summary of completed vs. remaining steps and STOP

Previously completed steps are NEVER re-executed.
The build step is completed once for all environments.

---

## Execution

Follow the **Pipeline Trigger Rules** from `@references/deploy-rules.md` for every `gh workflow run` call below.

### Step 7 - Trigger build pipeline

> **Skip gate (scaffold-only mode):** If `SCAFFOLD_ONLY = true` (initiated via `/polaris.ship`), do NOT trigger any build or deploy pipeline. Skip Steps 7, 8, and all subsequent pipeline steps.
>
> Display the completion message:
> ```
> Application scaffolding and deployment configuration completed successfully.
> Use the Polaris Ship App to deploy the application to the target environment.
> ```
>
> Then run the **IAM Validation** checks and STOP.
>
> **Check 1 - IAM skill applied:**
> ```
> python -c "import sys,pathlib; sys.exit(0 if pathlib.Path('.polaris/skills/iam.md').exists() else 1)"
> ```
>
> **Check 2 - IAM integration in codebase:**
> ```
> python -c "
> import sys,pathlib
> p=pathlib.Path('.')
> found=((p/'src'/'auth').is_dir() or (p/'apps'/'backend'/'src'/'auth').is_dir() or (p/'.env.iam.example').exists() or (p/'apps'/'backend'/'.env.iam.example').exists())
> sys.exit(0 if found else 1)
> "
> ```
>
> **Check 3 - IAM configuration present:**
> ```
> python -c "
> import sys
> from pathlib import Path
> found=any(f.exists() and any(k in f.read_text('utf-8','ignore') for k in ['IAM_JWKS_URL','APTEAN_IAM_','IAM_TOKEN_ISSUER','IAM_AUDIENCE']) for f in [Path('.env.example'),Path('.env'),Path('apps/backend/.env.example'),Path('apps/backend/.env')])
> sys.exit(0 if found else 1)
> "
> ```
>
> If all 3 return exit code 0: STOP cleanly. If ANY check fails, display and then STOP:
> ```
> ⚠️ IAM validation failed.
>
> IAM is not integrated or a valid IAM registration was not found for this application.
>
> Integrate IAM and complete IAM registration before deploying to AppCentral.
> ```

1. Trigger: `gh workflow run "docker-build-push-<ServiceName>.yml"`
2. Wait 5 seconds, then poll for run ID:
   `gh run list --workflow "docker-build-push-<ServiceName>.yml" --limit 1 --json databaseId --jq ".[0].databaseId"`
3. Watch until completion: `gh run watch <run-id>`
4. If failed: display run URL, offer retry (see Failure Recovery above)
5. If succeeded: mark "build" as completed

The build runs ONCE regardless of how many environments will be deployed.

### Step 8 - Deploy to environment

Determine the deployment mode based on Step 5:
- If Step 6 merged a devops PR: this is a **first-time deployment**. Run all sub-steps (8a, 8b, 8c).
- If Step 6 was skipped (workflows already on main): this is a **re-deployment after code changes**. Skip 8a (infra) and 8b (keyvault), run only 8c (helm deploy). Infrastructure and DATABASE_URL are already configured from the first deployment.

For each target environment (dev first, then tst if requested):

**8a. Trigger infrastructure pipeline (FIRST-TIME ONLY - skip for re-deployments):**

`gh workflow run "infra.<ServiceName>.deploy.yml" -f environments=<env>`

Wait for completion. If failed: offer retry. Mark "infra-<env>" as completed.

**8b. Update DATABASE_URL in KeyVault (FIRST-TIME ONLY - skip for re-deployments):**

Follow the **KeyVault Helm Values Update Rules** section above. The agent
does NOT call `az` locally and does NOT need Azure access. The agent
triggers the central workflow and watches its run.

1. Determine `<owner>` and `<repo>` from `gh repo view --json owner,name`.

2. Trigger the central update-helm-values workflow:
   ```
   gh workflow run update-helm-values.yml \
     -R Aptean-Labs/polaris-devops \
     -f target_owner=<owner> \
     -f target_repo=<repo> \
     -f service_name=<ServiceName> \
     -f environment=<env>
   ```

3. Wait 5 seconds, then resolve the run ID:
   ```
   gh run list --workflow update-helm-values.yml \
     --repo Aptean-Labs/polaris-devops \
     --limit 1 --json databaseId --jq ".[0].databaseId"
   ```
   Retry up to 3 times with 5-second intervals if no run ID returned.

4. Watch the run until it finishes:
   ```
   gh run watch <run-id> --repo Aptean-Labs/polaris-devops
   ```

5. On non-zero exit code:
   - Fetch logs: `gh run view <run-id> --log-failed --repo Aptean-Labs/polaris-devops`
   - STOP with: "Update Helm Values workflow failed. Run URL printed above. Common causes: SPN missing Key Vault role, template secret missing in the env vault, repo variable POSTGRES_TEMPLATE_SECRET_NAME not set, repo secret <PFX>_AZURE_KEYVAULT_URL not set. Contact the platform team."
   - Do NOT retry automatically.

6. On success: display "DATABASE_URL resolved and updated for <env> environment via run #<run-id>". The agent does NOT see and does NOT log the connection string itself; the workflow handles it inside the cloud runner.

7. Mark "keyvault-<env>" as completed.

**8c. Trigger helm deploy pipeline (ALWAYS - both first-time and re-deployments):**

`gh workflow run "helm.<ServiceName>.deploy.yml" -f environments=<env>`

If the workflow does not accept an `environments` input (external dependency not yet applied):
trigger without the parameter: `gh workflow run "helm.<ServiceName>.deploy.yml"`
and warn: "Helm workflow deploys to all environments. Per-environment control
requires updating the reference repo template."

Wait for completion. Mark "helm-<env>" as completed on success.

If the helm deploy FAILS, fetch the failed-step log BEFORE offering retry:
```
gh run view <run-id> --log-failed
```
Then inspect the output for a pre-upgrade hook failure:
- If the log contains `pre-upgrade hooks failed` / `BackoffLimitExceeded` /
  `job ... failed`, the failure is the MIGRATION JOB, not the helm release.
  A blind retry will fail identically. Surface the captured migrate-job pod
  logs to the user. If the deploy workflow includes the "Capture Helm hook
  job logs on failure" diagnostics step (scaffolded by /polaris.devops), the
  migrate pod's real error (the `describe job` + pod logs) is in the run log
  just after the failed helm step - quote the relevant lines to the user.
  If that diagnostics step is absent, tell the user the workflow predates the
  diagnostics step and to re-run /polaris.devops to add it, or pull logs
  manually from the cluster:
  `kubectl -n <release>-<env> logs -l job-name=<migrate-job> --all-containers --tail=-1`
- Only AFTER surfacing the real error, offer retry (see Failure Recovery above).
  Do NOT auto-retry a migration-hook failure - the underlying cause (bad
  connection string, migration SQL error, unreachable DB) must be fixed first.

### Step 9 - Environment progression

Based on the user input argument:

- `dev` (default): Deploy to dev only. After success, ask:
  "Dev deployed successfully. Deploy to QA (tst)? (Yes/No)"
  If Yes: repeat Step 8 for tst.
  If No: display summary and STOP.

- `tst`: Deploy to dev first (if not already deployed to dev). Then deploy to tst.
  The agent MUST verify dev is deployed before proceeding to tst.
  If dev deployment status cannot be determined: deploy to dev first.

- `all`: Deploy to dev, then automatically to tst (no prompt between environments).

Environment ordering is STRICT: dev MUST complete fully before tst begins.

### Step 10 - Display summary

```
DEPLOY SUMMARY:
  Service: <ServiceName>
  Repository: <owner>/<repo>
  PR Merge: Merged PR #<number> / Already merged
  Build: Completed (run #<id>)
  Dev:
    Infrastructure: Completed/Skipped (run #<id>)
    KeyVault: DATABASE_URL updated/skipped
    Helm Deploy: Completed/Skipped (run #<id>)
  QA (tst):
    Infrastructure: Completed/Skipped (run #<id>)
    KeyVault: DATABASE_URL updated/skipped
    Helm Deploy: Completed/Skipped (run #<id>)

Service is live. Check environment URLs in GitHub Actions run output.
```

**Telemetry**: Run: `polaris telemetry record deploy --feature <slug> --phase complete --agent claude`
