# Google OIDC setup (staging and production)

This is the runbook for wiring real Google OIDC into a non-dev
environment. In Development the [local stub](#how-local-dev-differs)
bypasses Google entirely; everywhere else (`ASPNETCORE_ENVIRONMENT`
in {`Staging`, `Production`}, and any `Auth:UseStub=false`) the
backend validates real Google ID tokens.

## Why a separate Google client per environment

Each environment gets its own Google OAuth 2.0 Client ID. Reasons:

- Redirect URIs are pinned per client. Staging redirects to
  `https://staging.maintained.house/api/auth/callback/google`,
  production to `https://maintained.house/api/auth/callback/google`.
- Audience validation. The backend's `Auth:Google:ClientId` is also
  the audience claim it expects on incoming tokens. A staging token
  is rejected by production (and vice versa), which is what we want.
- Blast radius. Rotating credentials in one environment can't break
  another.

## Step-by-step Google Cloud Console setup

Do this once per environment.

1. **Project**. Open the
   [Google Cloud Console](https://console.cloud.google.com/) and pick
   an existing project or create a fresh one (e.g.
   `maintained-house-staging`). The same Google account can own
   multiple projects.

2. **OAuth consent screen**. Under "APIs & Services" -> "OAuth consent
   screen", configure once per project:
   - User type: **External** (so any Google account can sign in).
   - App name, support email, developer contact email - whatever
     you want users to see at consent.
   - Scopes: leave defaults (`openid`, `profile`, `email`). Slice 1
     only needs `openid` + `email`; the others are forward-looking.
   - Test users: in non-published state, only added emails can sign
     in. For staging, add the email addresses of everyone who will
     test. For production, publish the app (Google reviews if you
     request restricted scopes; `openid + email` is no-review).

3. **Credentials**. Under "APIs & Services" -> "Credentials":
   - Click "Create Credentials" -> "OAuth client ID".
   - Application type: **Web application**.
   - Name: `home-maintenance-staging` (or `-production`).
   - Authorized JavaScript origins:
     - `https://staging.maintained.house`
   - Authorized redirect URIs:
     - `https://staging.maintained.house/api/auth/callback/google`
   - Click Create. Note the Client ID and Client Secret - the
     secret is shown once.

4. **Save the values** in your password manager:
   - `GOOGLE_CLIENT_ID` (sample shape: `123...apps.googleusercontent.com`)
   - `GOOGLE_CLIENT_SECRET`

## Backend configuration

The backend reads its auth config from two places, merged in order:

1. `backend/src/HomeMaintenance.API/appsettings.Staging.json`
   (committed; contains placeholders + comments).
2. Environment variables, which override anything in the JSON files.
   Use `Section__Key` syntax for nested config (e.g.
   `Auth__Google__ClientId`).

Required environment variables for a staging deployment:

| Variable | Value |
|---|---|
| `ASPNETCORE_ENVIRONMENT` | `Staging` |
| `Auth__Google__ClientId` | The staging Google OAuth Client ID |
| `MongoDB__ConnectionString` | Staging Mongo URI (TLS) |
| `Cors__AllowedOrigins` | `https://staging.maintained.house` |

`Auth__UseStub` MUST be unset (or `false`). The startup assertion in
`AuthenticationExtensions.AddAppAuthentication` throws if you try to
enable the stub outside the Development environment.

The Client Secret is **not** needed by the backend - the backend only
validates incoming ID tokens, it doesn't initiate the OAuth flow. The
secret lives only on the frontend.

## Frontend configuration

NextAuth handles the OAuth dance. Required env vars in the staging
deployment:

| Variable | Value |
|---|---|
| `NEXTAUTH_URL` | `https://staging.maintained.house` |
| `NEXTAUTH_SECRET` | A fresh 32-byte base64 string per env: `openssl rand -base64 32` |
| `GOOGLE_CLIENT_ID` | Staging Client ID (same value the backend uses) |
| `GOOGLE_CLIENT_SECRET` | Staging Client Secret |
| `API_BASE_URL` | Staging backend URL (server-side fetch) |
| `NEXT_PUBLIC_API_URL` | Same as above for the public dashboard probe |
| `NEXTAUTH_DEV_STUB` | **unset** (anything truthy enables the stub) |

Set these in the deployment platform (Vercel project settings -> Environment Variables for the staging branch, or the equivalent in your hosting tool). Never commit them.

## Verification checklist

Before declaring staging "open":

1. **Backend startup**. The API logs `Authority=https://accounts.google.com`
   and `Audience=<client-id>...` at startup. If you see
   `Auth:Google:ClientId is required when Auth:UseStub is false`,
   the env var isn't reaching the process.

2. **Frontend startup**. Sign-in page renders without the amber
   dev-stub form. If the form appears, `NEXTAUTH_DEV_STUB` is still
   truthy in the deployment env vars.

3. **End-to-end sign-in**. From an incognito window:
   - Visit `https://staging.maintained.house/properties`.
   - Middleware redirects to `/signin`.
   - Click "Sign in with Google".
   - Google consent page shows the staging app name and the test
     user is permitted.
   - On redirect, `/properties` renders with the empty state.

4. **Token claim sanity** (CLI). Open Chrome DevTools -> Application
   -> Cookies -> copy `next-auth.session-token`. Decode at
   [jwt.io](https://jwt.io) (without sharing it). The `aud` claim
   MUST equal the staging Client ID, `iss` MUST be
   `https://accounts.google.com`, and `exp` is ~1 hour out.

5. **API audience match**. Hit any protected endpoint from a Server
   Component (the frontend will already do this). If you see 401
   responses with `code: "unauthorized"` and the user is signed in,
   the most likely cause is `Auth__Google__ClientId` on the backend
   not matching the frontend's `GOOGLE_CLIENT_ID`.

## ID token refresh

Google ID tokens live ~1 hour. NextAuth caches whatever was issued at
sign-in and does not refresh on its own, so any backend call made more
than an hour after sign-in would be rejected with 401.

The frontend handles this with two cooperating pieces:

- `lib/auth.ts` sets `access_type=offline` + `prompt=consent` on the
  Google authorization request so a `refresh_token` is issued, then
  in the `jwt` callback it calls Google's token endpoint to mint a
  new `id_token` whenever the cached one is within 60s of expiry. On
  refresh failure it sets `session.error = "RefreshAccessTokenError"`.
- `lib/api-client.ts` detects 401 responses to authenticated calls
  and `redirect()`s to `/signin`, so anything that slips past the
  refresh (revoked credentials, deleted Google account, rotated
  refresh_token gone wrong) becomes a graceful sign-in bounce rather
  than a 500 page.

Operational notes:

- The consent screen now appears on every fresh sign-in (because of
  `prompt=consent`). This is the price of getting a refresh_token.
- Existing sessions issued before this change has no refresh_token,
  so the next time their id_token expires the api-client 401 handler
  bounces them to `/signin`. That's a one-time cost.
- Nothing on the GCP side needs to change: `access_type=offline` is
  a runtime parameter, not a client setting.

## Common errors

| Symptom | Cause |
|---|---|
| Backend startup throws `Auth:Google:ClientId is required` | Env var missing; check the deployment platform's variable injection. |
| Sign-in redirects to Google but Google says "redirect_uri_mismatch" | The authorized redirect URI in the GCP Console doesn't exactly match `{NEXTAUTH_URL}/api/auth/callback/google`. URI matching is byte-exact. |
| Signed-in user gets 401 on every API request | Audience mismatch (backend Client ID != frontend Client ID), or token expired and refresh isn't wired. |
| `Auth:UseStub MUST NOT be enabled outside the Development environment` | `Auth__UseStub=true` was set in a non-Development environment; remove it. |
| Cross-owner request returns 404 even though the user is correct | Working as designed - cross-owner is always 404 to prevent enumeration. Check `audit-trail/property-job-step.jsonl` for the `authz.denied` event. |

## How local dev differs

Local dev uses the **dev stub** for both backend and frontend:

- Backend reads `Auth:UseStub=true` from `appsettings.Development.json`
  and registers `DevStubAuthenticationHandler`, which accepts
  `Authorization: Bearer dev-<sub>`.
- Frontend reads `NEXTAUTH_DEV_STUB=true` from `.env.local` and shows
  a credentials form on `/signin` that mints an idToken of shape
  `dev-<sub>` matching the backend.

This means a developer can run the full sign-in -> API -> audit flow
without any GCP setup. Production startup refuses to enable the stub
(`AuthenticationExtensions.AddAppAuthentication` throws on misconfig).

## Rotation

Rotate the Client Secret periodically. The process:

1. In the GCP Console, click "Reset secret" on the OAuth client.
2. Update `GOOGLE_CLIENT_SECRET` in the deployment platform.
3. Redeploy the frontend. The backend doesn't use the secret so it
   needs no change.
4. Existing sessions remain valid; users won't be signed out (the
   secret is only used to negotiate new sessions). If you want to
   force re-sign-in, rotate `NEXTAUTH_SECRET` simultaneously.

## Going to production

Same checklist, with these adjustments:

- Use a separate Google OAuth Client (`home-maintenance-production`).
- Publish the consent screen so non-test-users can sign in.
- The production audit-log sink MUST be a managed service; the
  ephemeral container filesystem is not acceptable for the
  constitution's append-only retention requirement.
- Add a Content Security Policy header allowing
  `https://accounts.google.com` for the sign-in iframe.
- Run the `Stub_In_Production` integration test pattern against the
  deployed binary one more time as a smoke (it's already in the unit
  suite; this is belt-and-braces).
