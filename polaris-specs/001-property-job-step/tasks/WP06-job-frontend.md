---
work_package_id: WP06
lane: "doing"
dependencies: [WP04, WP05]
base_branch: main
base_commit: 0063b0c055c0cca3c0a46e2d12e1a15b74feac6b
created_at: '2026-05-17T11:39:31.433036+00:00'
subtasks: [T032, T033, T034, T035, T036, T037]
test_status: required
test_file: tests/e2e/WP06-wp06-job-frontend.e2e.js
domain: frontend-craft
shell_pid: "82041"
---

# WP06 - Job frontend

## Objective

Surface the Job API in the browser: create a Job under a Property,
view its checklist, tick steps off, and explicitly complete the Job.
Delivers US3, US4, and US5 end-to-end. Step mutation (add/remove/edit/
reorder) and Job rename UI are deferred to WP07.

## Inputs

- Spec: US3 (create Job), US4 (tick steps), US5 (complete Job).
- Contracts: `contracts/jobs.md`, `contracts/steps.md` (tick / untick
  subset).
- WP04 establishes the NextAuth session, BFF pattern, and Property
  list. WP05 ships the API endpoints.

## Subtasks

### T032 - Extend the typed API client

Edit `frontend/src/lib/api-client.ts`:

```ts
export type JobStatus = "Active" | "Completed";

export type StepDto = {
  id: string;
  order: number;
  description: string;
  isCompleted: boolean;
  completedAt: string | null;
};

export type JobSummary = {
  id: string;
  propertyId: string;
  name: string;
  dueDate: string | null;
  status: JobStatus;
  completedAt: string | null;
  stepCount: number;
  completedStepCount: number;
};

export type JobDetail = {
  id: string;
  propertyId: string;
  name: string;
  dueDate: string | null;
  status: JobStatus;
  completedAt: string | null;
  steps: StepDto[];
};

export type CreateJobInput = {
  propertyId: string;
  name: string;
  dueDate: string | null;
  steps: { description: string }[];
};

export const jobs = {
  list: (propertyId: string | null, idToken: string) =>
    call<{ jobs: JobSummary[] }>(
      `/api/jobs${propertyId ? `?propertyId=${propertyId}` : ""}`,
      { idToken }),
  get: (id: string, idToken: string) =>
    call<JobDetail>(`/api/jobs/${id}`, { idToken }),
  create: (input: CreateJobInput, idToken: string) =>
    call<JobDetail>("/api/jobs", { idToken, method: "POST", body: input }),
  complete: (id: string, idToken: string) =>
    call<JobDetail>(`/api/jobs/${id}/complete`, { idToken, method: "POST" }),
  tickStep: (jobId: string, stepId: string, idToken: string) =>
    call<StepDto>(`/api/jobs/${jobId}/steps/${stepId}/tick`, { idToken, method: "POST" }),
  untickStep: (jobId: string, stepId: string, idToken: string) =>
    call<StepDto>(`/api/jobs/${jobId}/steps/${stepId}/untick`, { idToken, method: "POST" }),
};
```

Add matching BFF routes under
`frontend/src/app/api/local/jobs/` and
`frontend/src/app/api/local/jobs/[id]/...` mirroring the pattern
from WP04 (each BFF route resolves the session, calls the API client,
returns the same response shape).

### T033 - /properties/[id] page enhancements

Replace
`frontend/src/app/properties/[id]/page.tsx` (or create if not yet
present) so it:

1. Loads the Property via `properties.get(id, session.idToken)`.
2. Loads its Jobs via `jobs.list(id, session.idToken)`.
3. Renders the Property header (name).
4. Renders a list of Jobs (`JobCard` server component) under the
   header.
5. Renders a `CreateJobForm` client component below the list.

`CreateJobForm` is a Client Component with:
- A name input (max 200 chars).
- An optional due date input (`<input type="date">`).
- A dynamic list of step description rows. "Add step" appends a
  blank row; row "Remove" button removes it.
- Submit button. On submit, POST to
  `/api/local/jobs` with the assembled body. On success,
  `router.refresh()`. On failure, surface the problem-details
  `detail`.

Use server actions or fetch + `router.refresh`, whichever is
cleaner; ensure the idToken stays out of the browser.

`JobCard.tsx` (Server Component): renders name, due date, and
"x of y steps complete" summary; links to `/jobs/{id}`.

### T034 - /jobs/[id] page

Create `frontend/src/app/jobs/[id]/page.tsx`:

```tsx
export default async function JobPage({ params }: { params: { id: string } }) {
  const session = await requireSession();
  const job = await jobs.get(params.id, session.idToken!);

  return (
    <main className="mx-auto max-w-3xl p-6 space-y-6">
      <header>
        <h1 className="text-2xl font-semibold">{job.name}</h1>
        <p className="text-sm text-gray-500">
          {job.dueDate ? `Due ${job.dueDate}` : "No due date"} - {job.status}
          {job.completedAt && ` (completed ${job.completedAt})`}
        </p>
      </header>

      <JobChecklist job={job} />
      <CompleteJobButton job={job} />
    </main>
  );
}
```

