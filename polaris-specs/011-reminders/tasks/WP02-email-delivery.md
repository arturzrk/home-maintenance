---
work_package_id: WP02
title: "Email delivery"
lane: "done"
dependencies: []
subtasks: [T008, T009, T010, T011, T012]
test_status: required
test_file: backend/tests/HomeMaintenance.UnitTests/Infrastructure/ResendEmailSenderTests.cs
domain: backend-logic
reviewed_by: "Artur Żurek"
review_status: "approved"
---

# WP02 - Email delivery

## Objective

`IEmailSender` port with two implementations: `ResendEmailSender` for
staging/prod, `LoggingEmailSender` (no-op, logs instead) as the
Development/CI default - mirrors the existing `Auth:UseStub` pattern so
no real Resend account or key is ever required to build, run, or test
locally or in CI.

## Context

- No dependency on WP01 - can be implemented in parallel.
- appsettings pattern to follow: `appsettings.json` (dev defaults) +
  `appsettings.Staging.json` (placeholders/comments) + env var overrides
  via `Section__Key`. Register the hosted/DI pieces in
  `Infrastructure/DependencyInjection.cs`.
- Look at how `Auth:UseStub` picks an implementation and fails fast when
  misconfigured - `Email:Provider` follows the same shape.

## Subtasks

### T008 - `IEmailSender` port

`Application/Common/Interfaces/IEmailSender.cs`:
`Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken ct = default)`.

### T009 - `ResendEmailSender`

Infrastructure implementation: `HttpClient` POST to Resend's email API,
`Authorization: Bearer {ApiKey}`, configured `From` address. Options
bound from `Email:Resend:ApiKey`, `Email:FromAddress`.

### T010 - `LoggingEmailSender`

Infrastructure no-op implementation: logs recipient/subject/body instead
of sending. Selected whenever `Email:Provider` is `Log` (the
Development/CI default).

### T011 - Config + DI + startup assertion

Wire `Email:Provider` selection in `DependencyInjection.cs`; add
`appsettings.json`/`appsettings.Staging.json` entries. Startup assertion:
if `Email:Provider` is `Resend` and `Email:Resend:ApiKey` is missing,
fail fast at startup (mirrors the `Auth:UseStub` misconfiguration
check) rather than silently dropping mail.

### T012 - Unit tests

`ResendEmailSender` request-shape test against a fake
`HttpMessageHandler` (method, URL, auth header, body shape);
`LoggingEmailSender` logs instead of sending; startup-assertion test for
a missing Resend key when `Email:Provider=Resend`.

## Definition of Done

- [ ] `IEmailSender`, `ResendEmailSender`, `LoggingEmailSender` implemented
- [ ] `Email:Provider=Log` remains the Development/CI default - no test ever calls the real Resend API
- [ ] Startup fails fast on `Email:Provider=Resend` with no API key
- [ ] Unit tests green

## Risks

- CI must never hit real Resend - `Email:Provider=Log` staying the
  default for Development/CI is the safeguard; the startup assertion
  protects production from silently not sending, not CI from
  accidentally sending.

## Run Command

```bash
polaris implement WP02
```

## Activity Log

- 2026-07-26T12:04:16Z -- unknown -- lane=doing -- Moved to doing
- 2026-07-26T12:04:18Z -- unknown -- lane=testing -- dotnet test green: 186/186 Unit.Tests + 204/204 Integration.Tests (6 new email unit tests: provider selection/startup assertion, Resend request-shape + non-2xx handling, logging sender)
- 2026-07-26T12:04:27Z -- unknown -- lane=for_review -- Email delivery implemented per plan: IEmailSender port, ResendEmailSender (typed HttpClient), LoggingEmailSender (Development/CI default), EmailExtensions.AddEmailSending with startup fail-fast when Provider=Resend and no ApiKey (mirrors Auth:UseStub). appsettings.json defaults to Log; appsettings.Staging.json set to Resend with ApiKey via env var. dotnet test green: 186/186 Unit + 204/204 Integration. polaris runtests CLI bug (get_specs_dir undefined, same as WP01/WP02/WP03/WP04 in prior features) - ran dotnet test directly.
- 2026-07-26T16:13:52Z -- unknown -- lane=done -- Merged via PR #120 (code, Copilot findings addressed) and PR #119 (kanban tracking).
