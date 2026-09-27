---
description: Apply security and compliance controls to a project using the Anthropic Cybersecurity Skills library.
---


## Model Guidance

Model: impl tier (see references/model-selection.md); routing is enforced by the launcher.

---

## User Input

**Telemetry**: Run: `polaris telemetry record compliance --feature <slug> --phase start --agent claude`

```text
$ARGUMENTS
```

You **MUST** consider the user input before proceeding (if not empty).

Flags:
- `--domain <domain>` - restrict to one domain (e.g. `api-security`, `container-security`, `devsecops`, `auth`, `secrets`, `cicd`, `cloud-security`, `webapp`, `compliance`)
- `--ci` - non-interactive CI gate mode (exits 0 = all critical controls present, 1 = missing controls)
- `--all` - apply all applicable skills without interactive selection

## Goal

Analyse the project stack, present a curated menu of applicable security skills from the
Anthropic Cybersecurity Skills library, and apply the selected skills to the project.

Security controls are sourced live from:
`https://github.com/mukul975/Anthropic-Cybersecurity-Skills`

## Skill Catalogue

The catalogue is organised by domain. Each entry is a skill slug from the repository.

```
CATALOGUE = {
  "devsecops": [
    "implementing-devsecops-security-scanning",
    "integrating-sast-into-github-actions-pipeline",
    "integrating-dast-with-owasp-zap-in-pipeline",
    "implementing-secret-scanning-with-gitleaks",
    "implementing-secrets-scanning-in-ci-cd",
    "implementing-semgrep-for-custom-sast-rules",
    "performing-sca-dependency-scanning-with-snyk",
    "implementing-github-advanced-security-for-code-scanning",
    "implementing-fuzz-testing-in-cicd-with-aflplusplus",
  ],
  "api-security": [
    "implementing-api-gateway-security-controls",
    "implementing-api-rate-limiting-and-throttling",
    "implementing-api-key-security-controls",
    "implementing-api-schema-validation-security",
    "implementing-api-abuse-detection-with-rate-limiting",
    "implementing-api-security-posture-management",
    "securing-api-gateway-with-aws-waf",
    "implementing-jwt-signing-and-verification",
  ],
  "auth": [
    "implementing-passwordless-authentication-with-fido2",
    "implementing-passwordless-auth-with-microsoft-entra",
    "implementing-hardware-security-key-authentication",
    "implementing-saml-sso-with-okta",
    "configuring-oauth2-authorization-flow",
    "implementing-mtls-for-zero-trust-services",
  ],
  "secrets": [
    "implementing-secrets-management-with-vault",
    "implementing-hashicorp-vault-dynamic-secrets",
    "implementing-aes-encryption-for-data-at-rest",
    "implementing-envelope-encryption-with-aws-kms",
    "implementing-digital-signatures-with-ed25519",
    "implementing-rsa-key-pair-management",
  ],
  "container-security": [
    "hardening-docker-containers-for-production",
    "hardening-docker-daemon-configuration",
    "implementing-container-image-minimal-base-with-distroless",
    "performing-container-security-scanning-with-trivy",
    "securing-container-registry-images",
    "securing-container-registry-with-harbor",
  ],
  "kubernetes": [
    "implementing-kubernetes-pod-security-standards",
    "implementing-rbac-hardening-for-kubernetes",
    "implementing-kubernetes-network-policy-with-calico",
    "implementing-container-network-policies-with-calico",
    "securing-kubernetes-on-cloud",
    "securing-helm-chart-deployments",
  ],
  "cicd": [
    "securing-github-actions-workflows",
    "implementing-code-signing-for-artifacts",
    "implementing-supply-chain-security-with-in-toto",
    "implementing-image-provenance-verification-with-cosign",
    "implementing-sigstore-for-software-signing",
    "implementing-infrastructure-as-code-security-scanning",
  ],
  "cloud-security": [
    "securing-aws-iam-permissions",
    "securing-aws-lambda-execution-roles",
    "securing-serverless-functions",
    "implementing-aws-iam-permission-boundaries",
    "implementing-aws-security-hub",
    "implementing-cloud-security-posture-management",
    "implementing-cloud-waf-rules",
    "implementing-ddos-mitigation-with-cloudflare",
  ],
  "webapp": [
    "implementing-web-application-logging-with-modsecurity",
    "implementing-runtime-application-self-protection",
    "performing-threat-modeling-with-owasp-threat-dragon",
    "implementing-dmarc-dkim-spf-email-security",
  ],
  "compliance": [
    "implementing-gdpr-data-protection-controls",
    "implementing-pci-dss-compliance-controls",
    "implementing-iso-27001-information-security-management",
    "implementing-patch-management-workflow",
  ],
  "threat-intelligence": [
    "analyzing-threat-actor-ttps-with-mitre-attack",
    "analyzing-threat-actor-ttps-with-mitre-navigator",
    "analyzing-apt-group-with-mitre-navigator",
    "analyzing-indicators-of-compromise",
    "analyzing-threat-intelligence-feeds",
    "building-detection-rules-with-sigma",
    "building-attack-pattern-library-from-cti-reports",
    "automating-ioc-enrichment",
  ],
}
```

