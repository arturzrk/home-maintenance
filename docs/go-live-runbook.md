# Go-live runbook - maintained.house

The operator's checklist from "domain not purchased" to "any Google
account can sign in at https://maintained.house". Work through the
phases in order; every step says **what** to do, **where**, and how to
**verify** it. No step requires reading source code.

Companion documents: [oidc-setup.md](oidc-setup.md) (Google client
setup and the full env-var matrices - this runbook references rather
than repeats them) and the README Deployment section (staging deploy
workflow this builds on).

Production values used throughout:

| Item | Value |
|---|---|
| Public brand | Maintained House |
| Frontend domain | `maintained.house` (Vercel) |
| API domain | `api.maintained.house` (Azure App Service) |
| Registrar | Porkbun |
| Contact mailbox | `contact@maintained.house` (Migadu, or Zoho Mail Lite) |
| Reminder sender | `reminders@notify.maintained.house` (Resend) |
| Database | MongoDB Atlas, EU region, TLS |
| OAuth consent | Published, basic scopes (`openid`, `email`, `profile`) |

## Phase 1 - Domain

1. **Buy `maintained.house`** at **Porkbun** (chosen for at-cost
   pricing, free WHOIS privacy/SSL, and a DNS panel that comfortably
   handles the MX/SPF/DKIM records below). Expect roughly USD 30/year
   and confirm the renewal price before purchase.
   *Verify*: Porkbun dashboard shows the domain active.
2. **Choose the DNS host** - Porkbun's own DNS is fine. You will add
   records in phases 3, 5, and 6; nothing to create yet.
   *Verify*: you can open the DNS record editor.

## Phase 2 - Contact mailbox

1. **Set up a mailbox** for `contact@maintained.house` via **Migadu**
   (flat annual fee, unlimited mailboxes/aliases - preferred since this
   project only needs a handful of addresses) or **Zoho Mail Lite**
   (~$1/user/month) as a fallback. Add the MX/SPF/DKIM records the
   provider prescribes at Porkbun. Forward-only setups (registrar
   forwarding, ImprovMX) remain acceptable if you don't need to send
   from the address.
   *Verify*: send a test mail from another account and confirm it
   arrives; reply and confirm the reply sends (or note reply-from
   limitations - forward-only is acceptable for launch).

## Phase 3 - Resend transactional email (reminders)

The reminders feature (011-reminders) sends daily digest emails via
Resend (`Email:Provider=Resend` in
`backend/src/HomeMaintenance.Infrastructure/Email/EmailExtensions.cs`).
It fails fast at API startup if the required settings below are
missing. Send from a dedicated subdomain rather than the bare domain so
its SPF/DKIM/deliverability reputation stays isolated from the contact
mailbox in phase 2 - a bounce-heavy digest should never risk contact@'s
inbox reputation.

1. **Add the domain in Resend**: dashboard -> Domains -> Add Domain ->
   `notify.maintained.house` (not the bare `maintained.house`).
2. **Verify it**: add the SPF, DKIM, and DMARC TXT records Resend
   generates to the `notify` subdomain at Porkbun's DNS editor. Since
   this is a distinct subdomain zone, no merging with the contact
   mailbox's SPF record is needed.
   *Verify*: Resend's Domains page shows `notify.maintained.house` as
   Verified.
