---
description: Create or update the project constitution through interactive phase-based discovery.
---


## Model Guidance

Model: plan tier (see references/model-selection.md); routing is enforced by the launcher.

---

## User Input

**Telemetry**: Run: `polaris telemetry record constitution --feature <slug> --phase start --agent claude`


```text
$ARGUMENTS
```

You **MUST** consider the user input before proceeding (if not empty).

**If `$ARGUMENTS` contains `--regenerate`:** run the single Bash command below, print its output, and stop. Do not read further. Do not ask any questions.

```bash
python -c "from specify_cli.core.constitution_regenerate import regenerate; from pathlib import Path; print(regenerate(Path('.')))"
```

If the command exits with an error, print the error message and stop.

---

## What This Command Does

Creates or updates `.polaris/memory/constitution.md` through interactive 11-phase discovery.

**Constitution is OPTIONAL.** All polaris commands work without it. It captures: technical standards, security baseline, code quality expectations, tribal knowledge, and governance rules.

## Discovery Phases

| Phase | Content | Questions | Required? |
|-------|---------|-----------|-----------|
| 1. Technical Standards | Languages, testing, performance, deployment | 4-5 | Recommended |
| 2. Security & Audit Baseline | Data classification, audit logging, AuthN/AuthZ, threat surface | 4 | **Required** |
| 2B. Non-Functional Baseline | DB pool, sessions, HTTP clients, threads, graceful shutdown, observability | 4 | **Required** |
| 2C. OWASP Security Standards | ASVS compliance level, OWASP Top 10 (2021) risk acknowledgement | 2 | **Required** |
| 2D. OWASP API Security Top 10 | API-specific risks (2023 edition) | 1 | **Conditional** (if Q9 shows API surface) |
| 2E. SOC 2 Trust Service Criteria | Security/Availability/Confidentiality/PI/Privacy criteria + audit target | 2 | Optional |
| 2F. CIS Benchmarks | OS/container/K8s/cloud/DB hardening level | 2 | Optional |
| 2G. SLSA Supply Chain Level | Build provenance and supply-chain integrity level | 1 | Optional |
| 2H. GDPR / ITAR Data Residency | Regulatory scope, residency region, retention, DPA contact | 4 | **Required** |
| 3. Code Quality | PR rules, review checklist, quality gates, docs | 3-4 | Optional |
| 4. Tribal Knowledge | Conventions, lessons learned, historical decisions | 2-4 | Optional |
| 5. Governance | Amendment process, compliance, exceptions | 2-3 | Optional |

**Paths**: Minimal (~2 pages, Phases 1 + 2 + 2B + 2C + 2D (if API) + 2H, 17-20 questions) or Comprehensive (~4 pages, all phases, 28-35 questions). Phases 2, 2B, 2C, and 2H are **required** in both paths. Phase 2D is conditional on Q9 showing any API surface. Phases 2E, 2F, and 2G are optional Comprehensive-only. If any answer is genuinely "none" or "N/A", record that explicitly rather than skipping - future reviewers need to see the conscious decision.

## Steps

### 1. Initial Choice

Ask: A) Skip (create placeholder), B) Minimal (Phase 1 only), C) Comprehensive (all phases)

If skipped: write placeholder to `.polaris/memory/constitution.md` and exit.

### 2. Phase 1 - Technical Standards

Ask one at a time with examples. When recommending a runtime/framework version, default to the floor in `src/specify_cli/scaffolds/version_pins.yaml` (Node 22+ LTS, Python 3.12+, Java 21+ LTS, PostgreSQL 17+, Next.js 16+, React 19+, Django 5.1+, .NET 8+ LTS, Rust latest stable).

- **Q1 Languages/Frameworks**: e.g., "Python 3.12+ with FastAPI 0.115+", "TypeScript 5.x on Node 22 LTS with Next.js 16 + React 19"
- **Q2 Testing**: e.g., "pytest with 80% coverage", "Jest with 90% coverage"
- **Q3 Performance/Scale**: e.g., "1000 req/s at p95 < 200ms", "N/A"
- **Q4 Deployment**: e.g., "Docker on K8s", "Cross-platform: Linux/macOS/Windows"
- **Q5 Azure DevOps**: Ask if they use ADO for work items. If yes, collect org URL and project name. Enables `/polaris.fix` ADO integration.

