# Tasks - 009 System menu & dashboard landing

Feature: persistent header system menu + dashboard as post-sign-in landing.
Frontend-only; no backend changes. Spec: [spec.md](spec.md), plan: [plan.md](plan.md).

## Work Packages

### WP01 - System menu + header rework (frontend-craft)

Session-aware header with the system menu island.

- [x] T001 Sign-out server action (`frontend/src/app/actions.ts`): `"use server"`, calls `signOut({ redirectTo: "/signin" })` from `@/lib/auth`
- [x] T002 `frontend/src/components/system-menu.tsx` client component: identity trigger (`#system-menu-trigger`, aria-expanded/haspopup), popover `role="menu"` with My properties link, User guide link (new tab), system-info block (version + health), Sign out form; closes on outside click / Escape / item click
- [x] T003 `frontend/src/app/layout.tsx`: brand becomes `<Link href="/">`; resolve `auth()` + `getApiInfo()`/`checkHealth()` (failure-tolerant); render SystemMenu when session exists, else plain User guide link (FR-11)
- [x] T004 Jest: system-menu.test.tsx (identity, open/close incl. Escape, items, health states, sign-out form present)

Sketch: layout stays a Server Component; menu receives only plain props
(identity string, version, healthy) - never the idToken. No new deps.

Dependencies: none. Parallel: T001 [P] with T002.

### WP02 - Landing switch + dashboard page (frontend-craft, deps: WP01)

Make `/` the protected landing page; keep every existing e2e green.

- [x] T005 `signin/page.tsx`: both `redirectTo` fallbacks `"/properties"` -> `"/"` (callbackUrl deep links untouched)
- [x] T006 `middleware.ts` matcher: add `"/"`, `"/job-definitions/:path*"`, `"/assets/:path*"`
- [x] T007 Dashboard `app/page.tsx`: `requireSession()`, user-facing copy, prominent My properties link (`#dashboard-properties-link`), keep ConnectionStatus
- [x] T008 e2e helper `signInAs`: waitForURL dashboard root instead of `/properties`; sweep existing specs for landing assumptions
- [x] T009 Full local gate: lint, jest, build, full Playwright suite (33) green

Sketch: T005 and T008 MUST land together (all existing e2e go through
signInAs). Matcher `"/"` is exact-path; `/signin` and `/user-manual/*`
stay public (CI probes `/signin` - verified).

Dependencies: WP01 (header/menu already on the branch keeps the gate honest).

### WP03 - E2E: system menu suite (testing-specialist, deps: WP02)

`frontend/e2e/wp09-system-menu.spec.ts`, one describe "WP09: System menu".

- [ ] T010 WP09-1: stub sign-in without callbackUrl lands on dashboard (SC-02)
- [ ] T011 WP09-2: menu trigger shows identity; My properties item navigates (US1/US2)
- [ ] T012 WP09-3: system-info block shows version and Connected state (US4)
- [ ] T013 WP09-4: sign out -> /signin; then /properties redirects to sign-in (US5/SC-03)
- [ ] T014 WP09-5: deep link preserved - visit a property page signed out, sign in, land back on it (FR-08)
- [ ] T015 WP09-6: dashboard My properties CTA navigates (US6)

Dependencies: WP02. All tests use uniqueUser() isolation; id-based
locators (`#system-menu-trigger`, `#dashboard-properties-link`).

## Sequencing

WP01 -> WP02 -> WP03 (strict chain; small feature, no parallel WPs).
MVP scope = WP01+WP02 (menu usable, landing switched); WP03 locks it in.
