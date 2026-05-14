import type { DefaultSession } from "next-auth";

declare module "next-auth" {
  /**
   * Carries the bearer token the API expects on every protected request.
   * In Development this is the dev-stub value (\"dev-<sub>\"); in
   * Production it's the Google ID token.
   */
  interface Session {
    idToken?: string;
    user?: DefaultSession["user"];
  }
}

declare module "next-auth/jwt" {
  interface JWT {
    idToken?: string;
  }
}
