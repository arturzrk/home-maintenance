"use server";

import { revalidatePath } from "next/cache";
import {
  ApiError,
  assets,
  type AssetDto,
  type UpdateAssetBody,
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

export async function createAsset(
  formData: FormData,
): Promise<ActionResult<{ id: string }>> {
  const propertyId = String(formData.get("propertyId") ?? "").trim();
  const name = String(formData.get("name") ?? "").trim();
  const category = String(formData.get("category") ?? "").trim();

  if (!propertyId) return { ok: false, error: "propertyId is required" };
  if (!name) return { ok: false, error: "Name is required" };
  if (name.length > 200)
    return { ok: false, error: "Name must be 200 characters or fewer" };
  if (category.length > 100)
    return { ok: false, error: "Category must be 100 characters or fewer" };

  const session = await requireSession();
  try {
    const created = await assets.create(
      { propertyId, name, category: category || null },
      session.idToken,
    );
    revalidatePath(`/properties/${propertyId}`);
    return { ok: true, value: { id: created.id } };
  } catch (err) {
    return failureFrom(err);
  }
}

export async function updateAsset(
  id: string,
  body: UpdateAssetBody,
): Promise<ActionResult<AssetDto>> {
  if (body.name !== undefined) {
    const trimmed = body.name.trim();
    if (!trimmed) return { ok: false, error: "Name is required" };
    if (trimmed.length > 200)
      return { ok: false, error: "Name must be 200 characters or fewer" };
    body.name = trimmed;
  }
  if (typeof body.category === "string" && body.category.length > 100)
    return { ok: false, error: "Category must be 100 characters or fewer" };
  if (typeof body.notes === "string" && body.notes.length > 2000)
    return { ok: false, error: "Notes must be 2000 characters or fewer" };

  const session = await requireSession();
  try {
    const updated = await assets.update(id, body, session.idToken);
    revalidatePath(`/assets/${id}`);
    revalidatePath(`/properties/${updated.propertyId}`);
    return { ok: true, value: updated };
  } catch (err) {
    return failureFrom(err);
  }
}
