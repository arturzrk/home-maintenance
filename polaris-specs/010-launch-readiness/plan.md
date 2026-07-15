# Implementation Plan: 010 - Go-live launch readiness (Maintained House)

**Branch**: per-WP branches (`010-launch-readiness-WP##`) | **Date**: 2026-07-15 | **Spec**: [spec.md](spec.md)
**Tracker**: GitHub issue #102

## Summary

Frontend pages plus operator documentation; no backend changes. Make `/`
public with a split render (landing for anonymous, dashboard for a
session), add `/privacy` and `/terms` as public static pages, link them
from the landing and sign-in pages, write the go-live runbook, and align
public-facing branding on "Maintained House".

## Technical Context

**Current state (verified in code)**

- `frontend/src/middleware.ts` matcher includes `"/"` (added in 009 WP02)
  - anonymous `/` currently 307s to `/signin?callbackUrl=%2F`.
- `frontend/src/app/page.tsx` calls `requireSession()` then renders the
  dashboard ("Welcome back", `#dashboard-properties-link`,
  ConnectionStatus).
- Root layout (009 WP01) already handles the no-session header correctly
  (brand + plain User guide link, no menu) - the landing page inherits it
  with zero changes (FR-11 of 009 carries over).
- `frontend/src/app/(auth)/signin/page.tsx` - two form actions, default
  `redirectTo "/"`; no footer today.
- e2e: `signInAs` waits for pathname `/` after stub sign-in - unaffected
  (signed-in `/` still renders the dashboard). wp09 suite asserts
  signed-in dashboard behavior - unaffected.
- App metadata (`layout.tsx` Metadata) currently `title: "Home
  Maintenance"`.
- Docs: `docs/oidc-setup.md` already targets maintained.house URIs;
  README deployment section covers the staging App Service;
  `docs/observability.md` exists. Constitution names maintained.house as
  the reserved production domain - consistent, no amendment needed.

## Architecture

### Split render on `/` (FR-01)

- Remove `"/"` from the middleware matcher (back to protected-only
  routes; `/privacy` and `/terms` never enter it).
- `app/page.tsx`: `const session = await auth();` - if no session,
  render `<LandingPage />`; else the existing dashboard JSX (move the
  `requireSession()` call out; `auth()` result decides the branch, and
  the dashboard branch keeps its data fetches). Session-expiry edge
  (RefreshAccessTokenError) renders the landing - acceptable: one click
  re-enters sign-in.
- The signed-in dashboard branch is byte-identical output to today.

### Landing page (FR-02)

- `frontend/src/components/landing-page.tsx` (server component, no
  client JS): hero ("Maintained House" + one-liner), 3-4 feature
  highlight cards (properties, recurring schedules, checklists, assets)
  using existing card styling, primary CTA `<Link href="/signin"
  id="landing-signin-cta">`, footer links: User guide (new tab),
  `/privacy`, `/terms`. Static content only - no fetches, no images
  (keeps it dependency-free and fast).

### Legal pages (FR-03/04/05)

- `frontend/src/app/privacy/page.tsx` and `frontend/src/app/terms/page.tsx`
  - server components rendering prose (shared minimal typography
  wrapper; content authored as JSX, not markdown, to avoid adding a
  renderer dependency).
- Privacy content: data collected (Google name/email/sub + entered
  maintenance data), purpose (providing the service), storage (Azure
  App Service EU region + MongoDB), cookies (session only, no
  trackers), retention ("until you ask for removal"), removal via
  contact@maintained.house, audit-log note (constitution baseline).
- Terms content: service description, personal/household use,
  acceptable use, as-is/no-warranty, liability limitation, governing
  law Poland, changes-to-terms clause, contact.
- Sign-in page gains a small footer with Privacy / Terms links
  (`(auth)/signin/page.tsx`).
- Both pages public: not in the middleware matcher; no session calls.

### Branding (FR-08)

- `layout.tsx` Metadata title -> "Maintained House"; header brand text
  -> "Maintained House". Dashboard copy already says "Welcome back"
  (no change). User manual title/header update ("Maintained House -
  User Guide") - single HTML file edit.
- Note: e2e/jest referencing "Home Maintenance" text must be swept
  (layout header brand is asserted nowhere today - verify during
  implementation).

### Runbook (FR-07)

`docs/go-live-runbook.md`, phased checklist with verify steps:

1. Domain: purchase maintained.house; registrar DNS or delegate.
2. Mailbox: forwarding for contact@maintained.house (registrar
   forwarding or an email provider); verify a test mail round-trip.
3. MongoDB Atlas: cluster (EU region), TLS, database user, IP access
   from App Service outbound IPs, connection string; backup schedule.
4. Azure App Service (production): create or repoint app; env-var
   matrix (reference the staging matrix in docs/oidc-setup.md, prod
   values: `Auth:UseStub=false`, Google ClientId, Atlas URI); custom
   domain api.maintained.house + managed TLS; health probe.
5. Vercel: production project/domain maintained.house; env vars
   (API_BASE_URL=https://api.maintained.house, NEXT_PUBLIC_API_URL,
   NEXTAUTH_URL=https://maintained.house, NEXTAUTH_SECRET, Google
   client id/secret, AUTH_TRUST_HOST, NEXTAUTH_DEV_STUB unset).
6. Google OAuth: consent screen -> app name "Maintained House",
   homepage https://maintained.house, privacy
   https://maintained.house/privacy, terms link, authorized domain
   maintained.house; credentials -> production redirect URI
   https://maintained.house/api/auth/callback/google; publish app
   (basic scopes - no restricted-scope audit expected).
7. Verification: anonymous landing loads on the domain; legal URLs
   public; third-party Google account signs in; API health via
   frontend widget; deep-link round trip; CORS/cookies sanity.
8. Rollback notes: consent screen back to testing; DNS TTLs.

### Tests

- **Jest**: landing-page renders brand, CTA href, legal links; privacy
  and terms pages render key sections (lightweight smoke - static
  content).
- **e2e** (`wp10-launch.spec.ts`): anonymous `/` shows landing (no
  redirect); CTA navigates to /signin; /privacy and /terms render
  anonymously; signed-in `/` still shows dashboard (uses signInAs).
  Existing 39 tests must pass unchanged.

## Constitution Check

- No backend change; clean architecture untouched. Production domain
  matches the constitution's reserved maintained.house. Brand rename to
  "Maintained House" aligns with the constitution's rebrand note. Dev
  stub remains Development-only; runbook explicitly sets
  `Auth:UseStub=false` in prod. Tests accompany all behavior changes.

## Work Package Sketch (input to /polaris.tasks)

- **WP01 - Public pages** (frontend-craft): middleware, split render,
  landing component, privacy + terms pages, signin footer links,
  branding sweep (metadata/header/manual title), jest.
- **WP02 - E2E + regression** (testing-specialist, deps WP01):
  wp10-launch.spec.ts + full-suite regression run.
- **WP03 - Runbook + docs** (documentation, deps none, parallel-safe):
  docs/go-live-runbook.md, README pointer, docs consistency pass.
  `test_status: skipped` (docs-only).

## Risks

- Removing `"/"` from the middleware matcher must not touch the other
  protected routes - regression covered by existing wp09 tests
  (sign-out protection, deep link).
- Brand string sweep may break tests asserting "Home Maintenance"
  (user-manual e2e? none assert the title today - verify).
- Legal pages are plain-language, not legal advice - flagged in spec
  assumptions; acceptable for launch, revisit if the user base grows.

## Research / Data model / Contracts

No unknowns requiring research.md; no data-model or API changes.
