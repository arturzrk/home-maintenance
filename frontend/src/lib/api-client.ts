import { redirect } from "next/navigation";
import type { ApiInfo } from "@/types/api";

// Server-side base URL (Server Components, Server Actions, Route Handlers).
// The browser bundle never sees this value.
const SERVER_BASE_URL = process.env.API_BASE_URL ?? "http://localhost:5000";

// Public base URL for the few endpoints the browser hits directly
// (currently: /health and / for the dashboard status widget).
const PUBLIC_BASE_URL =
  process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5000";

// ---------------------------------------------------------------------------
// Errors
// ---------------------------------------------------------------------------

export class ApiError extends Error {
  constructor(
    public readonly code: string,
    public readonly status: number,
    message: string,
  ) {
    super(message);
    this.name = "ApiError";
  }
}

// ---------------------------------------------------------------------------
// Internal fetch wrapper
// ---------------------------------------------------------------------------

interface FetchOptions {
  /** Bearer token forwarded to the API (server-only callers supply this). */
  idToken?: string;
  method?: "GET" | "POST" | "PATCH" | "PUT" | "DELETE";
  body?: unknown;
  /** Override the base URL (defaults to the server-side one). */
  baseUrl?: string;
  signal?: AbortSignal;
}

async function apiFetch<T>(path: string, opts: FetchOptions = {}): Promise<T> {
  const base = opts.baseUrl ?? SERVER_BASE_URL;
  const headers: Record<string, string> = {};
  if (opts.body !== undefined) headers["Content-Type"] = "application/json";
  if (opts.idToken) headers.Authorization = `Bearer ${opts.idToken}`;

  const res = await fetch(`${base}${path}`, {
    method: opts.method ?? "GET",
    headers,
    body: opts.body !== undefined ? JSON.stringify(opts.body) : undefined,
    cache: "no-store",
    signal: opts.signal,
  });

  if (res.status === 204) return undefined as T;

  if (!res.ok) {
    const problem = (await res.json().catch(() => ({}))) as {
      code?: string;
      detail?: string;
      title?: string;
    };
    // 401 with a bearer token = the token was rejected (expired,
    // audience mismatch, revoked). Bounce to /signin so NextAuth can
    // mint a fresh one. redirect() throws NEXT_REDIRECT, which Next.js
    // converts into a real redirect in Server Components / Server
    // Actions / Route Handlers - callers MUST let it propagate.
    if (res.status === 401 && opts.idToken) {
      redirect("/signin");
    }
    throw new ApiError(
      problem.code ?? "error",
      res.status,
      problem.detail ?? problem.title ?? res.statusText,
    );
  }

  return (await res.json()) as T;
}

// ---------------------------------------------------------------------------
// System endpoints (public, called from the dashboard)
// ---------------------------------------------------------------------------

export async function getApiInfo(): Promise<ApiInfo> {
  return apiFetch<ApiInfo>("/", { baseUrl: PUBLIC_BASE_URL });
}

export async function checkHealth(): Promise<boolean> {
  try {
    const response = await fetch(`${PUBLIC_BASE_URL}/health`, {
      cache: "no-store",
    });
    return response.ok;
  } catch {
    return false;
  }
}

// ---------------------------------------------------------------------------
// Properties (WP03 backend)
// Callable from Server Components / Server Actions / Route Handlers.
// Never call from a Client Component directly: the idToken would leak
// to the browser bundle.
// ---------------------------------------------------------------------------

export interface Property {
  id: string;
  name: string;
}

export interface PropertyList {
  properties: Property[];
}

export const properties = {
  list: (idToken: string) =>
    apiFetch<PropertyList>("/api/properties", { idToken }),

  get: (id: string, idToken: string) =>
    apiFetch<Property>(`/api/properties/${id}`, { idToken }),

  create: (name: string, idToken: string) =>
    apiFetch<Property>("/api/properties", {
      method: "POST",
      body: { name },
      idToken,
    }),

  rename: (id: string, name: string, idToken: string) =>
    apiFetch<Property>(`/api/properties/${id}`, {
      method: "PATCH",
      body: { name },
      idToken,
    }),
};

// ---------------------------------------------------------------------------
// Assets (008 backend)
// Server-only.
// ---------------------------------------------------------------------------

export interface AssetDto {
  id: string;
  propertyId: string;
  name: string;
  category: string | null;
  notes: string | null;
  isObsolete: boolean;
}

export interface CreateAssetBody {
  propertyId: string;
  name: string;
  category?: string | null;
  notes?: string | null;
}

export interface UpdateAssetBody {
  name?: string;
  category?: string | null;
  notes?: string | null;
  isObsolete?: boolean;
}

export const assets = {
  list: (propertyId: string, idToken: string) =>
    apiFetch<AssetDto[]>(
      `/api/assets?propertyId=${encodeURIComponent(propertyId)}`,
      { idToken },
    ),

  get: (id: string, idToken: string) =>
    apiFetch<AssetDto>(`/api/assets/${id}`, { idToken }),

  create: (body: CreateAssetBody, idToken: string) =>
    apiFetch<AssetDto>("/api/assets", { method: "POST", body, idToken }),

  update: (id: string, body: UpdateAssetBody, idToken: string) =>
    apiFetch<AssetDto>(`/api/assets/${id}`, {
      method: "PATCH",
      body,
      idToken,
    }),
};

// ---------------------------------------------------------------------------
// Jobs (WP05 backend)
// Server-only.
// ---------------------------------------------------------------------------

export type JobStatus = "Active" | "Completed";

export interface StepDto {
  id: string;
  order: number;
  description: string;
  isCompleted: boolean;
  completedAt: string | null;
}