## Execution Steps

### 1. Detect Stack

Scan the project root with Glob/Grep (no bash). Identify:

| Signal | Files to check |
|---|---|
| Language | `pyproject.toml`, `package.json`, `*.csproj`, `go.mod`, `Gemfile` |
| Framework | deps in `package.json`/`pyproject.toml` for `fastapi`, `express`, `nextjs`, `django`, `spring`, `rails` |
| CI platform | `.github/workflows/*.yml` (GitHub Actions), `azure-pipelines.yml` (ADO), `.gitlab-ci.yml` (GitLab) |
| Container | `Dockerfile`, `docker-compose.yml` |
| Kubernetes | `helm/`, `k8s/`, `charts/`, `*.helm.yaml` |
| Cloud | `serverless.yml` (Lambda), `terraform/`, `pulumi/`, `cdk.json` |
| Existing SAST | `codeql`, `semgrep`, `bandit` in any CI yaml |
| Existing secrets scan | `.gitleaks.toml`, `gitleaks` or `trufflehog` in CI yaml |
| Existing SCA | `snyk`, `trivy`, `dependabot.yml`, `safety` in CI yaml |

Report detected stack in a brief summary line before proceeding.

### 2. Build Applicable Skill List

Filter the catalogue by detected stack:

- If `--domain <domain>` was passed: show only skills in that domain
- If no Dockerfile detected: exclude `container-security` and `kubernetes` domains
- If no `.github/workflows/` detected: exclude `implementing-github-advanced-security-for-code-scanning` and `securing-github-actions-workflows`
- If no cloud signals detected: exclude `cloud-security` domain
- If no Kubernetes signals detected: exclude `kubernetes` domain
- Always include `devsecops` skills (universally applicable)

For each skill in the filtered list, check idempotency before presenting:

**Idempotency markers** (skill is "already present" if ANY marker matches):
- `implementing-secret-scanning-with-gitleaks` / `implementing-secrets-scanning-in-ci-cd`: `.gitleaks.toml` exists OR `gitleaks` in CI yaml
- `integrating-sast-into-github-actions-pipeline` / `implementing-devsecops-security-scanning`: `semgrep` or `codeql` in CI yaml
- `performing-sca-dependency-scanning-with-snyk`: `snyk` or `trivy` in CI yaml
- `implementing-jwt-signing-and-verification`: `jsonwebtoken`, `python-jose`, `PyJWT` imported in source files
- `implementing-secrets-management-with-vault` / `implementing-hashicorp-vault-dynamic-secrets`: `VAULT_ADDR` in `.env.example` or source files
- `hardening-docker-containers-for-production`: `USER` directive (non-root) in Dockerfile
- `implementing-kubernetes-pod-security-standards`: `securityContext` in any yaml under `k8s/` or `helm/`

Mark each skill as: `[APPLICABLE]`, `[ALREADY PRESENT]`, or `[NOT APPLICABLE]`.

