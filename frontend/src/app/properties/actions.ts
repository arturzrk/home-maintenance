"use server";

import { revalidatePath } from "next/cache";
import {
  ApiError,
  properties,
  jobDefinitions,
  type CreateJobDefinitionBody,
  type JobDefinitionDto,
} from "@/lib/api-client";
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
    // Let NEXT_REDIRECT (thrown by api-client on 401) and any other
    // framework error bubble up so Next.js can handle it.
    throw err;
  }
}

export type ActionResultValue<T> =
  | { ok: true; value: T }
  | { ok: false; error: string };

export async function createJobDefinition(
  propertyId: string,
  body: Omit<CreateJobDefinitionBody, "propertyId">,
): Promise<ActionResultValue<JobDefinitionDto>> {
  if (!body.name.trim()) return { ok: false, error: "Name is required" };
  if (body.name.length > 200) return { ok: false, error: "Name must be 200 characters or fewer" };
  if (body.schedule.multiplier < 1) return { ok: false, error: "Multiplier must be at least 1" };
  if (!body.schedule.startDate) return { ok: false, error: "Start date is required" };
  for (const s of body.stepTemplates) {
    if (s.description.trim().length === 0) return { ok: false, error: "Step description cannot be empty" };
    if (s.description.length > 500) return { ok: false, error: "Step description must be 500 characters or fewer" };
  }

  const session = await requireSession();
  try {
    const created = await jobDefinitions.create({ propertyId, ...body }, session.idToken);
    revalidatePath(`/properties/${propertyId}`);
    return { ok: true, value: created };
  } catch (err) {
    if (err instanceof ApiError) return { ok: false, error: err.message };
    throw err;
  }
}

export async function renameProperty(
  id: string,
  name: string,
): Promise<ActionResult> {
  const trimmed = name.trim();
  if (!trimmed) return { ok: false, error: "Name is required" };
  if (trimmed.length > 100)
    return { ok: false, error: "Name must be 100 characters or fewer" };

  const session = await requireSession();
  try {
    await properties.rename(id, trimmed, session.idToken);
    revalidatePath(`/properties/${id}`);
    revalidatePath("/properties");
    return { ok: true };
  } catch (err) {
    if (err instanceof ApiError) return { ok: false, error: err.message };
    throw err;
  }
}
