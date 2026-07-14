"use server";

import { signOut } from "@/lib/auth";

/**
 * Ends the session and redirects to the sign-in page. No confirmation
 * dialog (app convention). The NEXT_REDIRECT thrown by signOut's
 * redirect propagates for Next.js to handle.
 */
export async function signOutAction(): Promise<void> {
  await signOut({ redirectTo: "/signin" });
}