### 3. Phase 2 - Security & Audit Baseline (Required)

This phase is REQUIRED for both Minimal and Comprehensive paths. If an answer is genuinely "none" or "not applicable", record that explicitly so future reviewers can see the conscious decision rather than guess.

- **Q6 Data Classification**: What is the most sensitive data tier the application stores or processes? Use the standard four-tier model: Public, Internal, Confidential, Restricted (PII/PHI/payment/secrets). Pick the highest tier that applies; the constitution records this and downstream specs reference it.
- **Q7 Audit Logging**: Which events MUST be captured to an append-only audit log, and how long are they retained? At minimum: authentication outcomes (success/failure), authorization denials, all writes/deletes against Confidential or Restricted data, configuration changes, and admin actions. Default retention: 1 year. Audit logs land in `audit-trail/` (project root) or a managed sink (Splunk, Sentinel, S3 + Object Lock); never in the same database table as the data they describe.
- **Q8 AuthN / AuthZ Model**: How do callers authenticate (none, API key, OIDC/OAuth2, mTLS, SSO) and how is authorization decided (role-based, attribute-based, ownership-based)? Default-deny is required: if a request does not match a permit rule, it is denied. Record the principal source (JWT, header, session) and the policy enforcement point.
- **Q9 Threat Surface**: Where does untrusted input enter the system? List every boundary: public HTTP, webhook, file upload, message queue, scheduled job consuming external data, third-party API. For each, note PII/PHI/payment exposure and whether the app is multi-tenant. The answer becomes the seed of the threat model under `security/threat-model.md`.

### 3B. Phase 2B - Non-Functional Baseline (Required)

This phase is REQUIRED for both Minimal and Comprehensive paths. The questions establish the resource-lifecycle and runtime posture defaults that every feature spec inherits via the spec template's "Non-Functional Requirements" section. If an answer is genuinely "none" (stateless CLI, no DB), record that explicitly so future reviewers can see the decision.

The motivation is concrete: greenfield apps consistently ship with leaked DB connections, leaked threads, and unbounded queues because the lifecycle was never specified. Recording it in the constitution means every spec has a baseline to inherit and every reviewer has a contract to compare against.

- **Q10 Database & Connection Pooling**: Does the app use a database? If yes, name the engine (PostgreSQL 17, MySQL 8.4 LTS, etc.) and the pool implementation:
  - Python/SQLAlchemy: `create_async_engine` with explicit `pool_size` and `max_overflow` (NOT defaults of 5/10 - size for expected concurrency).
  - Node/Express: `pg.Pool` singleton instantiated once at module scope; never `new Client()` per request.
  - Java/Spring: HikariCP with `maximumPoolSize`, `minimumIdle`, `connectionTimeout` explicitly set; never accept Hikari's optimistic defaults.
  - Django: `CONN_MAX_AGE` set (commonly 60s), `DISABLE_SERVER_SIDE_CURSORS=True` if using PgBouncer.
  - .NET: pooling is on by default in `Npgsql`; explicitly set `Maximum Pool Size` and `Connection Idle Lifetime`.
  Record the chosen pool sizes and the rule: "connection acquired per request via DI / `with` block, released by framework lifespan / `try/finally`, NEVER held across awaits without explicit transaction scope."

- **Q11 Background Tasks & Threads**: Does the app spawn long-running threads, async tasks, or message-queue consumers? If yes, the constitution mandates:
  - All background work uses a **bounded** executor (thread pool, asyncio task group, queue worker pool) with an explicit max concurrency.
  - Lifecycle is owned by the application's startup/shutdown hooks (FastAPI lifespan, Spring Boot `@PreDestroy`, Express `process.on('SIGTERM')`), NOT spawned ad-hoc per request.
  - Daemon threads are forbidden for any task that must complete (audit-log flushers, queue consumers); they're acceptable only for truly fire-and-forget work that survives a hard kill.
  - Every thread / task has a documented shutdown drain time and an alarm threshold for queue depth.

