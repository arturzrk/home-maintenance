/**
 * Helpers for working with Google OIDC ID tokens.
 *
 * Kept separate from auth.ts so the refresh logic can be unit-tested
 * without instantiating the full NextAuth pipeline.
 */

/**
 * Decodes the `exp` claim from a JWT without verifying the signature.
 * Returns Unix seconds, or undefined if the token is malformed.
 */
export function readJwtExp(token: string): number | undefined {
  const parts = token.split(".");
  if (parts.length < 2) return undefined;
  try {
    const payload = parts[1].replace(/-/g, "+").replace(/_/g, "/");
    const padded = payload.padEnd(payload.length + ((4 - (payload.length % 4)) % 4), "=");
    const decoded = JSON.parse(
      typeof atob === "function"
        ? atob(padded)
        : Buffer.from(padded, "base64").toString("utf8"),
    );
    return typeof decoded.exp === "number" ? decoded.exp : undefined;
  } catch {
    return undefined;
  }
}

export interface GoogleTokenRefreshResult {
  idToken: string;
  accessToken?: string;
  refreshToken?: string;
  expiresAt: number;
}

/**
 * Calls Google's token endpoint with grant_type=refresh_token.
 *
 * Throws on any non-2xx response or on missing fields in the payload.
 * The caller is responsible for catching and translating the error into
 * a "user must re-sign-in" signal.
 */
export async function refreshGoogleIdToken(args: {
  clientId: string;
  clientSecret: string;
  refreshToken: string;
  fetchImpl?: typeof fetch;
}): Promise<GoogleTokenRefreshResult> {
  const fetchImpl = args.fetchImpl ?? fetch;
  const res = await fetchImpl("https://oauth2.googleapis.com/token", {
    method: "POST",
    headers: { "Content-Type": "application/x-www-form-urlencoded" },
    body: new URLSearchParams({
      client_id: args.clientId,
      client_secret: args.clientSecret,
      grant_type: "refresh_token",
      refresh_token: args.refreshToken,
    }),
    cache: "no-store",
  });

  const payload = (await res.json().catch(() => ({}))) as {
    id_token?: string;
    access_token?: string;
    refresh_token?: string;
    expires_in?: number;
    error?: string;
    error_description?: string;
  };

  if (!res.ok) {
    throw new Error(
      payload.error_description ?? payload.error ?? `Google token refresh failed (HTTP ${res.status})`,
    );
  }

  if (!payload.id_token) {
    throw new Error("Google token refresh returned no id_token");
  }

  const fromClaim = readJwtExp(payload.id_token);
  const fromExpiresIn =
    typeof payload.expires_in === "number"
      ? Math.floor(Date.now() / 1000) + payload.expires_in
      : undefined;
  const expiresAt = fromClaim ?? fromExpiresIn;
  if (!expiresAt) {
    throw new Error("Google token refresh did not provide an expiry");
  }

  return {
    idToken: payload.id_token,
    accessToken: payload.access_token,
    // Google may or may not rotate refresh tokens; only overwrite when one is returned.
    refreshToken: payload.refresh_token,
    expiresAt,
  };
}
