import type { Page } from "@playwright/test";

const API_BASE = process.env.API_BASE_URL ?? "http://localhost:5000";

/** Sign in via the dev stub provider (requires NEXTAUTH_DEV_STUB=true). */
export async function signInAs(page: Page, sub: string): Promise<void> {
  await page.goto("/signin");
  await page.getByLabel(/OwnerId/i).fill(sub);
  await page.getByRole("button", { name: /Sign in as dev user/i }).click();
  await page.waitForURL(/\/properties/);
}

/** Create a property directly via the backend API and return its id. */
export async function createPropertyViaApi(
  token: string,
  name: string,
): Promise<string> {
  const resp = await fetch(`${API_BASE}/api/properties`, {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
      Authorization: `Bearer ${token}`,
    },
    body: JSON.stringify({ name }),
  });
  if (!resp.ok) throw new Error(`createProperty failed: ${resp.status}`);
  const data = (await resp.json()) as { id: string };
  return data.id;
}

export interface CreateJobDefinitionBody {
  name: string;
  schedule: {
    unit: "Day" | "Week" | "Month" | "Year";
    multiplier: number;
    startDate: string;
    endDate?: string | null;
  };
  stepTemplates?: { description: string }[];
}

/** Create a job definition directly via the backend API and return its id. */
export async function createJobDefinitionViaApi(
  token: string,
  propertyId: string,
  body: CreateJobDefinitionBody,
): Promise<string> {
  const resp = await fetch(`${API_BASE}/api/job-definitions`, {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
      Authorization: `Bearer ${token}`,
    },
    body: JSON.stringify({ ...body, propertyId }),
  });
  if (!resp.ok) throw new Error(`createJobDefinition failed: ${resp.status}`);
  const data = (await resp.json()) as { id: string };
  return data.id;
}

/** Create a job directly via the backend API and return its id. */
export async function createJobViaApi(
  token: string,
  propertyId: string,
  name: string,
  steps: string[] = [],
  assetId?: string,
): Promise<string> {
  const resp = await fetch(`${API_BASE}/api/jobs`, {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
      Authorization: `Bearer ${token}`,
    },
    body: JSON.stringify({
      propertyId,
      name,
      dueDate: null,
      steps: steps.map((description) => ({ description })),
      assetId: assetId ?? null,
    }),
  });
  if (!resp.ok) throw new Error(`createJob failed: ${resp.status}`);
  const data = (await resp.json()) as { id: string };
  return data.id;
}

/** Create an asset directly via the backend API and return its id. */
export async function createAssetViaApi(
  token: string,
  propertyId: string,
  body: { name: string; category?: string; notes?: string },
): Promise<string> {
  const resp = await fetch(`${API_BASE}/api/assets`, {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
      Authorization: `Bearer ${token}`,
    },
    body: JSON.stringify({ ...body, propertyId }),
  });
  if (!resp.ok) throw new Error(`createAsset failed: ${resp.status}`);
  const data = (await resp.json()) as { id: string };
  return data.id;
}

/** Flip an asset's obsolete flag directly via the backend API. */
export async function setAssetObsoleteViaApi(
  token: string,
  assetId: string,
  isObsolete: boolean,
): Promise<void> {
  const resp = await fetch(`${API_BASE}/api/assets/${assetId}`, {
    method: "PATCH",
    headers: {
      "Content-Type": "application/json",
      Authorization: `Bearer ${token}`,
    },
    body: JSON.stringify({ isObsolete }),
  });
  if (!resp.ok) throw new Error(`setAssetObsolete failed: ${resp.status}`);
}

/** Create a job, tick all its steps, and complete it. Returns the job id. */
export async function createAndCompleteJobViaApi(
  token: string,
  propertyId: string,
  name: string,
  steps: string[],
): Promise<string> {
  if (steps.length === 0) {
    throw new Error(
      "createAndCompleteJobViaApi requires at least one step (a job cannot be completed until every step is ticked)",
    );
  }
  const headers = {
    "Content-Type": "application/json",
    Authorization: `Bearer ${token}`,
  };
  const createResp = await fetch(`${API_BASE}/api/jobs`, {
    method: "POST",
    headers,
    body: JSON.stringify({
      propertyId,
      name,
      dueDate: null,
      steps: steps.map((description) => ({ description })),
    }),
  });
  if (!createResp.ok) {
    throw new Error(`createJob failed: ${createResp.status}`);
  }
  const job = (await createResp.json()) as {
    id: string;
    steps: { id: string }[];
  };
  for (const step of job.steps) {
    const tick = await fetch(
      `${API_BASE}/api/jobs/${job.id}/steps/${step.id}/tick`,
      { method: "POST", headers },
    );
    if (!tick.ok) throw new Error(`tickStep failed: ${tick.status}`);
  }
  const complete = await fetch(`${API_BASE}/api/jobs/${job.id}/complete`, {
    method: "POST",
    headers,
  });
  if (!complete.ok) throw new Error(`completeJob failed: ${complete.status}`);
  return job.id;
}

/** Return a unique user sub + matching bearer token for a fully isolated test. */
export function uniqueUser(): { sub: string; token: string } {
  const sub = `e2e-${Date.now()}-${Math.random().toString(36).slice(2, 7)}`;
  return { sub, token: `dev-${sub}` };
}

/** Today as "yyyy-MM-dd". */
export function todayIso(): string {
  return new Date().toISOString().split("T")[0];
}