- **Q12 HTTP Clients & External Resources**: Are outbound HTTP calls made? If yes, the constitution mandates singleton clients reused across the process: `httpx.AsyncClient` (Python), `undici.Pool` or `axios.create()` with `keepAlive: true` (Node), `OkHttpClient` as a singleton bean (Java/Spring), `IHttpClientFactory` (.NET). NEVER `requests.get()` or `new HttpClient()` per request: each call leaks a TCP socket and exhausts ephemeral ports under load.

- **Q13 Graceful Shutdown & Liveness/Readiness**: How does the app shut down cleanly? The constitution mandates:
  - SIGTERM handler installed (or framework default - Uvicorn lifespan, Spring Boot graceful shutdown, Express `server.close()`).
  - Drain timeout explicitly set (commonly 30-60s).
  - Shutdown order: stop accepting new requests -> drain in-flight -> close DB pool -> flush audit/log sinks -> exit.
  - Readiness probe flips to NotReady at shutdown start (gives the load balancer time to remove the pod from rotation).
  - Liveness probe stays healthy until the process is truly dead (don't conflate liveness with readiness).
  Document the drain timeout and the shutdown order in the constitution; spec authors inherit this and only document deltas.

### 3C. Phase 2C - OWASP Security Standards (Required)

This phase is REQUIRED for both Minimal and Comprehensive paths. It documents the project's
OWASP compliance target and records an explicit acknowledgement of the OWASP Top 10 (2021)
risk categories. If a category is genuinely not applicable (e.g., SSRF is irrelevant for an
offline CLI tool), record "N/A - [reason]" explicitly so future reviewers see a conscious
decision rather than an omission.

- **Q14 ASVS Target Level**: What OWASP Application Security Verification Standard (ASVS)
  compliance level does this application target?
  - L1 - Opportunistic: minimum baseline; all apps; opportunistically testable without source code
  - L2 - Standard (**default**): apps handling sensitive data; requires source code review and
    functional testing; recommended for most business applications
  - L3 - Advanced: high-assurance apps (banking core, medical devices, critical infrastructure);
    requires full threat modeling, in-depth code review, and penetration testing
  Default to L2 when the user accepts the default or is uncertain.

- **Q15 OWASP Top 10 (2021) Risk Acknowledgement**: Present all 10 categories below. For each,
  the team either accepts it as a risk to actively mitigate, or marks it "N/A - [reason]" if
  genuinely not applicable to their threat model. All 10 must be addressed; none may be silently
  omitted.
  - A01:2021 - Broken Access Control
  - A02:2021 - Cryptographic Failures
  - A03:2021 - Injection
  - A04:2021 - Insecure Design
  - A05:2021 - Security Misconfiguration
  - A06:2021 - Vulnerable and Outdated Components
  - A07:2021 - Identification and Authentication Failures
  - A08:2021 - Software and Data Integrity Failures
  - A09:2021 - Security Logging and Monitoring Failures
  - A10:2021 - Server-Side Request Forgery (SSRF)

### 3D. Phase 2D - OWASP API Security Top 10 (Conditional)

This phase is CONDITIONAL: include it when Q9 (Threat Surface) mentioned any HTTP API,
REST endpoint, GraphQL, or webhook boundary. Skip it when Q9 showed no API surface, but
still write the section with: "N/A - no API surface (explicit decision, [date])."

- **Q16 OWASP API Security Top 10 (2023) Risk Acknowledgement**: Present all 10 categories.
  For each, the team accepts as a risk to mitigate, or marks "N/A - [reason]" if not
  applicable. All 10 must be addressed; none may be silently omitted.
  - API1:2023 - Broken Object Level Authorization
  - API2:2023 - Broken Authentication
  - API3:2023 - Broken Object Property Level Authorization
  - API4:2023 - Unrestricted Resource Consumption
  - API5:2023 - Broken Function Level Authorization
  - API6:2023 - Unrestricted Access to Sensitive Business Flows
  - API7:2023 - Server Side Request Forgery
  - API8:2023 - Security Misconfiguration
  - API9:2023 - Improper Inventory Management
  - API10:2023 - Unsafe Consumption of APIs

### 3E. Phase 2E - SOC 2 Trust Service Criteria (Comprehensive only, Optional)

Ask to skip or continue. If skipped, omit the SOC 2 section from the constitution.

If continuing:
- **Q17 Trust Service Criteria**: Which criteria apply? (select all that apply)
  - Security (CC) - always the baseline when SOC 2 is selected
  - Availability (A)
  - Confidentiality (C)
  - Processing Integrity (PI)
  - Privacy (P)
- **Q18 Audit Target**: Type I (point-in-time design review) / Type II (6-12 month
  operational effectiveness review) / Not yet scheduled

### 3F. Phase 2F - CIS Benchmarks (Comprehensive only, Optional)

Ask to skip or continue. If skipped, omit the CIS section from the constitution.

If continuing:
- **Q19 CIS Benchmarks**: Which benchmarks apply? For each applicable one, select
  Level 1 (basic hygiene, low operational impact) or Level 2 (defence-in-depth,
  higher operational impact). Select all that apply:
  - CIS Linux Benchmark (OS hardening)
  - CIS Docker / containerd Benchmark
  - CIS Kubernetes Benchmark
  - CIS AWS / Azure / GCP Foundations Benchmark
  - CIS Database Benchmark (PostgreSQL, MySQL, MSSQL)
  - N/A - no applicable benchmark
  Use the current version of each benchmark at the time of setup.

### 3G. Phase 2G - SLSA Supply Chain Level (Comprehensive only, Optional)

Ask to skip or continue. If skipped, omit the SLSA section from the constitution.

If continuing:
- **Q20 SLSA Target Level**: What supply-chain integrity level does this project target?
  - L0 - No guarantees (no CI/CD, no provenance)
  - L1 - Build provenance generated (scripted build, basic audit trail)
  - L2 - Hosted build + signed provenance (**default** for apps with CI/CD pipelines;
    achievable via GitHub Actions OIDC + Sigstore)
  - L3 - Hardened build platform + non-falsifiable provenance (high-assurance apps)
  Default to L2 when the project has CI/CD pipelines and the user is uncertain.

### 3H. Phase 2H - GDPR / ITAR Data Residency (Required)

This phase is REQUIRED for both Minimal and Comprehensive paths. It captures the full
regulatory and data residency posture at project inception - richer than the lightweight
ITAR/GDPR question asked by `polaris ship` at deploy time. If none of the regulations
apply, record that explicitly; "Neither" is a valid and supported answer.

- **Q21 Regulatory Scope**: Does the application store or process EU personal data
  (GDPR), ITAR-controlled technical data, both, or neither? Pick the highest applicable
  scope. If unsure, default to GDPR for any app with EU users.
- **Q22 Data Residency Region**: Where must data physically reside? Common answers:
  EU-West, US-East, US-Gov, No restriction, Multiple regions. Record the constraint
  even if the answer is "no restriction" - that is itself a documented decision.
- **Q23 Retention and Deletion**: What is the maximum data retention period, and is
  there a documented process for deletion or anonymisation when retention expires or
  on user request? If unknown, default to "2 years; deletion process TBD".
- **Q24 DPA / Export Control Contact**: Name or role of the Data Protection Officer
  (GDPR) or Export Control Officer (ITAR). If neither applies, record "N/A".

### 4. Phase 3 - Code Quality (comprehensive only)

Ask to skip or continue. If yes:
- **PR Requirements**: approval count, CI checks
- **Review Checklist**: what reviewers should check
- **Quality Gates**: what must pass before merge
- **Documentation Standards**: docstrings, README, ADRs

### 5. Phase 4 - Tribal Knowledge (comprehensive only)

Ask to skip or continue. If yes:
- **Team Conventions**: coding styles, patterns to follow
- **Lessons Learned**: past mistakes to avoid
- **Historical Decisions** (optional): architectural choices and rationale

### 6. Phase 5 - Governance (comprehensive only)

Ask to skip or continue. If skipped, use defaults: PR-based amendments, reviewer compliance, case-by-case exceptions.

If yes: amendment process, compliance validation, exception handling (optional).

### 7. Summary and Confirmation

Present summary of all phases/answers. Ask: A) Write it, B) Start over, C) Cancel.

