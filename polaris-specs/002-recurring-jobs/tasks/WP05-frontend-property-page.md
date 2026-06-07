---
work_package_id: WP05
title: 'Frontend: JobDefinition create/list on Property page'
lane: "done"
dependencies: []
base_branch: main
base_commit: a0421c76c4b6dff6844ab93c29707231f1d80403
created_at: '2026-06-07T07:51:07.985659+00:00'
subtasks: [T027, T028, T029, T030, T031, T032]
assignee: ''
agent: "claude"
shell_pid: "70010"
test_status: required
test_file: tests/e2e/WP05-frontend-jobdefinition-createlist-on-property-page.e2e.js
review_status: ''
reviewed_by: ''
history:
- timestamp: '2026-05-29T00:00:00Z'
  lane: planned
  agent: system
  action: Prompt generated via /polaris.tasks
- timestamp: '2026-06-07T00:00:00Z'
  lane: done
  agent: system
  action: PR #37 merged; WP05 marked done.
domain: frontend-craft
---

# WP05 - Frontend: JobDefinition create/list on Property page

## Objective

Deliver US1 and US2 end-to-end in the browser: a homeowner can see their
recurring job definitions on the Property page, create a new one, and see
a visual "Recurring" badge on generated jobs. No definition detail page yet
(that is WP06).

## Inputs

- Spec: `polaris-specs/002-recurring-jobs/spec.md` (US1, US2)
- Contracts: `polaris-specs/002-recurring-jobs/contracts/job-definitions.md` (DTO shapes)
- Existing: `frontend/src/lib/api-client.ts` (API client patterns)
- Existing: `frontend/src/lib/types.ts` (or wherever Job type lives)
- Existing: `frontend/src/app/properties/[id]/page.tsx` (Property page)
- Existing: `frontend/src/app/properties/[id]/components/CreateJobForm.tsx` (form pattern)
- WP04 output: all backend endpoints live and tested

## Subtasks

### T027 - Extend api-client with JobDefinition methods

Modify `frontend/src/lib/api-client.ts` (or the equivalent API client file).

Add:
```typescript
export interface ScheduleDefinitionDto {
  unit: "Day" | "Week" | "Month" | "Year";
  multiplier: number;
  startDate: string;  // "yyyy-MM-dd"
  endDate?: string | null;
}

export interface StepTemplateDto {
  id: string;
  order: number;
  description: string;
}

export interface JobDefinitionDto {
  id: string;
  propertyId: string;
  name: string;
  schedule: ScheduleDefinitionDto;
  stepTemplates: StepTemplateDto[];
}

export interface CreateJobDefinitionBody {
  propertyId: string;
  name: string;
  schedule: ScheduleDefinitionDto;
  stepTemplates: { description: string }[];
}
```

Add methods following the existing auth-header pattern:
```typescript
async createJobDefinition(body: CreateJobDefinitionBody): Promise<JobDefinitionDto>
async listJobDefinitions(propertyId?: string): Promise<JobDefinitionDto[]>
```

Look at how `createJob` and `listJobs` are implemented; mirror the same pattern
for base URL, auth header attachment, error handling, and fetch options.

### T028 - Add jobDefinitionId to frontend Job type

Modify `frontend/src/lib/types.ts` (or wherever the `Job` TypeScript type is defined).

Add `jobDefinitionId: string | null` to the `Job` interface/type. Update any
type assertions that spread job objects (`{ ...job }`) so TypeScript is satisfied.

This is a non-breaking change: all existing jobs without the field will have
`null` (the API already returns `null` for one-shot jobs).

### T029 - Add "Recurring jobs" section to Property page

Modify `frontend/src/app/properties/[id]/page.tsx` (Server Component).

Add a "Recurring jobs" section below the one-shot job list:
1. Fetch `listJobDefinitions(propertyId)` on the server side (alongside existing property/jobs fetches).
2. Render a list: each item shows the definition `name` + schedule summary string (e.g. "Every 3 months from Jun 2026").
3. Link each definition name to `/job-definitions/{id}` (detail page from WP06).
4. If the list is empty, show "No recurring job definitions yet."
5. Include an "Add recurring job" trigger that opens (or reveals) `CreateJobDefinitionForm`.

