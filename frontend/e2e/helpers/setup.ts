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
    }),
  });
  if (!resp.ok) throw new Error(`createJob failed: ${resp.status}`);
  const data = (await resp.json()) as { id: string };
  return data.id;
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
