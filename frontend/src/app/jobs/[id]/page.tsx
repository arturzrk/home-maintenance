import Link from "next/link";
import { notFound } from "next/navigation";
import { ApiError, jobs as jobsApi } from "@/lib/api-client";
import { requireSession } from "@/lib/session";
import { CompleteJobButton } from "@/components/complete-job-button";
import { StepCheckbox } from "@/components/step-checkbox";

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
      <header className="space-y-1">
        <p className="text-xs text-gray-500">
          <Link href={`/properties/${job.propertyId}`} className="hover:underline">
            Back to property
          </Link>
        </p>
        <h1 className="text-2xl font-bold tracking-tight">{job.name}</h1>
        <p className="text-sm text-gray-500">
          {job.dueDate ? `Due ${job.dueDate}` : "No due date"} - Status: {job.status}
        </p>
      </header>

      <section className="rounded-md border border-gray-200 bg-white p-4 shadow-sm">
        <h2 className="text-sm font-semibold text-gray-700">Checklist</h2>
        {job.steps.length === 0 ? (
          <p className="mt-2 text-sm text-gray-500">No steps on this job.</p>
        ) : (
          <ul className="mt-2 space-y-1">
            {job.steps.map((step) => (
              <StepCheckbox
                key={step.id}
                jobId={job.id}
                step={step}
                jobLocked={completed}
              />
            ))}
          </ul>
        )}
      </section>

      <section>
        <CompleteJobButton job={job} />
      </section>
    </div>
  );
}
