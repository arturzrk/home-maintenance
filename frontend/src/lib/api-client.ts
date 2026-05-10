import type { ApiInfo } from "@/types/api";

const BASE_URL = process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5000";

// ─── Generic fetch wrapper ─────────────────────────────────────────────────

async function apiFetch<T>(
  path: string,
  options?: RequestInit
): Promise<T> {
  const response = await fetch(`${BASE_URL}${path}`, {
    headers: { "Content-Type": "application/json" },
    ...options,
  });

  if (!response.ok) {
    throw new Error(
      `API error: ${response.status} ${response.statusText} — ${path}`
    );
  }

  return response.json() as Promise<T>;
}

// ─── System endpoints ──────────────────────────────────────────────────────

export async function getApiInfo(): Promise<ApiInfo> {
  return apiFetch<ApiInfo>("/");
}

export async function checkHealth(): Promise<boolean> {
  try {
    const response = await fetch(`${BASE_URL}/health`);
    return response.ok;
  } catch {
    return false;
  }
}
