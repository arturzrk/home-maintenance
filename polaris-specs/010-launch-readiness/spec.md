# Feature 010 - Go-live launch readiness (Maintained House)

**Status**: Draft
**Tracker**: GitHub issue #102
**Created**: 2026-07-15

## Overview

Remove the blockers that prevent opening the app to users beyond the
developer: a public front door, the legal pages Google OAuth publishing
requires, and a step-by-step runbook for provisioning the production
environment under the public brand **Maintained House** at
**maintained.house**.

Today an anonymous visitor hitting the site is bounced straight to a
bare sign-in form with no explanation of what the product is; there is
no privacy policy or terms of service (both required to publish the
Google OAuth consent screen beyond test users); and production
infrastructure (domain, prod API, managed database, published OAuth
app) exists only as a staging setup plus intentions.

## Actors

- **Anonymous visitor** - no session; a prospective user or the Google
  OAuth verification reviewer.
- **Signed-in owner** - existing behavior; must be unaffected.
- **Operator** - the person executing the go-live runbook (domain,
  Azure, Vercel, Google Cloud Console, MongoDB Atlas).

## User Scenarios

- **US1 - The front door**: As an anonymous visitor at `/`, I see what
  Maintained House is (value proposition, feature highlights) and a
  clear call to action to sign in.
- **US2 - Legal transparency**: As an anonymous visitor, I can read the
  privacy policy and terms of service without an account, linked from
  the landing page and the sign-in page.
- **US3 - Nothing changes for members**: As a signed-in owner, `/` is
  still my dashboard, and every protected page behaves exactly as
  before.
- **US4 - Provisioning**: As the operator, I can follow a single
  runbook document from "domain not purchased" to "any Google account
  can sign in at https://maintained.house" without guessing any step.
- **US5 - OAuth verification**: As the Google reviewer, the app
  homepage and privacy policy URLs referenced by the consent screen
  resolve publicly and name the same brand (Maintained House).

## Functional Requirements

- **FR-01** `/` is publicly reachable. Without a session it renders the
  landing page; with a session it renders the dashboard (unchanged).
- **FR-02** The landing page presents: the Maintained House brand, a
  one-sentence value proposition, feature highlights (properties,
  recurring schedules with auto-generated jobs, step checklists,
  assets), a prominent sign-in call to action leading to the sign-in
  page, and links to the user guide, privacy policy, and terms.
- **FR-03** A public privacy policy page states: what is collected
  (Google account identity - name, email, subject id - and the
  maintenance data the user enters), why, where it is stored (Azure /
  MongoDB), that only session cookies are used (no advertising or
  analytics trackers), how to request data removal, and the contact
  address contact@maintained.house.
- **FR-04** A public terms-of-service page states: the service
  description, acceptable use, no-warranty and liability limitation
  wording, governing jurisdiction Poland, and the contact address.
- **FR-05** The privacy and terms pages are reachable without a
  session and are linked from both the landing page and the sign-in
  page.
- **FR-06** All protected routes keep their current behavior: visiting
  them without a session redirects to sign-in with a deep link; the
  full existing e2e suite passes.
- **FR-07** A go-live runbook document walks the operator through:
  domain purchase and DNS, Vercel production domain, Azure App Service
  production configuration and custom domain (api.maintained.house)
  with TLS, managed MongoDB (Atlas, TLS) and backup posture, secret
  wiring on both platforms, Google OAuth consent-screen publishing
  (brand name, homepage, privacy URL, authorized domains, production
  redirect URIs), the contact mailbox, and a post-launch verification
  checklist. Each step states what to do, where, and how to verify it.
- **FR-08** Public-facing brand consistency: the landing, legal pages,
  and app metadata use "Maintained House"; repository docs that name
  the production domain agree on maintained.house.
- **FR-09** New e2e coverage: anonymous `/` shows the landing (brand,
  CTA, legal links); privacy and terms pages render without a session;
  the CTA reaches the sign-in page; a signed-in user at `/` still sees
  the dashboard.

## Success Criteria

- **SC-01** An anonymous visit to `/` explains the product and offers
  sign-in within one click; no redirect to a bare form.
- **SC-02** Privacy and terms URLs return public content suitable for
  the Google OAuth consent screen fields.
- **SC-03** The full e2e suite (existing + new) passes; signed-in
  behavior is unaffected except the anonymous `/` case.
- **SC-04** Following only the runbook, the operator reaches a state
  where a Google account that is not the developer's signs in
  successfully on the production domain.
- **SC-05** No step in the runbook requires reading source code to
  execute.

## Key Entities

None - no domain-model or backend changes. The feature is frontend
pages plus operator documentation.

## Out of Scope

- Reminders/notifications, dashboard content, sharing (separate
  roadmap features).
- Infrastructure-as-code; provisioning stays manual per runbook.
- Marketing content beyond a single landing page (no blog, screenshots
  pipeline, or SEO work).
- Custom email hosting setup beyond documenting the forwarding step
  for contact@maintained.house.

## Assumptions

- Brand: Maintained House; domain maintained.house (already the
  constitution's reserved target - purchase is a runbook step, not a
  precondition for the code work).
- Contact address contact@maintained.house (forwarding/mailbox is a
  runbook step).
- Legal pages are honest plain-language documents, not lawyer-reviewed
  texts; jurisdiction Poland. They can be revised later without
  re-verification concerns as long as URLs are stable.
- Google OAuth verification for basic scopes (openid/email/profile) is
  the lightweight flow (no restricted-scope audit).
- Tracker issue #102; estimate skipped.
