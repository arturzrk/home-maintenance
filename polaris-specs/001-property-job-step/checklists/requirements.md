# Spec Quality Checklist - 001-property-job-step

Validates that `spec.md` meets the Polaris spec-quality bar before
proceeding to plan / tasks.

## Completeness

- [x] Summary section present and self-contained
- [x] At least one P1 user story
- [x] Every user story has an Independent Test and at least one
      Given/When/Then acceptance scenario
- [x] Edge cases listed (cross-owner access, stale ids, concurrent edits,
      token expiry, empty step list, reorder validity, sign-in cancel)
- [x] Functional requirements numbered (FR-001 through FR-032)
- [x] Key entities defined (OwnerId, Property, Job, Step)
- [x] Success criteria measurable and tech-agnostic
- [x] Audit and Security section answers all four required questions
      (data tier, audit logging, AuthN/AuthZ, threat surface)
- [x] Assumptions documented
- [x] Out of Scope documented
- [x] Tracker linked (GitHub issue #1)

## Testability

- [x] Every FR is testable through a direct observable behaviour
      (request -> response, state change, audit log entry, UI update)
- [x] Authorization rules are testable via cross-owner integration test
- [x] Validation rules (FR-009, FR-014, FR-021..024) specify lengths
      and trimming behaviour explicitly
- [x] Completion sealing (FR-019, FR-027) is testable as a matrix of
      "mutation X against Completed Job -> rejected"

## Technology-agnostic Success Criteria

- [x] SC-001..SC-008 phrased in user-visible or behaviour-observable
      terms, not internal-only metrics
- [x] No success criterion references a specific framework, library, or
      service name

## Constitution Alignment

- [x] Data tier (Internal) inherits from constitution baseline
- [x] Audit log policy matches constitution: append-only, 1-year
      retention, separate sink, captures auth outcomes + authz denials
      + writes/deletes against owned data + admin actions (no admin
      actions in scope here)
- [x] AuthN: Google OIDC, OwnerId from `sub`, default-deny
- [x] AuthZ: ownership-based; cross-owner returns 404 to avoid leak
- [x] No new dependencies declared in spec (plan-phase will list)
- [x] Aptean branding explicitly out of scope (deferred elsewhere)

## Scope Discipline

- [x] No speculative concepts ahead of Slice 1 (Asset, recurrence,
      multi-tenant, sharing all parked in Out of Scope or Follow-ups)
- [x] No placeholder use cases (every use case has at least one FR and
      one acceptance scenario)
- [x] Future slices named (1b, 2) so the deferred work has an owner

## Encoding and Style

- [x] No em dashes, smart quotes, or non-ASCII punctuation
      (constitution requirement; pre-commit hook will enforce)
- [x] All file paths in spec are repo-relative or absolute, never
      "the X folder" without qualification (per `.polaris/AGENTS.md`)

## Open Concerns

| Concern | Disposition |
|---|---|
| NextAuth vs. raw next-auth/google? | Deferred to plan phase |
| Embed vs. separate collection for Steps? | Defaulted to embed (Job is the aggregate); plan-phase confirms |
| JWKS caching window for Google's keys? | Deferred to plan phase |

## Sign-off

- Spec author (Claude): completed 2026-05-12
- Spec owner (user): pending review
