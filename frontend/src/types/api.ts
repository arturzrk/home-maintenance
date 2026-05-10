// ─── Shared API response types ────────────────────────────────────────────────
// These mirror the shapes returned by the backend.
// Extend this file as new endpoints are added.

export type HealthStatus = "Healthy" | "Degraded" | "Unhealthy";

export interface ApiInfo {
  service: string;
  version: string;
  status: string;
}
