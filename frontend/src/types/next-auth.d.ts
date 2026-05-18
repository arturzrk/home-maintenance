import type { DefaultSession } from "next-auth";

declare module "next-auth" {
  interface Session {
    idToken?: string;
    error?: "RefreshAccessTokenError";
    user?: DefaultSession["user"];
  }
}

declare module "next-auth/jwt" {
  interface JWT {
    idToken?: string;
    accessToken?: string;
    refreshToken?: string;
    /** Unix seconds at which idToken expires (best-effort from the JWT `exp` claim). */
    expiresAt?: number;
    error?: "RefreshAccessTokenError";
  }
}