### 3. Present Skill Selection (or CI gate)

**CI mode** (`--ci` flag):

Check only the critical controls:
1. Secrets scanning: `gitleaks`, `.gitleaks.toml`, `trufflehog` in CI config
2. SAST: `codeql`, `semgrep`, `bandit` in CI config
3. SCA: `snyk`, `trivy`, `dependabot`, `safety` in CI config
4. Container hardening (if Dockerfile exists): non-root `USER` directive in Dockerfile
5. Auth model documented (see `@references/quality-guardrails.md` Auth Cross-Check section): detection signal for "the project has API routes/handlers" is any of `@app.route`, `@router.`, `app.get(`/`app.post(`, `[HttpGet]`/`[HttpPost]`/`[ApiController]`, `@RestController`/`@GetMapping`/`@PostMapping` found in source files. PASS if `.polaris/memory/constitution.md` declares an AuthN/AuthZ model (Q8 answer present and non-empty), FAIL if that route/handler signal is present but no declared model exists, SKIP if no constitution.md exists at all - this is a presence check, not a per-endpoint scan (that level of detail lives in `/polaris.review`'s Security pass and `/polaris.standards`'s Auth category)
6. Bounded concurrency pattern present (see `@references/quality-guardrails.md` Concurrency Cross-Check section): detection signal for background-task/thread-spawning code is any of `ThreadPoolExecutor`, `Thread(`, `threading.Thread`, `asyncio.create_task`, `Executors.newFixedThreadPool`/`newCachedThreadPool`, `new Thread(` found in source files. PASS if that signal is present and shows an explicit bounded cap (a fixed pool size, worker count, or task-group limit) with no daemon thread used for must-complete work, FAIL if that signal is present and exhibits the unbounded-executor anti-pattern (an unbounded `ThreadPoolExecutor`/task queue with no concurrency cap, e.g. `newCachedThreadPool`, `ThreadPoolExecutor(max_workers=None)`, or a bare `Thread`/`threading.Thread` with no pool at all - or a daemon thread used for work that must survive process exit), SKIP if no background-task code is detected at all

Print a structured report:
```
COMPLIANCE GATE REPORT
======================
[PASS] Secrets scanning: gitleaks found in .github/workflows/ci.yml
[FAIL] SAST: no codeql/semgrep/bandit found in CI config
[PASS] SCA: trivy found in .github/workflows/ci.yml
[SKIP] Container hardening: no Dockerfile detected
[PASS] Auth model documented: constitution.md declares OAuth2 + default-deny (Q8)
[SKIP] Bounded concurrency pattern: no background-task code detected

Result: FAIL (1 critical control missing)
Missing: SAST scanning
Suggested fix: /polaris.comply --domain devsecops
```

Exit instructions: tell the user to return exit code 1 if any FAIL is present, 0 if all pass/skip.

**Interactive mode** (default):

Present the applicable skills as a numbered checklist grouped by domain.
Example format:

```
Detected stack: Node.js (Express) + Docker + GitHub Actions

Applicable security skills (12 found, 2 already present):

DevSecOps
  [1] implementing-devsecops-security-scanning       - SAST + DAST + SCA pipeline
  [2] integrating-sast-into-github-actions-pipeline  - CodeQL + Semgrep in CI
  [3] implementing-secret-scanning-with-gitleaks      - already present
  [4] performing-sca-dependency-scanning-with-snyk   - SCA/dependency scanning

API Security
  [5] implementing-api-rate-limiting-and-throttling  - rate-limit patterns
  [6] implementing-jwt-signing-and-verification       - JWT hardening

Container Security
  [7] hardening-docker-containers-for-production     - Docker CIS hardening
  [8] performing-container-security-scanning-with-trivy - Trivy scanning

CI/CD
  [9] securing-github-actions-workflows              - GitHub Actions hardening

Enter numbers to apply (e.g. "1,2,5,7"), "all" to apply all, or "none" to cancel:
```

Wait for user response before proceeding.

