import Link from "next/link";
import { checkHealth, getApiInfo } from "@/lib/api-client";
import { requireSession } from "@/lib/session";
import { ConnectionStatus } from "@/components/connection-status";
import type { ApiInfo } from "@/types/api";

export const dynamic = "force-dynamic";

/**
 * Dashboard - the signed-in landing page. Middleware guards "/", and
 * requireSession() here is defense in depth. Richer content (due jobs,
 * per-property summaries) arrives in future features.
 */
export default async function DashboardPage() {
  await requireSession();

  let healthy = false;
  let apiInfo: ApiInfo | null = null;

  try {
    [healthy, apiInfo] = await Promise.all([checkHealth(), getApiInfo()]);
  } catch {
    // API is not reachable - healthy stays false, apiInfo stays null.
  }

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-bold tracking-tight text-gray-900">
          Welcome back
        </h1>
        <p className="mt-1 text-sm text-gray-500">
          Track maintenance for your properties: recurring schedules,
          one-off jobs, and the assets they keep in shape.
        </p>
      </div>

      <Link
        id="dashboard-properties-link"
        href="/properties"
        className="block rounded-md border border-gray-200 bg-white px-4 py-4 shadow-sm transition hover:border-gray-300 hover:bg-gray-50"
      >
        <span className="text-base font-medium text-gray-900">
          My properties
        </span>
        <p className="mt-0.5 text-sm text-gray-500">
          View your properties and manage their jobs, schedules, and assets.
        </p>
      </Link>

      <ConnectionStatus healthy={healthy} apiInfo={apiInfo} />
    </div>
  );
}
