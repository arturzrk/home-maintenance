import NextAuth, { type Session } from "next-auth";
import Google from "next-auth/providers/google";
import Credentials from "next-auth/providers/credentials";
import { refreshGoogleIdToken, readJwtExp } from "@/lib/google-token";

// In Development a developer can sign in without going through Google by
// using the dev-stub credentials provider, which mints an idToken of the
// shape "dev-<sub>" that the backend's DevStubAuthenticationHandler
// accepts. Set NEXTAUTH_DEV_STUB=true in .env.local. Production MUST NOT
// enable this.
const enableDevStub = process.env.NEXTAUTH_DEV_STUB === "true";

// Refresh when the token has less than 60s of life remaining, so we never
// hand a freshly-expired token to the backend.
const REFRESH_WINDOW_SECONDS = 60;

const googleProvider = Google({
  clientId: process.env.GOOGLE_CLIENT_ID,
  clientSecret: process.env.GOOGLE_CLIENT_SECRET,
  // access_type=offline + prompt=consent is the canonical way to ask
  // Google for a refresh_token. Without these we get an id_token that
  // expires in ~1h and no way to mint a new one without bouncing the
  // user back through the OAuth flow.
  authorization: {
    params: {
      access_type: "offline",
      prompt: "consent",
    },
  },
});

const devStubProvider = Credentials({
  id: "dev-stub",
  name: "Dev stub (no real auth)",
  credentials: {
    sub: {
      label: "OwnerId (any string)",
      type: "text",
      placeholder: "alice",
    },
  },
  async authorize(input) {
    const sub = String(input?.sub ?? "").trim();
    if (!sub) return null;
    return {
      id: sub,
      name: sub,
      email: `${sub}@dev.local`,
      idToken: `dev-${sub}`,
    };
  },
});

export const { handlers, auth, signIn, signOut } = NextAuth({
  providers: enableDevStub ? [googleProvider, devStubProvider] : [googleProvider],
  pages: {
    signIn: "/signin",
  },
  callbacks: {
    async jwt({ token, account, user }) {
      // First sign-in: capture everything we'll need to refresh later.
      if (account) {
        if (typeof account.id_token === "string") {
          token.idToken = account.id_token;
          token.expiresAt = readJwtExp(account.id_token) ?? account.expires_at;
        } else if (user && "idToken" in user && typeof user.idToken === "string") {
          // Dev stub: no expiry, no refresh, just carry the token.
          token.idToken = user.idToken;
          token.expiresAt = undefined;
        }
        if (typeof account.access_token === "string") {
          token.accessToken = account.access_token;
        }
        if (typeof account.refresh_token === "string") {
          token.refreshToken = account.refresh_token;
        }
        token.error = undefined;
        return token;
      }

      // Subsequent calls: refresh the Google id_token if it's about to expire.
      const expiresAt = typeof token.expiresAt === "number" ? token.expiresAt : undefined;
      const refreshToken = typeof token.refreshToken === "string" ? token.refreshToken : undefined;
      if (!expiresAt || !refreshToken) return token;
      const nowSeconds = Math.floor(Date.now() / 1000);
      if (expiresAt - nowSeconds > REFRESH_WINDOW_SECONDS) return token;

      const clientId = process.env.GOOGLE_CLIENT_ID;
      const clientSecret = process.env.GOOGLE_CLIENT_SECRET;
      if (!clientId || !clientSecret) {
        token.error = "RefreshAccessTokenError";
        return token;
      }

      try {
        const refreshed = await refreshGoogleIdToken({
          clientId,
          clientSecret,
          refreshToken,
        });
        token.idToken = refreshed.idToken;
        token.expiresAt = refreshed.expiresAt;
        if (refreshed.accessToken) token.accessToken = refreshed.accessToken;
        if (refreshed.refreshToken) token.refreshToken = refreshed.refreshToken;
        token.error = undefined;
      } catch {
        // Don't blow up the session. Leave the (stale) token in place but
        // flag the error so requireSession can bounce the user to /signin.
        token.error = "RefreshAccessTokenError";
      }
      return token;
    },
    async session({ session, token }) {
      const s = session as Session & {
        idToken?: string;
        error?: "RefreshAccessTokenError";
      };
      s.idToken = typeof token.idToken === "string" ? token.idToken : undefined;
      s.error = token.error === "RefreshAccessTokenError" ? token.error : undefined;
      return s;
    },
  },
});