### 8. Write Constitution File


Generate markdown to `.polaris/memory/constitution.md` with sections for each completed phase. Include:
- Header with project name, date, version
- Technical Standards (Q1-Q4)
- Azure DevOps section (if Q5 answered yes)
- **Security & Audit Baseline (Q6-Q9, always included)** - data classification tier, audit log policy and retention, AuthN/AuthZ model, threat surface
- **Non-Functional Baseline (Q10-Q13, always included)** - DB pool implementation and sizing, background task lifecycle policy, HTTP client singleton policy, graceful shutdown order and drain timeout. Section title: `## Non-Functional Baseline`. Spec authors inherit these defaults via the spec template's "Non-Functional Requirements" section.
- **OWASP Security Standards (Q14-Q15, always included)** - ASVS target level and OWASP Top 10 (2021) risk acknowledgement checklist. Section title: `## OWASP Security Standards`. Generate this section using the following format:

  ```markdown
  ## OWASP Security Standards

  **ASVS Target Level**: [L1/L2/L3] - [Level Name]
  [One-line description of the level, e.g.: "Apps handling sensitive data. Requires source code review and functional testing. Recommended for most business applications."]

  **OWASP Top 10 (2021) - Risk Acknowledgement Checklist**

  Edition: OWASP Top 10 2021. Re-review when OWASP publishes the next major edition.

  - [x] A01:2021 - Broken Access Control
  - [x] A02:2021 - Cryptographic Failures
  - [x] A03:2021 - Injection
  - [x] A04:2021 - Insecure Design
  - [x] A05:2021 - Security Misconfiguration
  - [x] A06:2021 - Vulnerable and Outdated Components
  - [x] A07:2021 - Identification and Authentication Failures
  - [x] A08:2021 - Software and Data Integrity Failures
  - [x] A09:2021 - Security Logging and Monitoring Failures
  - [x] A10:2021 - Server-Side Request Forgery (SSRF)
  ```

  Replace `[x]` with `[ ] N/A - [reason]` for any category the team marked not applicable.
  The OWASP section MUST appear after `## Non-Functional Baseline` and before `## OWASP API
  Security Standards` (or before `## Code Quality` if Phase 2D was skipped). This section is
  user-authored after discovery; the `--regenerate` flag does NOT modify it.

