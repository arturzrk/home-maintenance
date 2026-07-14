---
work_package_id: WP02
title: Landing switch + dashboard page
lane: "for_review"
dependencies: ["WP01"]
subtasks: [T005, T006, T007, T008, T009]
test_status: required
test_file: frontend/e2e/helpers/setup.ts
domain: frontend-craft
---

# WP02 - Landing switch + dashboard page

## Objective

Post-sign-in default destination becomes the dashboard `/`; `/` becomes a
protected, user-facing landing page. Every existing e2e test stays green
via the `signInAs` helper update - these MUST ship together.

## Context

- The ONLY place the `/properties` landing default lives:
  `frontend/src/app/(auth)/signin/page.tsx`, two form actions, each
  `redirectTo: params?.callbackUrl ?? "/properties"`.
- `frontend/src/middleware.ts` matcher today:
  `["/properties/:path*", "/jobs/:path*"]`. `/`, `/job-definitions`,
  `/assets` rely only on server-side `requireSession()`.
- CI readiness probes `http://localhost:3000/signin` (stays public) -
  protecting `/` does not affect the pipeline.
- All 33 existing Playwright tests call `signInAs`, which ends with
  `page.waitForURL(/\/properties/)`.

## Subtasks

### T005 - Sign-in redirect default

`signin/page.tsx`: both fallbacks `?? "/properties"` -> `?? "/"`.
Deep-link behavior (`callbackUrl` present) unchanged (FR-08).

### T006 - Middleware matcher hardening

`middleware.ts` config:

```ts
matcher: ["/", "/properties/:path*", "/jobs/:path*",
          "/job-definitions/:path*", "/assets/:path*"],
```

`"/"` is exact-path in Next matchers; `/signin`, `/api/auth/*`,
`/user-manual/*` remain public.

### T007 - Dashboard rewrite

`frontend/src/app/page.tsx`:

- `await requireSession()` first (defense in depth).
- Replace "Dashboard / Minimal working set" copy with user-facing text
  (e.g. heading "Home Maintenance" or "Welcome", subline about tracking
  maintenance).
- Prominent link card to `/properties` with `id="dashboard-properties-link"`
  labelled "My properties" (styling: existing card conventions -
  rounded-md border bg-white shadow-sm).
- Keep `<ConnectionStatus healthy={...} apiInfo={...} />` and its
  server-side fetches.

### T008 - E2e helper + sweep

`frontend/e2e/helpers/setup.ts` `signInAs`: replace
`page.waitForURL(/\/properties/)` with a dashboard-root wait, e.g.
`page.waitForURL((url) => url.pathname === "/")`. Grep all specs for
other post-sign-in landing assumptions (there are none known - the specs
`page.goto()` explicitly after signInAs - but verify).

### T009 - Full local gate

`npm run lint`, `npm test`, `npm run build`, then the FULL Playwright
suite (33 tests) against the local stack. Fix any stragglers here, not
in WP03.

## Test Strategy

The full existing e2e suite IS the test for this WP (SC-04). No new
spec file yet (WP03). Jest untouched unless the dashboard gains
client-component logic (it should not - pure server component).

## Definition of Done

- [ ] Stub sign-in without callbackUrl lands on `/` (manual check)
- [ ] Visiting `/` signed out redirects to /signin with callbackUrl
- [ ] 33/33 Playwright locally; lint/jest/build green
- [ ] Deep link: /properties/{id} signed out -> signin -> back on the page

## Risks

- T005 without T008 breaks all 33 tests - single commit, never split.
- Middleware `"/"` accidentally written as `"/:path*"` would capture
  /signin and loop redirects - use the exact literal list above.

## Run Command

```bash
polaris implement WP02 --base WP01
```

## Activity Log

- 2026-07-14T16:50:35Z – unknown – lane=for_review – Implemented on branch 009-system-menu-WP02; PR #99
