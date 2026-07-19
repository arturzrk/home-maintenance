import Link from "next/link";
import { checkHealth, getApiInfo } from "@/lib/api-client";
import { auth } from "@/lib/auth";
import { ConnectionStatus } from "@/components/connection-status";
import { LandingPage } from "@/components/landing-page";
import type { ApiInfo } from "@/types/api";

export const dynamic = "force-dynamic";

/**
 * "/" is public: anonymous visitors get the Maintained House landing
 * page, a usable session gets the dashboard. Richer dashboard content
 * (due jobs, per-property summaries) arrives in future features.
 */
export default async function DashboardPage() {
  // Same bar as middleware/requireSession: a session without a usable
  // idToken (missing, or refresh failed) is treated as signed out, so
  // degraded sessions land on the public page instead of a dashboard
  // shell whose every next click bounces to /signin.
  const session = await auth();
  const signedIn =
    !!session?.idToken && session.error !== "RefreshAccessTokenError";
  if (!signedIn) {
    return <LandingPage />;
  }

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
