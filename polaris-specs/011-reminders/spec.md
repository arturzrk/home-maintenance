# Feature 011 - Reminders (due/overdue job email digest)

**Status**: Draft
**Tracker**: GitHub issue #112
**Created**: 2026-07-26

## Overview

Make the app proactive instead of something an owner has to remember to
open. Once a day, every owner with at least one job due within 3 days
or overdue receives a single email summarizing exactly that, grouped
by property, with a direct link into the app for each job. Owners with
nothing due receive nothing that day. Reminders can be turned off (and
back on) at any time.

Today nothing notifies anyone of anything - the whole value of the app
("don't forget the boiler service") depends entirely on the owner
remembering to open it. This closes that gap with the smallest useful
mechanism: one daily email, not a notification platform.

## Actors

- **Property owner** - a signed-in user with jobs across one or more
  properties.
- **The system** - a scheduled, unattended process that evaluates every
  owner's jobs once daily and sends digests. No human operates it.

## User Scenarios

- **US1 - Daily digest**: As an owner with jobs due soon or overdue, I
  receive one email a day summarizing them, grouped by property, so I
  don't have to remember to check the app.
- **US2 - No noise**: As an owner with nothing due or overdue, I
  receive no email that day.
- **US3 - One-click access**: From the digest email, clicking a job
  takes me straight to that job's page in the app. If I'm not currently
  signed in, I'm taken through sign-in and land on that same page
  afterward.
- **US4 - Turn it off**: I can disable reminder emails at any time from
  within the app, and turn them back on later.
- **US5 - Discoverable opt-out**: Every digest email includes a direct
  link to the place I can turn reminders off, so I don't have to hunt
  for it.
- **US6 - No extra signup**: My email address is picked up automatically
  from the account I already use to sign in - I never have to type it
  in anywhere.

## Functional Requirements

- **FR-01** Once daily, the system identifies every active (not
  completed) job, across every owner, that is due within 3 days or is
  already overdue.
- **FR-02** For each owner with at least one qualifying job that day,
  the system sends exactly one digest email, listing the qualifying
  jobs grouped by property, each showing the job name and its due date
  (or that it is overdue).
- **FR-03** Owners with zero qualifying jobs on a given day receive no
  email that day (FR-01/02 produce no output for them).
- **FR-04** Each job in the digest is a link that opens that job's page
  in the app. If the recipient is not signed in when they click it,
  they are routed through sign-in and returned to that same page
  (reusing the app's existing deep-link behavior).
- **FR-05** The digest email includes a link to the in-app place where
  reminders can be turned on or off.
- **FR-06** An owner can enable or disable reminder emails at any time;
  the setting is visible and changeable from within the app. Reminders
  are enabled by default so the feature delivers value without
  requiring discovery.
- **FR-07** The system determines an owner's contact email from their
  existing sign-in identity - no separate email-entry step exists
  anywhere in the app.
- **FR-08** A digest reflects state at send time only; jobs completed,
  edited, or removed after a digest was sent do not retroactively
  change that already-sent email.
- **FR-09** If a digest cannot be delivered for one owner (delivery
  failure, missing contact information), that failure does not prevent
  digests from being evaluated and sent for other owners.

## Success Criteria

- **SC-01** An owner with exactly one job due within the next 3 days
  and nothing else outstanding receives exactly one email that day,
  naming that job.
- **SC-02** An owner with zero due-or-overdue jobs receives zero
  digest emails that day.
- **SC-03** Clicking a job link in a digest email lands the owner on
  that job's page in the app, signing in first if necessary.
- **SC-04** Disabling reminders stops the next scheduled digest from
  being sent to that owner; re-enabling resumes delivery from the next
  scheduled run.
- **SC-05** The full existing automated test suite continues to pass,
  with new coverage added for the preference toggle and the due/overdue
  qualification rule.

## Key Entities

- **Notification preference** - per owner: whether reminder emails are
  enabled, and the contact email address to send them to (captured
  automatically from sign-in, not entered by the owner).
- **Reminder digest** - not a stored record; a summary computed fresh
  each day from an owner's current jobs at send time (per FR-08).

## Out of Scope

- SMS, push notifications, or any channel besides email (may be added
  later; this feature is built so detection of "what's due" does not
  need to change to add another delivery channel).
- Per-owner customization of the 3-day window, digest frequency, or
  quiet hours.
- Marking a job done, snoozing, or otherwise acting on a job directly
  from within the email.
- A dedicated unsubscribe flow outside the app (the in-app toggle,
  linked from every email, is the only opt-out mechanism).

## Assumptions

- Reminders default to **enabled** for every owner, so the feature
  delivers value immediately without requiring the owner to find and
  turn on a setting.
- Email delivery uses a transactional email provider (Resend); no
  in-house mail server.
- Digest send time is a fixed daily schedule chosen by the operator,
  not configurable per owner.
- No estimate captured (skipped); tracker issue #112 created per the
  established team pattern.
