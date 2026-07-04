---
work_package_id: WP06
title: 'Frontend: JobDefinition detail page + Generate Next'
lane: "done"
dependencies: []
base_branch: main
base_commit: 65861326cc36bb89d148c50f6d94ccb5437cbaef
created_at: '2026-06-08T06:37:29.680518+00:00'
subtasks: [T033, T034, T035, T036, T037, T038]
assignee: ''
agent: "claude-sonnet-4-6"
shell_pid: "35360"
test_status: required
test_file: tests/e2e/WP06-frontend-jobdefinition-detail-page-generate-next.e2e.js
review_status: "approved"
reviewed_by: "Artur Żurek"
history:
- timestamp: '2026-05-29T00:00:00Z'
  lane: planned
  agent: system
  action: Prompt generated via /polaris.tasks
domain: frontend-craft
---

# WP06 - Frontend: JobDefinition detail page + Generate Next

## Objective

Deliver US3 (generated jobs visibility), US4 (manual generate-next), and US5
(edit definition) end-to-end in the browser. Builds directly on the API client
extensions and component patterns established in WP05.

## Inputs

- Spec: `polaris-specs/002-recurring-jobs/spec.md` (US3, US4, US5)
- Contracts: `polaris-specs/002-recurring-jobs/contracts/job-definitions.md`
- Existing: `frontend/src/app/jobs/[id]/page.tsx` (job detail page pattern)
- Existing: `frontend/src/app/jobs/[id]/components/` (step list, drag handle pattern)
- WP05 output: `api-client.ts` extended, `JobDefinitionDto` type available

## Subtasks

### T033 - Extend api-client with remaining JobDefinition methods

Modify `frontend/src/lib/api-client.ts`.

Add request types:
```typescript
export interface UpdateJobDefinitionBody {
  name?: string;
  schedule?: ScheduleDefinitionDto;
  addStepTemplates?: { description: string }[];
  removeStepTemplateIds?: string[];
  editStepTemplates?: { id: string; description: string }[];
  reorderStepTemplateIds?: string[];
}
```

Add methods:
```typescript
async getJobDefinition(id: string): Promise<JobDefinitionDto>
async updateJobDefinition(id: string, body: UpdateJobDefinitionBody): Promise<JobDefinitionDto>
async generateNextOccurrence(id: string): Promise<Job>  // returns JobDto (existing Job type)
```

