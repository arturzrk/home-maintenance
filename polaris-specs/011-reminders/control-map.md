# Control map - 011-reminders

## Flows

| # | Flow | Entry | Steps | Exit |
|---|------|-------|-------|------|
| 1 | Profile capture | Any authenticated API request | Backend reads the email claim already present on the validated token; upserts it if new/changed | Owner has a stored contact email, no frontend involvement |
| 2 | Daily digest pass | Scheduled (24h) | Find active jobs due <=3 days or overdue, group by owner then property, skip owners with reminders off or no email, send one email per remaining owner | Email(s) sent; per-owner failures logged, do not abort the pass |
| 3 | Email click-through | Job link in a digest email | Opens `/jobs/{id}`; if signed out, middleware -> `/signin?callbackUrl=...` -> sign in -> back to the job page | Owner viewing their job (reuses existing 009 deep-link behavior) |
| 4 | Toggle reminders | System menu -> "Notification settings" | Open `/settings/notifications`, toggle Reminders on/off | Preference persisted, reflected on reload |
| 5 | Opt out from the email | Digest email footer link | Same destination as flow 4 | Preference persisted |

## Shared Dependencies

| Dependency | Used by flows | Notes |
|------------|--------------|-------|
| Notification preference store (new) | 1, 2, 4, 5 | Per-owner: email + remindersEnabled; auto-created on first authenticated request (flow 1) |
| `IJobRepository` (new cross-owner query) | 2 | `ListDueOrOverdueAsync` mirrors the existing owner-less `IJobDefinitionRepository.ListAllActiveAsync` precedent |
| `IEmailSender` port (new) | 2 | Resend in staging/prod; a logging no-op implementation in Development/CI so no real email account is required to build or test |
| Existing auth/middleware deep-link behavior | 3 | No changes needed - `/jobs/:path*` is already a protected route with callbackUrl preservation (verified in feature 009) |
| System menu (`system-menu.tsx`) | 4 | Gains one new link; existing close-on-click/Escape/outside-click behavior unchanged |
| Middleware matcher | 4, 5 | `/settings/:path*` must be added, following the same pattern as `/assets` and `/job-definitions` in feature 009 |
