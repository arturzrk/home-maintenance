# Control map --- 009-system-menu

## Flows

| # | Flow | Entry | Steps | Exit |
|---|------|-------|-------|------|
| 1 | Open system menu | Any page, signed in | Click trigger (shows identity) → menu opens | Menu visible; Escape/outside click closes |
| 2 | Navigate via menu | Menu open | Click "My properties" | `/properties`, menu closed |
| 3 | Open user guide | Menu open | Click "User guide" | `/user-manual/index.html` in new tab |
| 4 | View system info | Menu open | Read version + health line | (informational, no navigation) |
| 5 | Sign out | Menu open | Click "Sign out" (no confirm) | `/signin`; protected pages redirect to sign-in |
| 6 | Sign in → landing | `/signin`, no callbackUrl | Complete Google or dev-stub sign-in | Dashboard `/` |
| 7 | Sign in → deep link | Protected URL while signed out | Redirect to `/signin?callbackUrl=...` → sign in | Original URL |
| 8 | Brand home link | Any page | Click "Home Maintenance" brand | Dashboard `/` |
| 9 | Dashboard | `/`, signed in | View welcome copy + connection widget | "My properties" CTA → `/properties` |

## Shared Dependencies

| Dependency | Used by flows | Notes |
|------------|--------------|-------|
| Session (`auth()`) | 1, 5, 6, 7, 9 | Identity for trigger; gate for menu render (FR-11) |
| `getApiInfo()` / `checkHealth()` | 4, 9 | Same source as dashboard connection widget |
| Root layout header | 1--5, 8 | Only place the menu lives; server component + client menu island |
| Sign-in page form actions | 6, 7 | Default `redirectTo` changes `/properties` → `/` |
| Middleware matcher | 5, 7, 9 | `/` must join the protected routes |
| e2e `signInAs` helper | 6 | Currently waits for `/properties`; must wait for `/` |