export interface JobSummary {
  id: string;
  propertyId: string;
  name: string;
  dueDate: string | null;
  status: JobStatus;
  completedAt: string | null;
  stepCount: number;
  completedStepCount: number;
  jobDefinitionId: string | null;
  assetId: string | null;
}

export interface JobDetail {
  id: string;
  propertyId: string;
  name: string;
  dueDate: string | null;
  status: JobStatus;
  completedAt: string | null;
  steps: StepDto[];
  jobDefinitionId: string | null;
  assetId: string | null;
}

export interface JobList {
  jobs: JobSummary[];
}

export interface CreateJobInput {
  propertyId: string;
  name: string;
  dueDate: string | null;
  steps: { description: string }[];
  assetId?: string | null;
}

export const jobs = {
  list: (idToken: string, filters?: { propertyId?: string; status?: JobStatus; definitionId?: string; assetId?: string }) => {
    const params = new URLSearchParams();
    if (filters?.propertyId) params.set("propertyId", filters.propertyId);
    if (filters?.status) params.set("status", filters.status);
    if (filters?.definitionId) params.set("definitionId", filters.definitionId);
    if (filters?.assetId) params.set("assetId", filters.assetId);
    const qs = params.toString();
    return apiFetch<JobList>(`/api/jobs${qs ? "?" + qs : ""}`, { idToken });
  },

  get: (id: string, idToken: string) =>
    apiFetch<JobDetail>(`/api/jobs/${id}`, { idToken }),

  create: (input: CreateJobInput, idToken: string) =>
    apiFetch<JobDetail>("/api/jobs", { method: "POST", body: input, idToken }),

  complete: (id: string, idToken: string) =>
    apiFetch<JobDetail>(`/api/jobs/${id}/complete`, { method: "POST", idToken }),

  tickStep: (jobId: string, stepId: string, idToken: string) =>
    apiFetch<JobDetail>(`/api/jobs/${jobId}/steps/${stepId}/tick`, {
      method: "POST",
      idToken,
    }),

  untickStep: (jobId: string, stepId: string, idToken: string) =>
    apiFetch<JobDetail>(`/api/jobs/${jobId}/steps/${stepId}/untick`, {
      method: "POST",
      idToken,
    }),

  // WP07: step mutation + job rename/due date
  addStep: (jobId: string, description: string, idToken: string) =>
    apiFetch<JobDetail>(`/api/jobs/${jobId}/steps`, {
      method: "POST",
      body: { description },
      idToken,
    }),

  removeStep: (jobId: string, stepId: string, idToken: string) =>
    apiFetch<JobDetail>(`/api/jobs/${jobId}/steps/${stepId}`, {
      method: "DELETE",
      idToken,
    }),

  editStepDescription: (jobId: string, stepId: string, description: string, idToken: string) =>
    apiFetch<JobDetail>(`/api/jobs/${jobId}/steps/${stepId}`, {
      method: "PATCH",
      body: { description },
      idToken,
    }),

  reorderSteps: (jobId: string, orderedStepIds: string[], idToken: string) =>
    apiFetch<JobDetail>(`/api/jobs/${jobId}/steps/order`, {
      method: "PUT",
      body: { orderedStepIds },
      idToken,
    }),

  update: (
    id: string,
    body: { name?: string; dueDate?: string | null },
    idToken: string,
  ) =>
    apiFetch<JobDetail>(`/api/jobs/${id}`, {
      method: "PATCH",
      body,
      idToken,
    }),
};

// ---------------------------------------------------------------------------
// Job Definitions (WP05 frontend / WP04 backend)
// Server-only.
// ---------------------------------------------------------------------------

export interface ScheduleDefinitionDto {
  unit: "Day" | "Week" | "Month" | "Year";
  multiplier: number;
  startDate: string;
  endDate?: string | null;
}

export interface StepTemplateDto {
  id: string;
  order: number;
  description: string;
}

export interface JobDefinitionDto {
  id: string;
  propertyId: string;
  name: string;
  schedule: ScheduleDefinitionDto;
  stepTemplates: StepTemplateDto[];
  assetId: string | null;
}

export interface CreateJobDefinitionBody {
  propertyId: string;
  name: string;
  schedule: ScheduleDefinitionDto;
  stepTemplates: { description: string }[];
  assetId?: string | null;
}

export interface UpdateJobDefinitionBody {
  name?: string;
  schedule?: ScheduleDefinitionDto;
  addStepTemplates?: { description: string }[];
  removeStepTemplateIds?: string[];
  editStepTemplates?: { id: string; description: string }[];
  reorderStepTemplateIds?: string[];
}

export const jobDefinitions = {
  list: (idToken: string, filters?: { propertyId?: string; assetId?: string }) => {
    const params = new URLSearchParams();
    if (filters?.propertyId) params.set("propertyId", filters.propertyId);
    if (filters?.assetId) params.set("assetId", filters.assetId);
    const qs = params.toString();
    return apiFetch<JobDefinitionDto[]>(`/api/job-definitions${qs ? "?" + qs : ""}`, { idToken });
  },

  get: (id: string, idToken: string) =>
    apiFetch<JobDefinitionDto>(`/api/job-definitions/${id}`, { idToken }),

  create: (body: CreateJobDefinitionBody, idToken: string) =>
    apiFetch<JobDefinitionDto>("/api/job-definitions", {
      method: "POST",
      body,
      idToken,
    }),

  update: (id: string, body: UpdateJobDefinitionBody, idToken: string) =>
    apiFetch<JobDefinitionDto>(`/api/job-definitions/${id}`, {
      method: "PATCH",
      body,
      idToken,
    }),

  generateNext: (id: string, idToken: string) =>
    apiFetch<JobDetail>(`/api/job-definitions/${id}/generate-next`, {
      method: "POST",
      body: {},
      idToken,
    }),
};
