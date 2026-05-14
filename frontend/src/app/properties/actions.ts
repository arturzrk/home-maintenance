"use server";

import { revalidatePath } from "next/cache";
import { ApiError, properties } from "@/lib/api-client";
import { requireSession } from "@/lib/session";

export type ActionResult =
  | { ok: true }
  | { ok: false; error: string };

export async function createProperty(formData: FormData): Promise<ActionResult> {
  const name = String(formData.get("name") ?? "").trim();
  if (!name) {
    return { ok: false, error: "Name is required" };
  }
  if (name.length > 100) {
    return { ok: false, error: "Name must be 100 characters or fewer" };
  }

  const session = await requireSession();
  try {
    await properties.create(name, session.idToken);
    revalidatePath("/properties");
    return { ok: true };
  } catch (err) {
    if (err instanceof ApiError) {
      return { ok: false, error: err.message };
    }
    return { ok: false, error: "Failed to create property" };
  }
}
