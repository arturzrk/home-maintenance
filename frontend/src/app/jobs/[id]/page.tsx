import Link from "next/link";
import { notFound } from "next/navigation";
import { ApiError, jobs as jobsApi } from "@/lib/api-client";
import { requireSession } from "@/lib/session";
import { CompleteJobButton } from "@/components/complete-job-button";
import { JobChecklist } from "@/components/job-checklist";
import { JobHeader } from "@/components/job-header";

export const dynamic = "force-dynamic";

export default async function JobDetailPage({
  params,
}: {
  params: Promise<{ id: string }>;
}) {
  const { id } = await params;
  const session = await requireSession();

  let job;
  try {
    job = await jobsApi.get(id, session.idToken);
  } catch (err) {
    if (err instanceof ApiError && err.status === 404) {
      notFound();
    }
    throw err;
  }

  const completed = job.status === "Completed";

  return (
    <div className="mx-auto max-w-3xl space-y-6">
      <p className="text-xs text-gray-500">
        <Link href={`/properties/${job.propertyId}`} className="hover:underline">
          Back to property
        </Link>
      </p>

      <JobHeader job={job} />

      <section className="rounded-md border border-gray-200 bg-white p-4 shadow-sm">
        <h2 className="text-sm font-semibold text-gray-700">Checklist</h2>
        <div className="mt-2">
          <JobChecklist
            jobId={job.id}
            initialSteps={job.steps}
            jobLocked={completed}
          />
        </div>
      </section>

      <section>
        <CompleteJobButton job={job} />
      </section>
    </div>
  );
}
