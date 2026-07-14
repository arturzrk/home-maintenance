# Feature 009 --- System menu & dashboard landing

**Status**: Draft
**Tracker**: GitHub issue #93
**Created**: 2026-07-14

## Overview

Give the app a persistent system menu in the header --- identity, navigation,
system info, and sign-out available from every page --- and make the dashboard
(`/`) the app's landing page after sign-in. This is the first step toward the
dashboard becoming the true home of the app; rich dashboard content (due
jobs, per-property summaries) is explicitly out of scope here.

Today the header offers only static brand text and a "User guide" link.
There is no way to sign out anywhere in the app, no navigation to
My properties except typing the URL, and the post-sign-in landing page is
`/properties` while `/` remains a developer placeholder.

## Actors

- **Signed-in owner** --- a user with an active session (Google in production,
  dev-stub in development/CI).
- **Unauthenticated visitor** --- anyone without a session.

## User Scenarios

- **US1 --- Identity at a glance**: As a signed-in user, on any page I can see
  who I'm signed in as via the menu trigger in the header.
- **US2 --- Navigate anywhere**: From any page I open the system menu and jump
  to My properties in one click.
- **US3 --- Help**: From the system menu I open the User guide (new tab).
- **US4 --- System info**: In the system menu I can see the app version and
  whether the backend is reachable (health).
- **US5 --- Sign out**: From the system menu I sign out; I land on the sign-in
  page immediately (no confirmation) and can no longer open app pages
  without signing in again.
- **US6 --- Landing on the dashboard**: After signing in I land on the
  dashboard, which greets me in user-facing language and offers a clear
  path to My properties.
- **US7 --- Brand goes home**: Clicking the "Home Maintenance" brand in the
  header takes me to the dashboard.

## Functional Requirements

- **FR-01** The header appears on every page and the brand text
  "Home Maintenance" is a link to `/`.
- **FR-02** For a signed-in user the header shows a system-menu trigger
  displaying the user's identity (display name or email; in dev-stub
  sessions the stub identity).
- **FR-03** The menu opens on trigger click and closes on outside click,
  on Escape, and after choosing a navigation item.
- **FR-04** The menu contains **My properties**, navigating to `/properties`.
- **FR-05** The menu contains **User guide**, opening
  `/user-manual/index.html` in a new tab.
- **FR-06** The menu contains a system-info block showing the app version
  and a live backend-health indicator (healthy/unreachable), matching what
  the dashboard's connection widget reports.
- **FR-07** The menu contains **Sign out**: one click ends the session with
  no confirmation dialog and redirects to the sign-in page; subsequently
  requesting a protected page redirects to sign-in.
- **FR-08** After sign-in the default destination is the dashboard `/`
  (both Google and dev-stub flows). Deep links keep working: signing in
  after being redirected from a specific protected URL returns the user to
  that URL, not to the dashboard.
- **FR-09** The dashboard (`/`) requires sign-in, uses user-facing copy
  (no "minimal working set" developer text), offers a prominent link to
  My properties, and retains the backend connection-status widget.
- **FR-10** The menu is keyboard-operable and the trigger and menu carry
  appropriate accessible names.
- **FR-11** Without a session (e.g. the sign-in page) the header shows only
  the brand and the User guide link --- no menu trigger, no identity.

## Success Criteria

- **SC-01** From any app page a signed-in user can reach My properties,
  the User guide, or sign-out in at most 2 clicks.
- **SC-02** Completing sign-in without a deep link lands on the dashboard.
- **SC-03** After signing out, opening any protected page requires signing
  in again (verified by e2e).
- **SC-04** The full existing e2e suite passes with the new landing
  behavior (helper updated), plus new e2e coverage for menu navigation and
  sign-out.
- **SC-05** The menu shows the identity of the signed-in account, and two
  different accounts see their own identities.

## Key Entities

No new domain entities and no backend changes. The feature consumes the
existing session (identity), the existing public API info endpoint
(version) and health endpoint (reachability).

## Out of Scope

- Rich dashboard content (due-soon jobs, per-property summaries, calendar).
- Account management (profile editing, account deletion).
- Notification/reminder entry points.
- Mobile-specific navigation patterns beyond the responsive dropdown.

## Assumptions

- Tracker issue created (#93) per team pattern; no baseline estimate
  recorded (skipped).
- The app version shown is the backend-reported service version (the same
  source the dashboard widget uses today); a separate frontend build
  version is not displayed.
- The sign-in page keeps its current design; this feature only adjusts its
  post-completion destination.
