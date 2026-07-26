---
work_package_id: WP04
title: Frontend settings + menu link
lane: planned
dependencies: ["WP01"]
subtasks: [T020, T021, T022, T023, T024, T025, T026]
test_status: required
test_file: frontend/src/components/__tests__/notification-settings-toggle.test.tsx
domain: frontend-craft
---

# WP04 - Frontend settings + menu link

## Objective

Settings page + toggle so an owner can turn reminder emails on/off, a
system-menu link to reach it, and the user-manual update every prior
user-facing feature has included (FR-10).

## Context

- Only needs WP01 (the preferences API) - not WP02/WP03.
- `system-menu.tsx` (feature 009) is the pattern for the new link:
  closes on click, same list-item styling as existing items.
- `middleware.ts` currently protects `/properties`, `/jobs`,
  `/job-definitions`, `/assets` - `/settings/:path*` needs to be added
  the same way `/assets` was added in feature 009. This class of miss
  (forgetting the matcher entry) already happened once before - don't
  repeat it.
- Toggle component should mirror `AssetHeader`'s obsolete-toggle
  pattern: button, pending state, inline error, `router.refresh()`.
- User manual lives at `frontend/public/user-manual/index.html` - add a
  new "Reminders" section following the same structure/tone as the
  existing Assets and System menu sections (what triggers a digest,
  what it contains, how to turn it on/off).

## Subtasks

### T020 - api-client

`frontend/src/lib/api-client.ts`:
`notificationPreferences.get()` / `.update(enabled)`.

### T021 - Server actions

`frontend/src/app/settings/notifications/actions.ts`: `ActionResult`
convention used throughout.

### T022 - Settings page + toggle component

`frontend/src/app/settings/notifications/page.tsx` (server component,
`requireSession`) + a small client toggle component (button, pending
state, inline error, `router.refresh()` on success).

### T023 - System menu link

`system-menu.tsx`: one new "Notification settings" link to
`/settings/notifications`, closes the menu on click like every other
item.

### T024 - Middleware matcher

Add `/settings/:path*` to the protected matcher in `middleware.ts`.

### T025 - User manual update (FR-10)

`frontend/public/user-manual/index.html`: new "Reminders" section -
what triggers a daily digest, what it contains (jobs grouped by
property, due/overdue), and how to turn it on/off from Notification
settings.

### T026 - Jest tests

Notification-settings toggle component: renders current state, submits
toggle, shows pending/error states.

## Definition of Done

- [ ] Settings page reachable only when signed in (`/settings/:path*` protected)
- [ ] Toggle reflects and persists `RemindersEnabled` via WP01's API
- [ ] System menu has a working "Notification settings" link
- [ ] User manual documents reminders (FR-10)
- [ ] Jest tests green

## Risks

- Forgetting the middleware matcher entry is an easy miss (happened
  once before, in feature 009, for `/assets`) - explicit subtask above.

## Run Command

```bash
polaris implement WP04 --base WP01
```