If `--all` flag was passed, skip the prompt and apply all `[APPLICABLE]` skills.

### 4. Apply Selected Skills

For each selected skill (in order, skipping `[ALREADY PRESENT]`):

**4a. Fetch SKILL.md**

Fetch using WebFetch from:
```
https://raw.githubusercontent.com/mukul975/Anthropic-Cybersecurity-Skills/main/skills/<slug>/SKILL.md
```

If WebFetch fails (network error), skip the skill and note it in the report.

**4b. Summarise**

Print a one-line summary of what this skill will do to the project before applying:
```
Applying: implementing-api-rate-limiting-and-throttling
  > Adds rate-limiting middleware/configuration to the API layer
```

**4c. Apply**

Read the SKILL.md and execute its instructions for the detected framework. Follow the same
execution model as `/polaris.skill`:
- Create/modify only the files the skill instructs
- Match the project's existing code style and package manager
- Append env vars to `.env.example` (do not write to `.env`)
- Do NOT add external services - scaffold config and placeholder credentials only

**4d. Write guardrail**

Check if a `guardrail.md` exists in the same skill directory:
```
https://raw.githubusercontent.com/mukul975/Anthropic-Cybersecurity-Skills/main/skills/<slug>/guardrail.md
```

If it exists, write it to `.polaris/skills/<slug>.md`.

**4e. Record outcome**

Track each skill as: `applied`, `already present`, `skipped (not applicable)`, `failed`.

Accumulate all files created/modified by this skill into a shared list for the final report. Do NOT print a per-skill "Files created" summary - all file output goes in the final Compliance Report only.

### 5. Compliance Report

After all skills are processed, print the compliance report:

```
============================================================
POLARIS COMPLIANCE REPORT
============================================================

Stack detected: Node.js (Express) + Docker + GitHub Actions

Controls applied (4):
  + implementing-devsecops-security-scanning       [devsecops]
  + implementing-api-rate-limiting-and-throttling  [api-security]
  + hardening-docker-containers-for-production     [container-security]
  + securing-github-actions-workflows              [cicd]

Already present (2):
  ~ implementing-secret-scanning-with-gitleaks    [devsecops]
  ~ implementing-jwt-signing-and-verification      [api-security]

Skipped - not applicable (3):
  - securing-aws-iam-permissions                  [cloud-security] - no cloud signals
  - implementing-kubernetes-pod-security-standards [kubernetes]    - no k8s detected
  - implementing-gdpr-data-protection-controls    [compliance]     - not selected

Controls still missing (manual action needed):
  ! performing-threat-modeling-with-owasp-threat-dragon - run /polaris.comply --domain webapp

Compliance score: 6/9 applicable controls present (67%)

Files created/modified:
  path/to/file1.yml                           - description
  path/to/file2.toml                          - description

Next steps:
  1. Review generated files and fill .env credentials
  2. Run your test suite to verify nothing broke
  3. Re-run /polaris.comply to apply remaining controls
  4. Run /polaris.comply --ci in your pipeline to gate deployments
============================================================
```

Compliance score formula: `(controls_present / total_applicable_controls) * 100`
where `controls_present = applied + already_present`.

## Operating Principles

- **Detect, don't assume**: always identify the stack before filtering skills
- **Never apply offensive skills**: skip any skill containing `exploit`, `bypass`, `red-team`, `c2`, `malware`, `phishing`, `pentest`, `attack`
- **Idempotent by default**: check markers before applying; never re-apply an existing control
- **Scaffold only, no provisioning**: skills that need external services (Vault, Okta, AWS) create config files and `.env.example` entries - never attempt to provision external resources
- **Cross-platform**: use Read/Glob/Grep tools for all file operations; no bash-only commands
- **Guardrail first**: always write the guardrail to `.polaris/skills/` before applying a skill
- **One skill at a time**: apply skills sequentially, not in parallel, to avoid conflicts

## Context

$ARGUMENTS

**Telemetry**: Run: `polaris telemetry record compliance --feature <slug> --phase complete --agent claude`