`JobChecklist` is a Client Component (it owns optimistic state for
ticks). It receives the full job, renders ordered `StepCheckbox`
rows, and calls `router.refresh()` after server responses.

`CompleteJobButton` is a Client Component (enable/disable logic
driven by checklist completeness).

### T035 [P] - StepCheckbox

`frontend/src/components/StepCheckbox.tsx`:

```tsx
"use client";
import { useState, useTransition } from "react";
import { useRouter } from "next/navigation";

export default function StepCheckbox({
  jobId,
  step,
  jobLocked,
}: {
  jobId: string;
  step: StepDto;
  jobLocked: boolean;
}) {
  const router = useRouter();
  const [optimistic, setOptimistic] = useState(step.isCompleted);
  const [pending, startTransition] = useTransition();

  async function toggle() {
    const next = !optimistic;
    setOptimistic(next);
    const route = next
      ? `/api/local/jobs/${jobId}/steps/${step.id}/tick`
      : `/api/local/jobs/${jobId}/steps/${step.id}/untick`;
    const res = await fetch(route, { method: "POST" });
    if (!res.ok) {
      setOptimistic(!next); // rollback
      // problem-details body could be surfaced via a toast; for Slice 1, log
      console.error(await res.text());
      return;
    }
    startTransition(() => router.refresh());
  }

  return (
    <label className="flex items-center gap-3 py-1">
      <input
        type="checkbox"
        checked={optimistic}
        onChange={toggle}
        disabled={jobLocked || pending}
      />
      <span className={optimistic ? "line-through text-gray-500" : ""}>
        {step.description}
      </span>
    </label>
  );
}
```

### T036 [P] - CompleteJobButton

`frontend/src/components/CompleteJobButton.tsx`:

```tsx
"use client";
import { useTransition } from "react";
import { useRouter } from "next/navigation";

export default function CompleteJobButton({
  job,
}: {
  job: JobDetail;
}) {
  const router = useRouter();
  const [pending, startTransition] = useTransition();

  const allDone = job.steps.length > 0 && job.steps.every(s => s.isCompleted);
  const completed = job.status === "Completed";

  async function submit() {
    const res = await fetch(`/api/local/jobs/${job.id}/complete`, { method: "POST" });
    if (res.ok) startTransition(() => router.refresh());
    else console.error(await res.text());
  }

  if (completed) {
    return (
      <p className="text-green-700">
        Completed on {new Date(job.completedAt!).toLocaleDateString()}
      </p>
    );
  }

  return (
    <button onClick={submit} disabled={!allDone || pending}
            className="rounded bg-blue-600 px-4 py-2 text-white disabled:opacity-50">
      Complete Job
    </button>
  );
}
```

### T037 - Jest tests

In `frontend/src/__tests__/jobs/`:

- `CreateJobForm.test.tsx`: render, add three step rows, fill name +
  steps, submit, mock fetch returning 201, assert `router.refresh`.
- `CreateJobForm.removeStep.test.tsx`: add rows, remove the middle one,
  submit, assert the request body lists the remaining steps in order.
- `StepCheckbox.optimistic.test.tsx`: render unchecked, click,
  observe checked state immediately, fetch resolved -> still checked.
- `StepCheckbox.rollback.test.tsx`: click, fetch rejects with 400,
  state reverts to unchecked.
- `CompleteJobButton.test.tsx`: disabled when any step incomplete;
  enabled when all complete; click triggers POST; success refreshes.
- `CompleteJobButton.completed.test.tsx`: when Job is Completed,
  button replaced with "Completed on ..." text.

Mock fetch via `global.fetch = jest.fn(...)`. Mock
`useRouter().refresh` with `jest.fn()`.

## Test strategy

- Component-level Jest tests for every interactive piece (checklist,
  button, form).
- Manual end-to-end through the quickstart walkthrough.
- The backend integration tests in WP05 cover the API; this WP relies
  on those.

## Definition of Done

- [ ] Signed-in user can navigate Property -> create Job ->
      `/jobs/{id}` -> tick steps -> Complete Job and see the read-only
      state.
- [ ] Optimistic UI: ticking a step shows instantly; on backend
      failure, state reverts.
- [ ] `Complete Job` is disabled until every step is ticked.
- [ ] Completed Job renders read-only checklist (no tick toggles).
- [ ] `npm run lint`, `npm run build`, `npm test` all green.
- [ ] CI green.

## Risks and non-obvious bits

- The BFF route pattern from WP04 is repeated here for every Job
  endpoint. Each BFF route is ~10 lines; resist the urge to abstract
  prematurely. Once a fifth analogous route appears in a later slice,
  extract a `forwardToApi(path, init, session)` helper.
- The page is a Server Component but most interactivity happens in
  Client islands. Pass the full `JobDetail` from the server boundary;
  do NOT refetch on the client.
- `router.refresh()` re-runs the Server Component which re-fetches
  the Job. This is the "source of truth" for the post-mutation state;
  optimistic state is only there to make the UI feel snappy.
- For Slice 1 we surface failures with `console.error`. Toasts are a
  follow-up - do not introduce a notification library here.

## Next command

```
polaris implement WP06 --base WP05
```
