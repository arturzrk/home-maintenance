import { notFound } from "next/navigation";
import {
  ApiError,
  jobs as jobsApi,
  properties as propertiesApi,
} from "@/lib/api-client";
import { requireSession } from "@/lib/session";
import { CreateJobForm } from "@/components/create-job-form";
import { JobCard } from "@/components/job-card";

export const dynamic = "force-dynamic";

export default async function PropertyDetailPage({
  params,
}: {
  params: Promise<{ id: string }>;
}) {
  const { id } = await params;
  const session = await requireSession();

  let property;
  try {
    property = await propertiesApi.get(id, session.idToken);
  } catch (err) {
    if (err instanceof ApiError && err.status === 404) {
      notFound();
    }
    throw err;
  }

  const { jobs } = await jobsApi.list(session.idToken, { propertyId: id });

  return (
    <div className="mx-auto max-w-3xl space-y-6">
      <header className="space-y-1">
        <p className="text-xs text-gray-500">Property</p>
        <h1 className="text-2xl font-bold tracking-tight">{property.name}</h1>
      </header>

      <CreateJobForm propertyId={property.id} />

      <section className="space-y-2">
        <h2 className="text-sm font-semibold text-gray-700">Jobs</h2>
        {jobs.length === 0 ? (
          <p className="text-sm text-gray-500">
            No jobs yet. Create one above.
          </p>
        ) : (
          <ul className="space-y-2">
            {jobs.map((j) => (
              <li key={j.id}>
                <JobCard job={j} />
              </li>
            ))}
          </ul>
        )}
      </section>
    </div>
  );
}
