"use server";

import { revalidatePath } from "next/cache";
import {
  ApiError,
  jobDefinitions,
  type JobDefinitionDto,
  type JobDetail,
  type UpdateJobDefinitionBody,
} from "@/lib/api-client";
import { requireSession } from "@/lib/session";

export type ActionResult<T = void> =
  | { ok: true; value: T }
  | { ok: false; error: string; code?: string };

function failureFrom(err: unknown): ActionResult<never> {
  if (err instanceof ApiError) {
    return { ok: false, error: err.message, code: err.code };
  }
  throw err;
}

export async function updateJobDefinition(
  id: string,
  body: UpdateJobDefinitionBody,
): Promise<ActionResult<JobDefinitionDto>> {
  const session = await requireSession();
  try {
    const updated = await jobDefinitions.update(id, body, session.idToken);
    revalidatePath(`/job-definitions/${id}`);
    return { ok: true, value: updated };
  } catch (err) {
    return failureFrom(err);
  }
}

export async function generateNextOccurrence(
  id: string,
): Promise<ActionResult<JobDetail>> {
  const session = await requireSession();
  try {
    const job = await jobDefinitions.generateNext(id, session.idToken);
    return { ok: true, value: job };
  } catch (err) {
    return failureFrom(err);
  }
}
