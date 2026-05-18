import { redirect } from "next/navigation";
import { auth } from "@/lib/auth";

/**
 * Resolves the current session inside a Server Component or Server
 * Action. Redirects to /signin if the caller is not authenticated, or
 * if NextAuth was unable to refresh the Google id_token (the previous
 * refresh_token was revoked / expired, the user revoked access, etc).
 * The returned object is guaranteed to carry an idToken usable as a
 * bearer for the backend API.
 */
export async function requireSession() {
  const session = await auth();
  if (!session?.idToken || session.error === "RefreshAccessTokenError") {
    redirect("/signin");
  }
  return session as typeof session & { idToken: string };
}
