import { redirect } from "next/navigation";
import { auth } from "@/lib/auth";

/**
 * Resolves the current session inside a Server Component or Server
 * Action. Redirects to /signin if the caller is not authenticated. The
 * returned object is guaranteed to carry an idToken usable as a bearer
 * for the backend API.
 */
export async function requireSession() {
  const session = await auth();
  if (!session?.idToken) {
    redirect("/signin");
  }
  return session as typeof session & { idToken: string };
}