- **OWASP API Security Standards (Q16, conditional on API surface)** - API risk acknowledgement
  checklist. Section title: `## OWASP API Security Standards`. Write this section when Phase 2D
  was triggered (Q9 showed API surface). When Q9 showed no API surface, write:
  `## OWASP API Security Standards` / `N/A - no API surface (explicit decision, [date]).`
  When Phase 2D was triggered, generate using this format:

  ```markdown
  ## OWASP API Security Standards

  Edition: OWASP API Security Top 10 2023. Re-review when OWASP publishes the next major edition.

  - [x] API1:2023 - Broken Object Level Authorization
  - [x] API2:2023 - Broken Authentication
  - [x] API3:2023 - Broken Object Property Level Authorization
  - [x] API4:2023 - Unrestricted Resource Consumption
  - [x] API5:2023 - Broken Function Level Authorization
  - [x] API6:2023 - Unrestricted Access to Sensitive Business Flows
  - [x] API7:2023 - Server Side Request Forgery
  - [x] API8:2023 - Security Misconfiguration
  - [x] API9:2023 - Improper Inventory Management
  - [x] API10:2023 - Unsafe Consumption of APIs
  ```

  Replace `[x]` with `[ ] N/A - [reason]` for categories not applicable.
  This section is user-authored; the `--regenerate` flag does NOT modify it.

- **SOC 2 Compliance (Q17-Q18, if Phase 2E answered)** - Trust Service Criteria and audit
  target. Section title: `## SOC 2 Compliance`. Omit entirely if Phase 2E was skipped.
  Generate using this compact format:

  ```markdown
  ## SOC 2 Compliance

  **Trust Service Criteria**: Security (CC), [Availability (A)], [Confidentiality (C)], [PI], [Privacy (P)]
  **Audit Target**: [Type I / Type II / Not yet scheduled], [target date if known]
  ```

  List only the selected criteria. This section is user-authored; `--regenerate` does NOT modify it.

