# Tasks - 010 Go-live launch readiness (Maintained House)

Public landing on `/`, legal pages, go-live runbook, branding pass.
Spec: [spec.md](spec.md), plan: [plan.md](plan.md). Issue #102.

## Work Packages

### WP01 - Public pages + branding (frontend-craft)

- [x] T001 Middleware: remove `"/"` from the matcher (other protected routes untouched)
- [x] T002 `components/landing-page.tsx` (server component, static): hero "Maintained House" + one-liner, 4 feature highlight cards, CTA `#landing-signin-cta` -> `/signin`, footer links (User guide new-tab, `/privacy`, `/terms`)
- [x] T003 `app/page.tsx` split render: `auth()` decides - no session -> LandingPage, session -> existing dashboard branch unchanged (keep its fetches; drop requireSession)
- [x] T004 Legal pages `app/privacy/page.tsx` + `app/terms/page.tsx` (public, static prose per plan content outline) + shared typography wrapper
- [x] T005 Sign-in page footer: Privacy / Terms links
- [x] T006 Branding sweep: layout Metadata title + header brand -> "Maintained House"; user-manual title/header; verify no test asserts the old brand string
- [x] T007 Jest: landing (brand, CTA href, legal links), privacy/terms smoke, dashboard-branch regression (existing tests still pass)

Dependencies: none.

### WP02 - E2E launch suite (testing-specialist, deps: WP01)

- [ ] T008 WP10-1: anonymous `/` renders landing (no redirect; brand, CTA, legal links visible)
- [ ] T009 WP10-2: CTA navigates to `/signin`
- [ ] T010 WP10-3: `/privacy` and `/terms` render anonymously with key headings
- [ ] T011 WP10-4: signed-in `/` still renders the dashboard (signInAs)
- [ ] T012 Full-suite regression (39 existing + new) local + CI

Dependencies: WP01.

### WP03 - Go-live runbook + docs (documentation, no deps, parallel-safe)

- [x] T013 `docs/go-live-runbook.md`: 8 phases per plan (domain, mailbox, Atlas, App Service prod + api.maintained.house, Vercel prod, OAuth consent publishing, verification checklist, rollback notes) - every step with where/what/verify
- [x] T014 README: link the runbook from the Deployment section
- [x] T015 Docs consistency pass: oidc-setup.md cross-references, brand naming in docs

`test_status: skipped` (docs-only). Dependencies: none - can run parallel to WP01/WP02.

## Sequencing

WP01 -> WP02; WP03 independent (parallel-safe, docs only).
MVP = WP01 (public pages live); WP02 locks behavior; WP03 unblocks the operator.
