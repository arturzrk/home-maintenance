# Implementation Plan: 009 --- System menu & dashboard landing

**Branch**: worktree branches per WP (`009-system-menu-WP##`) | **Date**: 2026-07-14 | **Spec**: [spec.md](spec.md)
**Tracker**: GitHub issue #93

## Summary

Frontend-only feature. Add a session-aware system menu to the root-layout
header (identity trigger, My properties, User guide, system info, Sign out),
make the brand a home link, switch the post-sign-in default destination to
`/` and protect + rewrite the dashboard as a user-facing landing page.
Update the e2e `signInAs` helper and add a new e2e suite. No backend or
domain changes; no new persisted data.

## Technical Context

**Current state (verified in code)**

- `frontend/src/app/layout.tsx` --- static header: brand `<span>` (not a
  link) + "User guide" `<a>`. Root layout is a Server Component; it can
  call `auth()` directly.
- `frontend/src/lib/auth.ts` --- NextAuth v5; exports `{ handlers, auth,
  signIn, signOut }`. Dev-stub provider active when `NEXTAUTH_DEV_STUB=true`.
- `frontend/src/app/(auth)/signin/page.tsx` --- both form actions use
  `redirectTo: params?.callbackUrl ?? "/properties"`. This is the ONLY
  place the `/properties` landing default lives.
- `frontend/src/middleware.ts` --- matcher `["/properties/:path*",
  "/jobs/:path*"]`; `/` is currently public. `/job-definitions` and
  `/assets` pages are protected only by their server-side
  `requireSession()` (which redirects without a callbackUrl).
- `frontend/src/app/page.tsx` --- dashboard placeholder; fetches
  `checkHealth()` + `getApiInfo()` server-side, renders `ConnectionStatus`.
- e2e `signInAs` (frontend/e2e/helpers/setup.ts) waits for
  `page.waitForURL(/\/properties/)` after stub sign-in.

**Session identity**: `session.user.name` / `session.user.email` (Google);
dev-stub sessions carry the stub sub as the user identity --- render
`session.user?.name ?? session.user?.email ?? "Account"`.

## Architecture

### Header & menu

- `layout.tsx` stays a Server Component. It resolves `const session =
  await auth()` (NO `requireSession` --- the header must render without a
  session on `/signin`, FR-11) and fetches `getApiInfo()`/`checkHealth()`
  in the same `Promise.all` style as the dashboard, tolerating failure
  (`healthy=false`, `apiInfo=null`).
- New client component `frontend/src/components/system-menu.tsx`:
  - Props: `{ identity: string; version: string | null; healthy: boolean }`.
  - Rendered only when a session exists; otherwise the header keeps the
    plain "User guide" link (FR-11).
  - Trigger: `<button id="system-menu-trigger" aria-label="System menu"
    aria-expanded aria-haspopup="menu">` showing the identity text.
  - Popover: absolutely-positioned panel, `role="menu"`; items --- link
    "My properties" (`/properties`), link "User guide" (new tab, same
    rel/aria as today), a non-interactive system-info block ("Version
    {x.y.z}" + "API: Connected / Unreachable" with green/red dot classes
    consistent with `ConnectionStatus`), and a "Sign out" button.
  - Close behavior: outside click (document listener while open), Escape,
    and on navigation-item click. Plain React state; no headless-UI dep
    (consistent with the zero-dependency component style of the codebase).
- Sign out: NextAuth v5 server action pattern --- the menu's sign-out button
  submits a `<form action={signOutAction}>` where `signOutAction` is a
  `"use server"` function calling `signOut({ redirectTo: "/signin" })`.
  Server action lives in `frontend/src/app/actions.ts` (new, tiny) to keep
  `layout.tsx` clean. No confirmation dialog (app convention).

### Landing & protection

- `signin/page.tsx`: both `redirectTo` defaults change to `"/"`.
  `callbackUrl` deep-link behavior is untouched (FR-08).
- `middleware.ts` matcher gains `"/"` plus the currently-missing
  `"/job-definitions/:path*"` and `"/assets/:path*"` (low-risk hardening;
  behavior for those pages today is a `requireSession` redirect without
  callbackUrl --- after this they also get proper `callbackUrl` deep links).
  Note: matcher `"/"` matches only the exact root path --- Next.js matcher
  semantics --- so static assets and `/signin` remain public.
- `app/page.tsx` (dashboard): call `await requireSession()` (defense in
  depth alongside middleware), replace dev copy with a user-facing
  greeting, keep `ConnectionStatus`, and add a prominent "My properties"
  link card (`id="dashboard-properties-link"` for e2e).

### Tests

- **Jest**: `system-menu.test.tsx` --- renders identity; opens/closes on
  trigger click and Escape; shows version + health states; menu absent
  when no session (tested via header render logic --- see WP note);
  sign-out form present. Update any snapshot/DOM assumptions in existing
  tests touching layout (none currently import layout --- verify).
- **e2e**: `signInAs` helper waits for `/$` (dashboard) instead of
  `/properties`. New `wp09-system-menu.spec.ts`:
  1. sign-in lands on dashboard (SC-02)
  2. menu shows identity; My properties navigates (US1/US2)
  3. system info block shows version + Connected (US4)
  4. sign out → /signin; direct visit to /properties redirects back to
     signin (US5/SC-03)
  5. deep link: visit /properties/{id} signed out → signin →
     stub sign-in → back on the property page (FR-08)
  6. dashboard CTA navigates to My properties (US6)
- Existing 33 e2e tests re-run: only the helper change affects them.

## Constitution Check

- Clean architecture untouched (no backend change). ✓
- "Start minimal, grow intentionally": no new dependencies; plain React
  menu; server actions per existing conventions. ✓
- Test-driven: jest + e2e cover every FR; CI e2e job gates the PR. ✓
- Security baseline: `/` joins protected routes; sign-out invalidates the
  session cookie via NextAuth; no token exposure to client (identity
  string only --- never the idToken). ✓

## Work Package Sketch (input to /polaris.tasks)

- **WP01 --- System menu + header rework** (frontend-craft):
  layout.tsx session/header, system-menu.tsx, sign-out server action,
  jest tests.
- **WP02 --- Landing switch + dashboard page** (frontend-craft, deps WP01):
  signin redirectTo, middleware matcher, dashboard rewrite, jest tests,
  e2e helper update + full-suite fix-up.
- **WP03 --- E2E suite** (testing-specialist, deps WP02):
  wp09-system-menu.spec.ts (6 tests above).

Small feature --- 3 WPs keeps each independently green (helper change in
WP02 must land with the redirect change or the suite breaks).

## Risks

- **e2e helper coupling**: every existing spec calls `signInAs`; the
  waitForURL change MUST ship in the same WP as the signin redirect
  change, or 33 tests fail.
- **Header on /signin**: layout renders for the signin page too; menu must
  gate on session, not on route.
- **Health fetch in layout**: runs on every page render (no-store). It is
  one extra internal fetch per request --- acceptable at personal scale;
  note for future caching if it ever shows up in traces.
- **Middleware matcher `"/"`**: does not intercept `/user-manual/*`
  static files (exact-path match). CI readiness is unaffected --- verified
  ci.yml probes `http://localhost:3000/signin`, which stays public.

## Research / Data model / Contracts

No unknowns requiring research.md; no data-model changes; no API contract
changes (consumes existing `GET /` and `GET /health`).