- **CIS Benchmarks (Q19, if Phase 2F answered)** - Hardening baseline. Section title:
  `## CIS Benchmarks`. Omit entirely if Phase 2F was skipped. Generate using this format:

  ```markdown
  ## CIS Benchmarks

  - [Benchmark name] L[1/2] (current version at time of setup)
  ```

  One line per applicable benchmark. This section is user-authored; `--regenerate` does NOT modify it.

- **SLSA Supply Chain Security (Q20, if Phase 2G answered)** - Supply chain integrity level.
  Section title: `## SLSA Supply Chain Security`. Omit entirely if Phase 2G was skipped.
  Generate using this compact format:

  ```markdown
  ## SLSA Supply Chain Security

  **SLSA Target Level**: L[0/1/2/3] - [Level name]
  [One-line rationale, e.g.: "Hosted build + signed provenance via GitHub Actions OIDC."]
  ```

  This section is user-authored; the `--regenerate` flag does NOT modify it.

- **Data Residency and Regulatory Compliance (Q21-Q24, always included)** - GDPR/ITAR posture.
  Section title: `## Data Residency and Regulatory Compliance`. Always written; if regulatory
  scope is "Neither", record that explicitly. Generate using this format:

  ```markdown
  ## Data Residency and Regulatory Compliance

  **Regulatory Scope**: [GDPR / ITAR / Both / Neither - explicit decision]
  **Data Residency Region**: [EU-West / US-East / US-Gov / No restriction / Other]
  **Retention Period**: [e.g., 2 years; deletion/anonymisation on account deletion]
  **DPA / Export Control Contact**: [name/role or N/A]
  ```

  This section is user-authored; the `--regenerate` flag does NOT modify it.
  NOTE: `polaris ship` also asks an ITAR/GDPR question at deploy time and writes a
  `## Deployment Compliance & Topology` section. These two sections are complementary:
  this section captures design intent; the ship section captures deployment topology.

- Code Quality (if Phase 3)
- Tribal Knowledge (if Phase 4)
- Governance (Phase 5 or defaults)
- **Model Selection section (always included)**
- License Compliance section (always included):
  - Allowed: Apache-2.0, BSD-2/3-Clause, MIT, ISC, PSF-2.0, Unlicense, 0BSD, CC0-1.0
  - Prohibited: LGPL, AGPL, GPL, SSPL, BSL, CPAL, EUPL, MPL-2.0
- Deployment Compliance & Topology section: do NOT generate this manually.
  This section is auto-managed by `polaris ship`. The first time a developer
  runs `polaris ship` (or `/polaris.ship`), they are asked whether the app
  needs ITAR or GDPR compliance, and the answer is written into both
  `.polaris/metadata.yaml` (under a `deployment:` block) and this constitution
  file (as a "## Deployment Compliance & Topology" section). Subsequent ships
  read the stored value and do not re-prompt. To change it later, run
  `/polaris.constitution --amend` and update the deployment section in place,
  then sync `.polaris/metadata.yaml` to match (or use the helper
  `specify_cli.core.deployment_decisions.write_decisions(...)` which updates
  both files together).

The Model Selection section must be included verbatim. Load `.claude/commands/references/model-selection.md` and copy its full content into the constitution as the `## Model Selection` section.

### 9. Post-Write Security Validation

After writing the constitution file, run a security guardrail check. Non-blocking: reports
issues, auto-fixes what it can, and always proceeds to the next step.

