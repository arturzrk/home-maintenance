---
work_package_id: WP03
title: 'Frontend: assets UI'
lane: "for_review"
dependencies: ["WP02"]
base_branch: main
subtasks: [T011, T012, T013, T014, T015, T016]
test_status: required
test_file: frontend/src/components/__tests__/asset-list.test.tsx
domain: frontend-craft
---

# WP03 - Frontend: assets UI

## Objective

All user-facing asset surfaces: Assets section on the property page,
asset detail page with the asset's work lists, obsolete toggle, and
optional asset dropdowns on the job/definition create forms.

## Context

- Patterns to mirror: property page sections + `create-job-form.tsx`
  (id-labelled inputs), `property-header.tsx`/`job-header.tsx`
  (InlineEditableText usage), `app/jobs/actions.ts` (server actions +
  revalidatePath), `app/job-definitions/[id]/page.tsx` (detail page
  fetching related lists server-side).
- InlineEditableText ariaLabel convention: "Edit asset name".
- Obsolete semantics: badge in lists, excluded from dropdowns, detail
  page fully functional, toggle reversible (FR-08).

## Subtasks

### T011 --- api-client

`AssetDto` type `{ id, propertyId, name, category, notes, isObsolete }`;
`assets` api: `create`, `list(propertyId)`, `get(id)`, `update(id,
body)`. Add `assetId?: string | null` to Job/JobDefinition types and
create bodies; `jobs.list`/`jobDefinitions.list` accept an `assetId`
filter param.

### T012 --- Server actions

`app/assets/actions.ts`: `createAsset(formData or args)` and
`updateAsset(id, body)` following `ActionResult` + `failureFrom` +
`revalidatePath` conventions (revalidate `/properties/{propertyId}` and
`/assets/{id}`).

### T013 --- Property page Assets section

`AssetList` component under the existing sections: heading "Assets",
empty state "No assets yet.", create form (name input
`#asset-name`, category input `#asset-category`, "Add asset" button),
cards linking to `/assets/{id}` showing name + category + "Obsolete"
badge when flagged (styling consistent with JobCard badges).

### T014 --- Asset detail page

`app/assets/[id]/page.tsx` (server component, force-dynamic like the
other detail pages): fetch asset, jobs (`assetId` filter), definitions
(`assetId` filter); 404 -> notFound(). "Back to property" link.
`AssetHeader` client island: InlineEditableText name (ariaLabel "Edit
asset name"), category + notes editing (InlineEditableText or a small
edit form -- match DefinitionHeader's collapsible pattern if a form),
obsolete toggle button ("Mark obsolete" / "Reactivate") with an
"Obsolete" badge when set. Sections: "Jobs" and "Recurring jobs" lists
reusing existing card/link presentation.

### T015 --- Create-form dropdowns + detail links

Property page server component fetches the property's assets once and
passes non-obsolete ones into `CreateJobForm` and
`CreateJobDefinitionForm`: optional `<select id="job-asset">` /
`<select id="jd-asset">` labelled "Asset (optional)", default "No
asset". Selected value goes into the create body. Job and definition
detail pages render a small link to `/assets/{assetId}` when scoped
(fetch asset name server-side or embed name in DTO? -- keep it simple:
`GET /api/assets/{id}` server-side on the detail page when assetId is
present).

### T016 --- Component tests

Jest tests: AssetList renders cards + empty state + obsolete badge;
create form submits trimmed name; AssetHeader obsolete toggle calls
updateAsset and reflects state; CreateJobForm renders/omits dropdown
based on assets prop and includes assetId in submission.

## Definition of Done

- [ ] `npm run lint`, `npm test`, `npm run build` green
- [ ] All five control-map flows demonstrable against the local stack
- [ ] Existing Playwright suites still pass locally (27/27)
- [ ] Dropdowns hide obsolete assets; lists show them with a badge

## Risks

- **Label collisions**: new selects use ids (`#job-asset`, `#jd-asset`)
  -- the property page already has multiple "Name" labels; ids keep e2e
  locators unambiguous (established lesson).
- **Server-passed assets**: dropdown data comes from the server
  component render; a just-created asset appears after revalidation --
  acceptable (same behavior as other create flows).

## Run Command

```bash
polaris implement WP03 --base WP02
```

## Activity Log

- 2026-07-12T18:36:38Z – unknown – lane=for_review – Implemented on branch 008-assets-WP03; PR #89
