---
work_package_id: WP01
title: 'Public pages + branding'
lane: "for_review"
dependencies: []
base_branch: main
base_commit: ee2eb59a1c86e6f84b5395b305bc1252413dc5d1
created_at: '2026-07-19T07:03:39.327028+00:00'
subtasks: [T001, T002, T003, T004, T005, T006, T007]
test_status: required
test_file: frontend/src/components/__tests__/landing-page.test.tsx
domain: frontend-craft
shell_pid: "53931"
---

# WP01 - Public pages + branding

## Objective

Anonymous visitors at `/` get a Maintained House landing page; `/privacy`
and `/terms` are public; sign-in page links them; public brand becomes
"Maintained House". Signed-in behavior unchanged.

## Context

- `frontend/src/middleware.ts` matcher currently includes `"/"` (added
  in 009 WP02). Other entries stay.
- `frontend/src/app/page.tsx` calls `requireSession()` then renders the
  dashboard. The dashboard JSX must survive unchanged (wp09 e2e asserts
  "Welcome back" + `#dashboard-properties-link`).
- Root layout already renders the menu-less header without a session.
- Header brand text and Metadata title say "Home Maintenance" today; no
  jest/e2e test asserts that string (verify with grep before and after).
- Content outlines for legal pages: plan.md "Legal pages" section.
  Contact: contact@maintained.house. Jurisdiction: Poland.

## Subtasks

### T001 - Middleware

Remove `"/"` from the matcher list only. Keep
`/properties|/jobs|/job-definitions|/assets` patterns.

### T002 [P] - LandingPage component

`frontend/src/components/landing-page.tsx`, server component, zero
client JS, no data fetches:

- Hero: h1 "Maintained House", one-liner ("Track the maintenance of
  your home - recurring schedules, checklists, and the assets they
  keep in shape."), CTA `<Link id="landing-signin-cta" href="/signin">`
  ("Get started" / "Sign in").
- 4 highlight cards (existing rounded-md border bg-white style):
  Properties, Recurring schedules (auto-generated jobs), Step
  checklists, Assets.
- Footer: User guide (`/user-manual/index.html`, new tab, same
  rel/aria as header), `/privacy`, `/terms`.

### T003 - Split render on /

`app/page.tsx`: `const session = await auth();` (import from
`@/lib/auth`, drop `requireSession`). `if (!session) return
<LandingPage />;` else the existing dashboard JSX + fetches untouched.
Keep `export const dynamic = "force-dynamic"`.

### T004 [P] - Legal pages

`app/privacy/page.tsx`, `app/terms/page.tsx`: server components, static
JSX prose, shared lightweight typography (e.g. a local wrapper div with
prose-like classes; do NOT add a markdown/typography dependency).

Privacy sections: What we collect (Google name/email/subject id;
maintenance data you enter); Why (to provide the service, nothing
else); Where (Microsoft Azure + MongoDB, EU region); Cookies (session
authentication only - no advertising or analytics trackers); Retention
and removal (until you request deletion; write to
contact@maintained.house); Audit records note; Changes to this policy;
Contact.

Terms sections: What the service is; Your account (Google sign-in);
Acceptable use (personal/household tracking, no abuse); Your data
(yours; see privacy policy); No warranty ("as is"); Limitation of
liability; Governing law (Poland); Changes to these terms; Contact.

Each page: h1 + "Last updated: 2026-07-15" line. Back link to `/`.

### T005 - Sign-in page footer

`(auth)/signin/page.tsx`: small centered footer under the forms with
links to `/privacy` and `/terms` (text-xs text-gray-500).

### T006 - Branding sweep

- `layout.tsx`: Metadata title + description -> "Maintained House" /
  updated description; header brand link text -> "Maintained House".
- `frontend/public/user-manual/index.html`: title tag, header brand,
  h1 -> "Maintained House" naming (content references to "Home
  Maintenance" app name updated where they name the product).
- `grep -rn "Home Maintenance" frontend/src frontend/e2e` before/after;
  update any test fixtures that assert the old brand (none known).

### T007 - Jest

`landing-page.test.tsx`: renders h1 brand, CTA href /signin, footer
links (guide/privacy/terms). Plus smoke tests for privacy and terms
pages (render, key headings, contact address present) - note these are
async server components; render via `await PrivacyPage()` pattern or
mark as integration-style tests consistent with existing suite
conventions (check how other server components are tested; if none,
test the extracted content components instead - extract
`PrivacyContent`/`TermsContent` as plain components if needed for
testability).

## Test Strategy

Jest for content/links; behavior (redirects, split render) is WP02's
e2e. Full existing local gate must stay green: lint, jest, build, 39
Playwright.

## Definition of Done

- [ ] Anonymous `/` renders landing (manual curl: 200, no 307)
- [ ] Signed-in `/` renders dashboard unchanged (wp09 suite green)
- [ ] /privacy and /terms return 200 without a session
- [ ] lint, jest, build, full e2e green locally
- [ ] No "Home Maintenance" brand string left in user-facing UI

## Risks

- Async server-component testing in jest is awkward - prefer extracting
  testable presentational components over fighting the runtime.
- RefreshAccessTokenError sessions render the landing rather than
  redirecting - acceptable (one click to sign-in); do not add special
  handling.

## Run Command

```bash
polaris implement WP01
```

## Activity Log

- 2026-07-19T10:51:06Z – unknown – lane=for_review – Implemented on branch 010-launch-readiness-WP01; PR #105