3. **Set the app settings** (App Service -> Configuration, alongside
   phase 5's settings): `Email__Provider=Resend`,
   `Email__Resend__ApiKey` (from Resend -> API Keys), and
   `Email__FromAddress=reminders@notify.maintained.house`. Confirm
   `Frontend__BaseUrl=https://maintained.house` is also set - the
   digest service skips its run entirely if this is blank.
   *Verify*: `ReminderDigestService`'s startup log line shows no
   fail-fast error; trigger a digest pass (or wait for the daily run)
   and confirm a test owner with `RemindersEnabled=true` receives an
   email from `reminders@notify.maintained.house`.

## Phase 4 - MongoDB Atlas

1. **Create a cluster**: [cloud.mongodb.com](https://cloud.mongodb.com)
   -> new project `maintained-house-prod` -> create a cluster in an EU
   region (the free/flex tier is fine to start). TLS is on by default.
2. **Database user**: Database Access -> add a user with a strong
   generated password, readWrite on the app database.
3. **Network access**: Network Access -> allow the App Service outbound
   IPs (Azure portal -> App Service -> Networking -> Outbound
   addresses). Avoid 0.0.0.0/0 except as a temporary bootstrap.
4. **Connection string**: Database -> Connect -> Drivers -> copy the
   `mongodb+srv://` URI with the user credentials.
5. **Backups**: enable the tier's backup/snapshot option; on the free
   tier note its absence and treat the paid tier as a fast follow.
   *Verify*: `mongosh "<uri>" --eval "db.runCommand({ ping: 1 })"`
   returns `ok: 1` from your machine (temporarily allow your IP).

## Phase 5 - Azure App Service (production API)

1. **App Service**: reuse the staging app or (recommended) create a
   second app, e.g. `maintained-house-prod`, so staging keeps working.
   If you create a new app, either:
   - repoint the deploy workflow: update the repo variable
     `AZURE_BACKEND_APP_NAME` and the `AZURE_BACKEND_PUBLISH_PROFILE`
     secret with the new app's publish profile (README Deployment
     section documents both), which makes main deploy to prod; or
   - clone `.github/workflows/deploy-backend.yml` into a second
     workflow with its own variable/secret pair if you want staging
     AND prod pipelines side by side.
2. **App settings** (portal -> Configuration -> Application settings):
   apply the production variant of the backend matrix in
   [oidc-setup.md](oidc-setup.md#backend-configuration):
   `ASPNETCORE_ENVIRONMENT=Production`, `Auth__Google__ClientId` (the
   production client from phase 7), `MongoDB__ConnectionString` (the
   Atlas URI), `Cors__AllowedOrigins=https://maintained.house`.
   `Auth__UseStub` stays unset - production startup refuses the stub.
3. **Audit-log sink**: The audit trail is persisted to the MongoDB
   Atlas database (`auditEvents` collection). Set
   `AuditLog__Provider=Mongo` in App Service Configuration (the
   `appsettings.Staging.json` file already does this for Staging; add
   the same setting for Production). The collection and its indexes
   (`actor+timestamp`, `target+timestamp`, `eventType`,
   `correlationId`) are created automatically by `MongoIndexInitializer`
   on first startup. No additional resources or runbook phases are
   required: Atlas is already a launch prerequisite, so the audit
   trail inherits its TLS and backup posture.
4. **Custom domain**: portal -> Custom domains -> add
   `api.maintained.house`; create the CNAME (and TXT validation
   record) at your DNS host; enable the free App Service managed
   certificate.
   *Verify*: `curl https://api.maintained.house/health` returns 200
   with a valid certificate, and `/` returns the service banner.

## Phase 6 - Vercel (production frontend)

1. **Domain**: Vercel project -> Settings -> Domains -> add
   `maintained.house`; create the A/CNAME records Vercel prescribes.
2. **Environment variables** (Production scope): the production
   variant of the frontend matrix in
   [oidc-setup.md](oidc-setup.md#frontend-configuration):
   `NEXTAUTH_URL=https://maintained.house`, fresh `NEXTAUTH_SECRET`,
   `GOOGLE_CLIENT_ID` / `GOOGLE_CLIENT_SECRET` (phase 7),
   `API_BASE_URL=https://api.maintained.house`,
   `NEXT_PUBLIC_API_URL=https://api.maintained.house`,
   `AUTH_TRUST_HOST=true`. `NEXTAUTH_DEV_STUB` must NOT be set.
3. **Redeploy** so the env vars take effect.
   *Verify*: `https://maintained.house` serves the landing page with a
   valid certificate; the amber dev-stub box does NOT appear on
   `/signin`.

## Phase 7 - Google OAuth (production client + publishing)

Follow [oidc-setup.md](oidc-setup.md#step-by-step-google-cloud-console-setup)
with the production values:

1. **Client**: project `maintained-house-prod` (or the production
   client in the existing project); authorized redirect URI
   `https://maintained.house/api/auth/callback/google`; authorized
   JavaScript origin `https://maintained.house`.
2. **Consent screen**: app name "Maintained House", user support and
   developer contact emails, homepage `https://maintained.house`,
   privacy policy `https://maintained.house/privacy`, terms
   `https://maintained.house/terms`, authorized domain
   `maintained.house`. Scopes stay at the basic defaults (openid,
   email, profile) - these do not trigger the restricted-scope audit.
3. **Publish**: OAuth consent screen -> Publishing status -> "In
   production". With only basic scopes this is self-serve; Google may
   still show an unverified-app interstitial until its lightweight
   brand verification completes - functional sign-in is not blocked.
   *Verify*: consent screen shows "In production"; the client's
   redirect URI list contains exactly the production callback.

## Phase 8 - Launch verification

Run through in an incognito window:

1. `https://maintained.house` -> landing page renders (no redirect).
2. `/privacy` and `/terms` load without signing in.
3. Sign in with a Google account that is NOT the developer's and was
   never added as a test user -> consent -> lands on the dashboard.
4. Dashboard connection widget shows Connected (proves frontend ->
   API -> Mongo end to end).
5. Create a property, a job with steps, tick and complete -> data
   persists across reload.
6. Deep link: open `/properties/<id>` from a signed-out window ->
   sign-in -> returned to the property page.
7. System menu: identity shown, User guide opens, sign out returns to
   the sign-in page and `/properties` redirects back to sign-in.
8. `https://api.maintained.house/health` returns 200.

## Phase 9 - Rollback notes

- **Stop new sign-ins**: set the OAuth consent screen back to
  "Testing" - existing sessions keep working, new users are blocked.
- **Frontend**: Vercel -> Deployments -> promote the previous
  deployment; or detach the domain to fall back to the vercel.app URL.
- **Backend**: App Service -> Deployment Center -> redeploy a previous
  run of the deploy workflow (or stop the app for a hard outage).
- **DNS**: keep TTLs at 300s or lower until launch has settled so any
  record change propagates quickly.