Also add (for the definition's generated jobs list):
```typescript
// Extend existing listJobs to accept optional definitionId filter
async listJobs(propertyId?: string, definitionId?: string): Promise<JobSummaryDto[]>
```

Check if `listJobs` already accepts query params; if so, add `definitionId` as
an additional optional param. If the backend does not yet support this filter,
use the existing `listJobs` and filter client-side on `jobDefinitionId` for now
(document this as a known limitation in the component).

### T034 - JobDefinition detail page (Server Component)

Create `frontend/src/app/job-definitions/[id]/page.tsx`.

Server Component. Fetches on server side:
1. `getJobDefinition(id)` -> `definition`
2. `listJobs(undefined, id)` -> `generatedJobs` (jobs with this `jobDefinitionId`)

Renders:
- **Header section**: definition name (editable, from `T037`) + schedule summary (same helper from WP05).
- **Generated jobs list**: a read-only list of job names, due dates, and status linked to `/jobs/{id}`. If empty: "No jobs generated yet."
- **StepTemplateList** client island (T035).
- **GenerateNextButton** client island (T036).

Pass `definition` as a prop to client islands; do not re-fetch inside them.

### T035 - StepTemplateList client component

Create `frontend/src/app/job-definitions/[id]/components/StepTemplateList.tsx`.

Client Component. Props: `definitionId: string`, `stepTemplates: StepTemplateDto[]`.

Features:
- Display ordered step template descriptions.
- **Add step**: text input + "Add" button; calls `updateJobDefinition(id, { addStepTemplates: [{ description }] })`; then `router.refresh()`.
- **Remove step**: each step has a "Remove" button; calls `updateJobDefinition(id, { removeStepTemplateIds: [id] })`; then `router.refresh()`.
- **Edit description**: inline editing (click to edit, Enter/blur saves); calls `updateJobDefinition(id, { editStepTemplates: [{ id, description }] })`; then `router.refresh()`.
- **Reorder**: implement drag-and-drop using `dnd-kit` (check if already in the project from Slice 1 job steps; reuse the same drag handle pattern from `StepList.tsx` if it exists). On drop, call `updateJobDefinition(id, { reorderStepTemplateIds: [id1, id2, ...] })`.

Show a loading overlay or disable buttons while a mutation is in flight.

### T036 - GenerateNextButton client component

Create `frontend/src/app/job-definitions/[id]/components/GenerateNextButton.tsx`.

Client Component. Props: `definitionId: string`.

Behaviour:
- Renders a "Generate next" button.
- On click: disabled (loading state) + call `apiClient.generateNextOccurrence(definitionId)`.
- On 201 success: navigate to `/jobs/{newJob.id}` using `router.push`.
- On 400 with `code: "next_occurrence_already_exists"`: show inline error
  "The next occurrence is already scheduled." Do NOT navigate.
- On other errors: show generic inline error.

Disable the button while loading; re-enable on error so the user can retry.

### T037 - Inline name and schedule editing on detail header

Modify the header section of `frontend/src/app/job-definitions/[id]/page.tsx`
(or extract a `DefinitionHeader` client component).

**Name editing**: click the name text -> becomes an `<input>`; Enter or blur
calls `apiClient.updateJobDefinition(id, { name })` then `router.refresh()`.
Mirror the inline-edit pattern used for Job name on the Job detail page
(`/jobs/[id]`).

**Schedule editing**: a small "Edit schedule" link/button below the schedule
summary opens a form panel (collapsible or modal) with:
- Unit `<select>` (Day/Week/Month/Year)
- Multiplier `<input type="number" min="1">`
- Start date `<input type="date">`
- End date `<input type="date">` (optional)
Save button calls `apiClient.updateJobDefinition(id, { schedule: { unit, multiplier, startDate, endDate } })`,
then closes the panel and calls `router.refresh()`.

Cancel button closes the panel without saving.

### T038 - Jest tests for WP06 components

Create test files in `frontend/src/app/job-definitions/[id]/components/`:

**`StepTemplateList.test.tsx`**:
- `renders_StepTemplateDescriptions`: given 3 templates, all descriptions in DOM.
- `addStep_CallsApiClient_WithDescription`: fill input, click Add, assert `updateJobDefinition` called with `addStepTemplates`.
- `removeStep_CallsApiClient_WithId`: click Remove on a step, assert `updateJobDefinition` called with `removeStepTemplateIds`.
- `editStep_CallsApiClient_OnBlur`: click description to edit, change text, blur, assert `updateJobDefinition` called with `editStepTemplates`.

**`GenerateNextButton.test.tsx`**:
- `success_NavigatesToNewJob`: mock `generateNextOccurrence` to return `{ id: "job-1" }`;
  click button; assert `router.push` called with `/jobs/job-1`.
- `duplicateError_ShowsInlineError`: mock 400 with `code: "next_occurrence_already_exists"`;
  click button; assert error message in DOM; assert no navigation.
- `loadingState_DisablesButton`: button disabled while request in flight.

**`DefinitionHeader.test.tsx`** (if extracted):
- `nameEdit_CallsApiClientOnEnter`: type new name, press Enter, assert `updateJobDefinition` called.

## Test Strategy

- Jest + React Testing Library + `@testing-library/user-event`.
- Mock `api-client.ts` at module level.
- Mock `next/navigation`'s `useRouter` to assert `push`/`refresh` calls.
- Test components in isolation with controlled props.

## Definition of Done

- [ ] `frontend/src/app/job-definitions/[id]/page.tsx` renders definition data server-side.
- [ ] `StepTemplateList` supports add, remove, edit, and reorder.
- [ ] `GenerateNextButton` navigates on success and shows error on duplicate.
- [ ] Inline name and schedule editing work on the detail page.
- [ ] `api-client.ts` has `getJobDefinition`, `updateJobDefinition`, `generateNextOccurrence`.
- [ ] All Jest tests pass.
- [ ] TypeScript type checks pass (`tsc --noEmit`).
- [ ] Visiting `/job-definitions/{id}` in the browser shows the correct data.

## Risks

- **dnd-kit already present**: check `package.json` for `@dnd-kit/core` before
  installing. If absent, add it; if present, reuse the existing setup.
- **Client/Server boundary**: the detail page is a Server Component that passes
  initial data as props to client islands. Do not call `getJobDefinition` inside
  the client components (causes double-fetch and auth issues).
- **listJobs with definitionId filter**: if the backend does not support the
  `?definitionId=` query param in this WP, filter client-side. Add a TODO comment
  and a backend ticket note.

## Run Command

```bash
polaris implement WP06 --base WP05
```

## Activity Log

- 2026-06-08T11:57:58Z -- unknown -- lane=doing -- Moved to doing
- 2026-06-08T11:57:59Z -- unknown -- lane=testing -- Moved to testing
- 2026-06-08T11:58:01Z -- unknown -- lane=for_review -- 48/48 tests pass (12 suites)
- 2026-06-08T19:27:15Z -- claude-sonnet-4-6 -- shell_pid=35360 -- lane=done -- PR #40 merged
