---
description: Trigger CI/CD pipelines to deploy a service to dev/tst environments.
---

## User Input

**Telemetry**: Run: `polaris telemetry record deploy --feature <slug> --phase start --agent {{AGENT_NAME}}`

```text
$ARGUMENTS
```

You **MUST** consider the user input before proceeding (if not empty).
Parse environment from arguments: `dev` (default), `tst`, or `all`.

If `$ARGUMENTS` contains `--scaffold-only`, set `SCAFFOLD_ONLY = true`. This means the flow was initiated via `/polaris.ship` (provisioning-only mode). In scaffold-only mode the agent will stop after Step 6 and NOT trigger any build or deploy pipelines.

---

## Pre-flight Checks

### Step 1 - Load Skill Rules (BLOCKING)

Read `.polaris/skills/devops-pipeline.skill.md` for cross-cutting invariants (APP CONFIG SCOPING, SERVICE NAMING, MAINMANIFEST GENERATION RULES, etc.) that apply during deploy operations.

If skill file is missing, HALT with:
```
ERROR: Required skill file missing: devops-pipeline.skill.md
Run `polaris upgrade` to deploy skill files, then retry /polaris.deploy.
```

Load `@references/deploy-rules.md` for the PR auto-merge rules, pipeline trigger rules, and KeyVault Helm values update rules.

### Step 2 - Detect ServiceName

Scan `.github/workflows/` for files matching `docker-build-push-*.yml`.
Extract ServiceName from the filename pattern (e.g., `docker-build-push-simpleapp.yml` -> `simpleapp`).

If no matching workflow found, check for an open PR from a devops branch:
- Run: `gh pr list --state open --json number,headRefName,title --jq '.[] | select(.headRefName | test("devops|scaffold"))'`
- If an open PR is found: display the PR info and proceed to Step 5 (PR merge)
- If no PR found: STOP with "No build pipeline found. Run /polaris.devops first."

If multiple matching workflows found: list them and ask user to select.

### Step 3 - Detect repository

Run: `gh repo view --json nameWithOwner --jq .nameWithOwner`

### Step 4 - Verify authentication

Verify GitHub CLI:
Run: `gh auth status`
If not authenticated: STOP with "GitHub CLI not authenticated. Run `gh auth login` first."

Azure CLI is NOT required by this command. All Azure-touching work runs
inside GitHub Actions:
- Deploy pipelines (build, infra, helm) authenticate as the runtime SPN
  using the `AZURE_CREDENTIAL_AUTOMATION` env-level secret that was
  populated by the Polaris Bootstrap workflow during `/polaris.devops`.
- The DATABASE_URL update (Step 8b below) triggers the central
  `update-helm-values.yml` workflow at `Aptean-Labs/polaris-devops`,
  which logs in as the per-env SPN and updates the helm-values secret in
  the env's Key Vault.

The agent on the laptop only uses `gh` (GitHub CLI) to trigger workflows
and watch their runs. The developer never needs Azure CLI installed or
personal Azure access for any step in this command.

---

## Steps 5-10

Load `@references/deploy-steps.md` and execute Steps 5 through 10.

**Telemetry**: Run: `polaris telemetry record deploy --feature <slug> --phase complete --agent {{AGENT_NAME}}`
