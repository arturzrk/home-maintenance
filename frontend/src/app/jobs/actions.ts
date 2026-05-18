"use server";

import { revalidatePath } from "next/cache";
import { ApiError, jobs, type CreateJobInput, type JobDetail } from "@/lib/api-client";
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

export async function createJob(formData: FormData): Promise<ActionResult<{ id: string }>> {
  const propertyId = String(formData.get("propertyId") ?? "").trim();
  const name = String(formData.get("name") ?? "").trim();
  const dueDateRaw = String(formData.get("dueDate") ?? "").trim();
  const stepLines = String(formData.get("steps") ?? "")
    .split("\n")
    .map((s) => s.trim())
    .filter((s) => s.length > 0);

  if (!propertyId) return { ok: false, error: "propertyId is required" };
  if (!name) return { ok: false, error: "Name is required" };
  if (name.length > 200) return { ok: false, error: "Name must be 200 characters or fewer" };
  for (const desc of stepLines) {
    if (desc.length > 500) {
      return { ok: false, error: "Each step description must be 500 characters or fewer" };
    }
  }

  const input: CreateJobInput = {
    propertyId,
    name,
    dueDate: dueDateRaw || null,
    steps: stepLines.map((description) => ({ description })),
  };

  const session = await requireSession();
  try {
    const created = await jobs.create(input, session.idToken);
    revalidatePath(`/properties/${propertyId}`);
    return { ok: true, value: { id: created.id } };
  } catch (err) {
    return failureFrom(err);
  }
}

export async function tickStep(jobId: string, stepId: string): Promise<ActionResult<JobDetail>> {
  const session = await requireSession();
  try {
    const updated = await jobs.tickStep(jobId, stepId, session.idToken);
    revalidatePath(`/jobs/${jobId}`);
    return { ok: true, value: updated };
  } catch (err) {
    return failureFrom(err);
  }
}

export async function untickStep(jobId: string, stepId: string): Promise<ActionResult<JobDetail>> {
  const session = await requireSession();
  try {
    const updated = await jobs.untickStep(jobId, stepId, session.idToken);
    revalidatePath(`/jobs/${jobId}`);
    return { ok: true, value: updated };
  } catch (err) {
    return failureFrom(err);
  }
}

export async function completeJob(jobId: string): Promise<ActionResult<JobDetail>> {
  const session = await requireSession();
  try {
    const updated = await jobs.complete(jobId, session.idToken);
    revalidatePath(`/jobs/${jobId}`);
    return { ok: true, value: updated };
  } catch (err) {
    return failureFrom(err);
  }
}

// ---- WP07 step mutation + job rename/due date ----

export async function addStep(
  jobId: string,
  description: string,
): Promise<ActionResult<JobDetail>> {
  const trimmed = description.trim();
  if (!trimmed) return { ok: false, error: "Description is required" };
  if (trimmed.length > 500)
    return { ok: false, error: "Description must be 500 characters or fewer" };

  const session = await requireSession();
  try {
    const updated = await jobs.addStep(jobId, trimmed, session.idToken);
    revalidatePath(`/jobs/${jobId}`);
    return { ok: true, value: updated };
  } catch (err) {
    return failureFrom(err);
  }
}

export async function removeStep(
  jobId: string,
  stepId: string,
): Promise<ActionResult<JobDetail>> {
  const session = await requireSession();
  try {
    const updated = await jobs.removeStep(jobId, stepId, session.idToken);
    revalidatePath(`/jobs/${jobId}`);
    return { ok: true, value: updated };
  } catch (err) {
    return failureFrom(err);
  }
}

export async function editStepDescription(
  jobId: string,
  stepId: string,
  description: string,
): Promise<ActionResult<JobDetail>> {
  const trimmed = description.trim();
  if (!trimmed) return { ok: false, error: "Description is required" };
  if (trimmed.length > 500)
    return { ok: false, error: "Description must be 500 characters or fewer" };

  const session = await requireSession();
  try {
    const updated = await jobs.editStepDescription(
      jobId,
      stepId,
      trimmed,
      session.idToken,
    );
    revalidatePath(`/jobs/${jobId}`);
    return { ok: true, value: updated };
  } catch (err) {
    return failureFrom(err);
  }
}

export async function reorderSteps(
  jobId: string,
  orderedStepIds: string[],
): Promise<ActionResult<JobDetail>> {
  const session = await requireSession();
  try {
    const updated = await jobs.reorderSteps(jobId, orderedStepIds, session.idToken);
    revalidatePath(`/jobs/${jobId}`);
    return { ok: true, value: updated };
  } catch (err) {
    return failureFrom(err);
  }
}

export async function updateJob(
  jobId: string,
  patch: { name?: string; dueDate?: string | null },
): Promise<ActionResult<JobDetail>> {
  if (patch.name !== undefined) {
    const trimmed = patch.name.trim();
    if (!trimmed) return { ok: false, error: "Name is required" };
    if (trimmed.length > 200)
      return { ok: false, error: "Name must be 200 characters or fewer" };
    patch.name = trimmed;
  }

  const session = await requireSession();
  try {
    const updated = await jobs.update(jobId, patch, session.idToken);
    revalidatePath(`/jobs/${jobId}`);
    return { ok: true, value: updated };
  } catch (err) {
    return failureFrom(err);
  }
}