```python
import re
from pathlib import Path

path = Path(".polaris/memory/constitution.md")
if not path.exists():
    print("Security validation: constitution.md not found - skipping check.")
else:
    text = path.read_text(encoding="utf-8")
    issues = []
    fixed = []

    # 1. Detect and redact hardcoded secrets
    secret_patterns = [
        (r'(?i)(password|passwd|api[_-]?key|token|credential)(\s*[=:]\s*)\S{6,}',
         "Potential hardcoded secret", r'\1\2<REDACTED>'),
        (r'-----BEGIN (RSA |EC |OPENSSH )?PRIVATE KEY-----',
         "Private key material", "# <PRIVATE KEY REMOVED>"),
    ]
    new_text = text
    for pattern, label, replacement in secret_patterns:
        if re.search(pattern, new_text):
            new_text = re.sub(pattern, replacement, new_text)
            fixed.append(f"Auto-redacted: {label}")

    if new_text != text:
        path.write_text(new_text, encoding="utf-8")
        text = new_text

    # 2. Security section completeness - report missing, do not block
    for heading in [
        "## Security & Audit Baseline",
        "## OWASP Security Standards",
        "## Data Residency and Regulatory Compliance",
    ]:
        if heading not in text:
            issues.append(f"Missing section: '{heading}' - add it using the answers from this session")

    if "ASVS Target Level" not in text:
        issues.append("Missing: ASVS Target Level not recorded - add it to the OWASP section")

    if "Data Classification" not in text and "data classification" not in text.lower():
        issues.append("Missing: Data Classification tier not recorded")

    # 3. Unresolved TODOs in security sections
    sec_start = text.find("## Security")
    if sec_start != -1 and re.search(r'(?i)\bTODO\b|\bTBD\b|\bFIXME\b', text[sec_start:sec_start+3000]):
        issues.append("Unresolved TODO/TBD in security section - resolve when possible")

    # Report
    if fixed:
        print("Constitution security guardrail - auto-fixed:")
        for f in fixed:
            print(f"  {f}")
    if issues:
        print("Constitution security guardrail - issues to address:")
        for i in issues:
            print(f"  {i}")
        print("Self-heal: update constitution.md with the missing information above.")
    if not fixed and not issues:
        print("Constitution security guardrail PASSED.")
```

**Self-heal:** If secrets were auto-redacted, replace the placeholder with a reference to a secrets manager. Add any missing sections using the answers collected in this session.

### 10. Generate / Update Security Reference (`docs/reference/security.md`)

After the security validation passes, generate or refresh `docs/reference/security.md` to reflect the security controls now in place for this project.

**How to generate:**

Read the following source files and synthesize a `docs/reference/security.md` that describes what is actually implemented:

- `src/specify_cli/.polaris/templates/git-hooks/pre-commit-security-check` - secret/credential patterns checked
- `src/specify_cli/.polaris/templates/git-hooks/pre-commit-encoding-check` - encoding sanitization
- `src/specify_cli/.polaris/templates/git-hooks/pre-commit-agent-check` - agent runtime file protection
- `src/specify_cli/.polaris/templates/git-hooks/pre-commit-branch-guard` - branch protection rules
- `src/specify_cli/.polaris/templates/git-hooks/commit-msg` - commit attribution
- `src/specify_cli/core/branch_protection.py` - protected branch config
- `src/specify_cli/missions/software-dev/command-.polaris/templates/references/review-passes.md` - security review pass criteria
- `src/specify_cli/missions/software-dev/command-.polaris/templates/merge.md` - pre-merge gate
- `.polaris/memory/constitution.md` (just written) - the project's actual security baseline answers

**Document structure** (use these sections in order):

1. Pre-commit Hooks - one subsection per hook with what it checks/blocks
2. Code Review Security Gate - Pass 1 criteria and SoD enforcement
3. Pre-merge Security Gate - what the diff scan detects
4. Constitution Security Baseline - the phases completed in THIS run (pull actual answers from the written constitution.md)
5. Compliance Hardening - domains available via `/polaris.comply`
6. Audit Trail - what is logged and where
7. Text Sanitization - the sanitizer policy
8. License Compliance - allowed/prohibited licenses
9. Security Control Lifecycle - table mapping triggers to controls
10. Updating This Document - pointer table to source files

**Rules:**
- Write only what is actually implemented - no aspirational content
- Pull the project's specific answers (ASVS level, data classification, GDPR/ITAR scope) from the constitution just written
- If `docs/reference/security.md` already exists, update it in place; preserve the structure but refresh values from the current constitution
- If `docs/` does not exist in the project, create `docs/reference/security.md` anyway

### 11. Success Message

Report: constitution file location, phases completed, security validation result, security.md location, next steps (review, share, run /polaris.specify).

## Behaviors

- Ask one question at a time with skip options
- Keep constitution lean (1-3 pages)
- If skipped entirely, still create placeholder file


**Telemetry**: Run: `polaris telemetry record constitution --feature <slug> --phase complete --agent claude`
