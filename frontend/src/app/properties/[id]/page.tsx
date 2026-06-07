import Link from "next/link";
import { notFound } from "next/navigation";
import {
  ApiError,
  jobs as jobsApi,
  jobDefinitions as jobDefinitionsApi,
  properties as propertiesApi,
  type ScheduleDefinitionDto,
} from "@/lib/api-client";
import { requireSession } from "@/lib/session";
import { CreateJobForm } from "@/components/create-job-form";
import { CreateJobDefinitionForm } from "@/components/create-job-definition-form";
import { JobCard } from "@/components/job-card";
import { PropertyHeader } from "@/components/property-header";

function scheduleLabel(s: ScheduleDefinitionDto): string {
  const unit = s.multiplier === 1 ? s.unit.toLowerCase() : `${s.multiplier} ${s.unit.toLowerCase()}s`;
  const from = new Date(s.startDate).toLocaleDateString("en-GB", { month: "short", year: "numeric" });
  return `Every ${unit} from ${from}`;
}

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

  const [{ jobs }, definitions] = await Promise.all([
    jobsApi.list(session.idToken, { propertyId: id }),
    jobDefinitionsApi.list(session.idToken, { propertyId: id }),
  ]);

  return (
    <div className="mx-auto max-w-3xl space-y-6">
      <PropertyHeader property={property} />

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

      <section className="space-y-2">
        <h2 className="text-sm font-semibold text-gray-700">Recurring jobs</h2>
        {definitions.length === 0 ? (
          <p className="text-sm text-gray-500">No recurring job definitions yet.</p>
        ) : (
          <ul className="space-y-2">
            {definitions.map((d) => (
              <li key={d.id}>
                <div className="rounded-md border border-gray-200 bg-white px-4 py-3 shadow-sm">
                  <Link
                    href={`/job-definitions/${d.id}`}
                    className="text-base font-medium text-gray-900 hover:underline"
                  >
                    {d.name}
                  </Link>
                  <p className="mt-0.5 text-xs text-gray-500">{scheduleLabel(d.schedule)}</p>
                </div>
              </li>
            ))}
          </ul>
        )}
        <CreateJobDefinitionForm propertyId={property.id} />
      </section>
    </div>
  );
}
