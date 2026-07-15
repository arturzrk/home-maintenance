# Control map - 010-launch-readiness

## Flows

| # | Flow | Entry | Steps | Exit |
|---|------|-------|-------|------|
| 1 | Anonymous front door | `/`, no session | Landing renders (brand, value prop, highlights) | CTA -> `/signin`; links -> guide/privacy/terms |
| 2 | Read legal pages | Landing or signin footer | Open `/privacy` or `/terms` | Public page, no session needed |
| 3 | Signed-in landing | `/`, with session | Dashboard renders (unchanged from 009) | as today |
| 4 | Sign-in page links | `/signin` | Footer links to privacy/terms | legal pages |
| 5 | Operator go-live | Runbook doc | Domain -> DNS -> Vercel -> App Service -> Atlas -> OAuth publish -> verify | Third-party Google account signs in on prod |

## Shared Dependencies

| Dependency | Used by flows | Notes |
|------------|--------------|-------|
| Session (`auth()`) in `/` page | 1, 3 | Split render replaces the middleware guard on `/` |
| Middleware matcher | 1, 2 | `/` leaves the matcher again; `/privacy`, `/terms` never enter it |
| Header (FR-11 of 009) | 1, 2 | Anonymous pages keep the menu-less header |
| Brand string "Maintained House" | 1, 2, 5 | Landing, legal pages, app metadata, OAuth consent screen |
| docs/oidc-setup.md | 5 | Referenced by the runbook, already uses maintained.house |
| e2e signInAs helper | 3 | Unaffected: sign-in still lands on `/` (dashboard branch) |
