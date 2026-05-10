import { checkHealth, getApiInfo } from "@/lib/api-client";
import { ConnectionStatus } from "@/components/connection-status";
import type { ApiInfo } from "@/types/api";

/**
 * Dashboard — minimal working set entry point.
 * Server Component: data is fetched at request time on the server.
 * Features and widgets will be added here as the project grows.
 */
export default async function DashboardPage() {
  let healthy = false;
  let apiInfo: ApiInfo | null = null;

  try {
    [healthy, apiInfo] = await Promise.all([checkHealth(), getApiInfo()]);
  } catch {
    // API is not reachable — healthy stays false, apiInfo stays null.
  }

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-bold tracking-tight text-gray-900">
          Dashboard
        </h1>
        <p className="mt-1 text-sm text-gray-500">
          Minimal working set — backend + frontend connected.
        </p>
      </div>

      <ConnectionStatus healthy={healthy} apiInfo={apiInfo} />
    </div>
  );
}
