"use server";

import { revalidatePath } from "next/cache";
import {
  ApiError,
  notificationPreferences,
  type NotificationPreferencesDto,
} from "@/lib/api-client";
import { requireSession } from "@/lib/session";

export type ActionResult<T = void> =
  | { ok: true; value: T }
  | { ok: false; error: string; code?: string };

function failureFrom(err: unknown): ActionResult<never> {
  if (err instanceof ApiError) {
    return { ok: false, error: err.message, code: err.code };
  }
  // Let NEXT_REDIRECT (thrown by api-client on 401) and any other
  // framework error bubble up so Next.js can handle it.
  throw err;
}

export async function updateNotificationPreferences(
  remindersEnabled: boolean,
): Promise<ActionResult<NotificationPreferencesDto>> {
  const session = await requireSession();
  try {
    const updated = await notificationPreferences.update(
      remindersEnabled,
      session.idToken,
    );
    revalidatePath("/settings/notifications");
    return { ok: true, value: updated };
  } catch (err) {
    return failureFrom(err);
  }
}
