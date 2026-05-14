import NextAuth, { type Session } from "next-auth";
import Google from "next-auth/providers/google";
import Credentials from "next-auth/providers/credentials";

// In Development a developer can sign in without going through Google by
// using the dev-stub credentials provider, which mints an idToken of the
// shape "dev-<sub>" that the backend's DevStubAuthenticationHandler
// accepts. Set NEXTAUTH_DEV_STUB=true in .env.local. Production MUST NOT
// enable this.
const enableDevStub = process.env.NEXTAUTH_DEV_STUB === "true";

const googleProvider = Google({
  clientId: process.env.GOOGLE_CLIENT_ID,
  clientSecret: process.env.GOOGLE_CLIENT_SECRET,
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
      // Carried through into the JWT callback below.
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
      // First sign-in: capture the Google ID token (or the dev-stub one).
      if (account?.id_token) {
        token.idToken = account.id_token;
      } else if (user && "idToken" in user && typeof user.idToken === "string") {
        token.idToken = user.idToken;
      }
      return token;
    },
    async session({ session, token }) {
      // Expose the API bearer token to Server Components and Server Actions.
      (session as Session & { idToken?: string }).idToken =
        typeof token.idToken === "string" ? token.idToken : undefined;
      return session;
    },
  },
});
