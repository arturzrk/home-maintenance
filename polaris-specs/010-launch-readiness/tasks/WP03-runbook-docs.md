---
work_package_id: WP03
title: Go-live runbook + docs
lane: "for_review"
dependencies: []
base_branch: main
base_commit: ee2eb59a1c86e6f84b5395b305bc1252413dc5d1
created_at: '2026-07-19T10:37:41.963795+00:00'
subtasks: [T013, T014, T015]
test_status: skipped
test_file: tests/e2e/WP03-go-live-runbook-docs.e2e.js
domain: documentation
shell_pid: "34039"
---

# WP03 - Go-live runbook + docs

## Objective

`docs/go-live-runbook.md`: a checklist the operator can follow from
"domain not purchased" to "any Google account signs in at
https://maintained.house" without reading source code (SC-04/05).
Parallel-safe: touches only docs; independent of WP01/WP02.

## Context

- Existing references: README Deployment section (staging App Service
  workflow, publish-profile secret, env-var pointer),
  `docs/oidc-setup.md` (staging env matrix, maintained.house redirect
  URIs already documented), `docs/observability.md`.
- Deploy workflow: `.github/workflows/deploy-backend.yml` (auto on
  backend/** push to main; manual dispatch available).
- Production values decided in spec/plan: brand "Maintained House",
  domain maintained.house, API api.maintained.house, contact
  contact@maintained.house, Atlas for Mongo, `Auth:UseStub=false`,
  `NEXTAUTH_DEV_STUB` unset.

## Subtasks

### T013 - Runbook

`docs/go-live-runbook.md`, 8 phases, each step = what / where / verify:

1. **Domain**: purchase maintained.house (note ~USD 30/yr renewal);
   DNS zone at registrar or delegated.
2. **Contact mailbox**: forwarding for contact@maintained.house;
   verify round trip.
3. **MongoDB Atlas**: EU cluster, TLS, db user, network access for App
   Service outbound IPs, connection string, backup schedule; verify
   with mongosh ping.
4. **Azure App Service (prod)**: app settings matrix (adapt the
   staging matrix in oidc-setup.md; Auth:UseStub=false, Google
   ClientId, Atlas URI); custom domain api.maintained.house + managed
   certificate; verify /health over HTTPS. Note: repoint or clone the
   staging deploy workflow (AZURE_BACKEND_APP_NAME variable) - decide
   staging vs prod app split and document both options.
5. **Vercel (prod)**: domain maintained.house; env vars
   (API_BASE_URL=https://api.maintained.house, NEXT_PUBLIC_API_URL,
   NEXTAUTH_URL=https://maintained.house, NEXTAUTH_SECRET fresh,
   GOOGLE_CLIENT_ID/SECRET, AUTH_TRUST_HOST=true, NEXTAUTH_DEV_STUB
   unset); verify landing loads on the domain.
6. **Google OAuth publishing**: consent screen (app name "Maintained
   House", homepage https://maintained.house, privacy
   https://maintained.house/privacy, terms
   https://maintained.house/terms, authorized domain
   maintained.house); credentials redirect URI
   https://maintained.house/api/auth/callback/google; publish to
   production (basic scopes - openid/email/profile - no restricted
   audit); verify status "In production".
7. **Verification checklist**: anonymous landing 200; /privacy +
   /terms public; third-party Google account full sign-in; dashboard
   health widget Connected; deep-link round trip
   (/properties/{id} -> signin -> back); sign-out; user guide link.
8. **Rollback**: consent screen back to Testing; Vercel domain
   detach; DNS TTL note; App Service slot/stop.

### T014 - README pointer

README Deployment section: link the runbook as the production path
(staging text stays).

### T015 - Docs consistency

Cross-reference pass: oidc-setup.md mentions the runbook for prod;
brand naming ("Maintained House") consistent across README intro,
runbook, oidc-setup; no lingering ".com" or placeholder domains.

## Definition of Done

- [ ] Runbook covers all 8 phases with verify steps
- [ ] No step requires reading source code
- [ ] README links the runbook
- [ ] Encoding hook passes (no em dashes)

## Risks

- Runbook rot: values live in one doc; the env matrices reference
  oidc-setup.md rather than duplicating it where possible.

## Run Command

```bash
polaris implement WP03
```

## Activity Log

- 2026-07-19T10:51:09Z – unknown – lane=for_review – Docs on branch 010-launch-readiness-WP03; PR #106
