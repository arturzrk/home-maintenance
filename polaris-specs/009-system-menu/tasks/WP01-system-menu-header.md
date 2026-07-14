---
work_package_id: WP01
title: System menu + header rework
lane: "doing"
dependencies: []
base_branch: main
base_commit: 117d9a6859cecd903818a44de1019a0b3d80ce26
created_at: '2026-07-14T15:14:05.308673+00:00'
subtasks: [T001, T002, T003, T004]
test_status: required
test_file: frontend/src/components/__tests__/system-menu.test.tsx
domain: frontend-craft
shell_pid: "981"
---

# WP01 - System menu + header rework

## Objective

Session-aware header on every page: brand links home, signed-in users get
a dropdown system menu (identity, My properties, User guide, system info,
Sign out). No session -> today's plain header (FR-11).

## Context

- `frontend/src/app/layout.tsx` is a Server Component and may call
  `auth()` directly (NOT `requireSession` - it must render on /signin
  without redirecting).
- `frontend/src/lib/auth.ts` exports `{ handlers, auth, signIn, signOut }`.
- Patterns to mirror: `ConnectionStatus` (health dot styling),
  `create-job-form.tsx` (id-labelled controls), server-action conventions
  in `app/*/actions.ts`.
- Identity string: `session.user?.name ?? session.user?.email ?? "Account"`.
  Dev-stub sessions surface the stub sub via these fields.

## Subtasks

### T001 [P] - Sign-out server action

`frontend/src/app/actions.ts` (new):

```ts
"use server";
import { signOut } from "@/lib/auth";

export async function signOutAction(): Promise<void> {
  await signOut({ redirectTo: "/signin" });
}
```

No confirmation dialog (app convention). NEXT_REDIRECT propagates.

### T002 - SystemMenu client component

`frontend/src/components/system-menu.tsx`, `"use client"`.

Props: `{ identity: string; version: string | null; healthy: boolean;
signOutAction: () => Promise<void> }` (pass the server action as a prop
from the layout so the component stays presentational and testable).

- Trigger: `<button id="system-menu-trigger" aria-haspopup="menu"
  aria-expanded={open} aria-label="System menu">` showing `identity`.
- Panel (`role="menu"`, absolutely positioned under the trigger):
  - Link "My properties" -> `/properties` (closes menu on click)
  - Link "User guide" -> `/user-manual/index.html`, `target="_blank"`,
    `rel="noopener noreferrer"` (mirror current header link)
  - System info block (non-interactive): "Version {version ?? "unknown"}"
    and "API: Connected / Unreachable" with a green/red dot span
  - `<form action={signOutAction}><button type="submit">Sign out</button></form>`
- Close on: outside click (document mousedown listener while open),
  Escape keydown, any nav item click. Plain useState/useEffect; no deps.

### T003 - Layout rework

`frontend/src/app/layout.tsx`:

- Brand: `<Link href="/" className=...>Home Maintenance</Link>`.
- Fetch in parallel, failure-tolerant (same pattern as the dashboard):
  `const [session, healthy, apiInfo] = await Promise.all([auth(),
  checkHealth(), getApiInfo().catch(() => null)])`.
- Right side: session ? `<SystemMenu identity=... version={apiInfo?.version
  ?? null} healthy={healthy} signOutAction={signOutAction} />` : the
  existing User guide `<a>` unchanged.

### T004 - Jest tests

`frontend/src/components/__tests__/system-menu.test.tsx`:

- renders identity on the trigger
- opens on click (menu items visible), closes on Escape and outside click
- My properties link href; User guide link target/_blank + rel
- "Version 1.2.3" and Connected (healthy) vs Unreachable (!healthy)
- sign-out button submits the passed action (mock fn assert called)

Mock `next/navigation` as in existing suites if needed.

## Test Strategy

Jest only in this WP (component-level). Full e2e continuity is WP02's
gate; the header change alone must not break existing specs (menu is
additive; `signInAs` unaffected until WP02 changes the landing).

## Definition of Done

- [ ] `npm run lint`, `npm test`, `npm run build` green
- [ ] Existing Playwright suite (33) still passes locally
- [ ] /signin shows plain header (no menu, no identity)
- [ ] idToken never passed to any client component

## Risks

- Passing a server action as a prop to a client component is supported
  (Next 15) but the prop must be a plain async function reference.
- Outside-click listener must not leak: add/remove in useEffect tied to
  `open`.

## Run Command

```bash
polaris implement WP01
```
