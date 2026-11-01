import Link from "next/link";
import { unstable_rethrow } from "next/navigation";
import {
  checkHealth,
  getApiInfo,
  jobs as jobsApi,
  properties as propertiesApi,
} from "@/lib/api-client";
import { auth } from "@/lib/auth";
import { ConnectionStatus } from "@/components/connection-status";
import { DueJobsOverview } from "@/components/due-jobs-overview";
import { LandingPage } from "@/components/landing-page";
import { groupJobsByDueDate, type DueJobBuckets } from "@/lib/due-jobs";
import type { ApiInfo } from "@/types/api";

export const dynamic = "force-dynamic";

/**
 * "/" is public: anonymous visitors get the Maintained House landing
 * page, a usable session gets the dashboard with due/overdue jobs
 * across every property.
 */
export default async function DashboardPage() {
  // Same bar as middleware/requireSession: a session without a usable
  // idToken (missing, or refresh failed) is treated as signed out, so
  // degraded sessions land on the public page instead of a dashboard
  // shell whose every next click bounces to /signin.
  const rawSession = await auth();
  const signedIn =
    !!rawSession?.idToken && rawSession.error !== "RefreshAccessTokenError";
  if (!signedIn) {
    return <LandingPage />;
  }
  const session = rawSession as typeof rawSession & { idToken: string };

  let healthy = false;
  let apiInfo: ApiInfo | null = null;

  try {
    [healthy, apiInfo] = await Promise.all([checkHealth(), getApiInfo()]);
  } catch {
    // API is not reachable - healthy stays false, apiInfo stays null.
  }

  let dueJobBuckets: DueJobBuckets = { overdue: [], dueThisWeek: [], comingUp: [] };
  try {
    const [{ jobs }, { properties }] = await Promise.all([
      jobsApi.list(session.idToken, { status: "Active" }),
      propertiesApi.list(session.idToken),
    ]);
    dueJobBuckets = groupJobsByDueDate(jobs, properties, new Date());
  } catch (err) {
    // apiFetch redirects to /signin (throwing Next's internal
    // NEXT_REDIRECT) when the idToken is rejected; that must propagate,
    // not be swallowed into an empty dashboard. Any other failure
    // (network error, 5xx) degrades to the empty buckets above.
    unstable_rethrow(err);
  }

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-bold tracking-tight text-gray-900">
          Welcome back
        </h1>
      </div>

      <DueJobsOverview buckets={dueJobBuckets} />

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