Schedule summary helper:
```typescript
function scheduleLabel(s: ScheduleDefinitionDto): string {
  const unit = s.multiplier === 1 ? s.unit.toLowerCase() : `${s.multiplier} ${s.unit.toLowerCase()}s`;
  const from = new Date(s.startDate).toLocaleDateString("en-GB", { month: "short", year: "numeric" });
  return `Every ${unit} from ${from}`;
}
```

### T030 - CreateJobDefinitionForm client component

Create `frontend/src/app/properties/[id]/components/CreateJobDefinitionForm.tsx`.

This is a Client Component (`"use client"`).

Fields:
- **Name**: text input, required, max 200 chars.
- **Schedule unit**: `<select>` with options Day, Week, Month, Year.
- **Multiplier**: number input, min 1, default 1.
- **Start date**: date picker (`<input type="date">`), required.
- **End date**: date picker, optional.
- **Steps**: dynamic list of text inputs; "Add step" button appends a row;
  each row has a "Remove" button. Mirror the step row pattern in `CreateJobForm.tsx`.

On submit:
1. Validate client-side (non-empty name, multiplier >= 1, startDate set).
2. Call `apiClient.createJobDefinition({ propertyId, name, schedule, stepTemplates })`.
3. On success: call `router.refresh()` (to reload the server component) and reset the form / close the panel.
4. On error: show an inline error message.

Show a loading state on the submit button while the request is in flight.

### T031 - "Recurring" badge on generated jobs in job list

Modify the job list rendering on the Property page (wherever individual job rows
are rendered in `frontend/src/app/properties/[id]/`).

When a job's `jobDefinitionId != null`, render a small badge/label alongside
the job name. Suggested: `<span className="badge-recurring">Recurring</span>` or
an icon with title="Recurring job". Keep it subtle - one-shot jobs are visually
unchanged.

If the Property page job list renders inside a separate component (e.g.
`JobList.tsx`), make the change there.

### T032 - Jest tests

Create or extend test files in `frontend/src/app/properties/[id]/components/`:

**`CreateJobDefinitionForm.test.tsx`**:
- `renders_AllFormFields`: name input, unit select, multiplier input, date inputs present.
- `submit_CallsApiClientWithCorrectPayload`: fill form, submit, assert
  `apiClient.createJobDefinition` called with expected body shape.
- `submit_EmptyName_ShowsValidationError`: prevent submission, show error.
- `addStep_AppendsStepRow`: click "Add step", assert new input appears.

**`JobList.test.tsx`** (or wherever job badge is rendered):
- `badge_Present_WhenJobDefinitionIdSet`: render job with `jobDefinitionId: "abc"`,
  assert badge element in DOM.
- `badge_Absent_WhenJobDefinitionIdNull`: render job with `jobDefinitionId: null`,
  assert no badge.

Mock `apiClient` using Jest's module mock or MSW (whichever pattern the project uses).

## Test Strategy

- Unit/component tests use Jest + React Testing Library.
- No E2E tests in this WP (E2E coverage is in WP07 if added).
- Mock `api-client.ts` at the module level to avoid real network calls.
- Assert on DOM state after user interactions using `userEvent` from `@testing-library/user-event`.

## Definition of Done

- [ ] `api-client.ts` extended with `createJobDefinition` and `listJobDefinitions`.
- [ ] `Job` type has `jobDefinitionId: string | null`.
- [ ] Property page shows "Recurring jobs" section with definitions list.
- [ ] `CreateJobDefinitionForm` renders and submits correctly.
- [ ] Generated jobs show "Recurring" badge.
- [ ] All Jest tests pass (`npm test` / `pnpm test`).
- [ ] TypeScript type checks pass (`tsc --noEmit`).
- [ ] No console errors in the browser when visiting the Property page.

## Risks

- **Server vs. Client Components**: `listJobDefinitions` must be called server-side
  in `page.tsx`. `CreateJobDefinitionForm` must be a Client Component (uses state,
  router). Do not mix them.
- **Date input format**: `<input type="date">` returns `"yyyy-MM-dd"` which matches
  the API contract. No conversion needed.
- **router.refresh()**: requires `useRouter` from `next/navigation` inside a Client
  Component. Confirm the Next.js version supports this pattern (Next.js 15.3 does).

## Run Command

```bash
polaris implement WP05 --base WP04
```

## Activity Log

- 2026-06-07T07:51:08Z – claude – shell_pid=70010 – lane=doing – Assigned agent via workflow command
