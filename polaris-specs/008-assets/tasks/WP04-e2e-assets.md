---
work_package_id: WP04
title: 'E2E: assets suite'
lane: "for_review"
dependencies: ["WP03"]
base_branch: main
subtasks: [T017, T018, T019, T020, T021, T022, T023]
test_status: required
test_file: frontend/e2e/wp08-assets.spec.ts
domain: testing-specialist
---

# WP04 - E2E: assets suite

## Objective

`frontend/e2e/wp08-assets.spec.ts` with 6 tests covering the spec's user
scenarios end-to-end, plus a `createAssetViaApi` helper. Runs in the CI
e2e job with the rest of the suite.

## Context

- Helpers available: `uniqueUser`, `signInAs`, `createPropertyViaApi`,
  `createJobViaApi`, `createJobDefinitionViaApi`,
  `createAndCompleteJobViaApi`, `todayIso`.
- Locator conventions: ids on create-form inputs (`#asset-name`,
  `#job-asset`, `#jd-asset`); InlineEditableText button/textbox share
  ariaLabel ("Edit asset name"); role-based locators elsewhere.
- Exact UI strings come from WP03; verify against the implemented
  components before writing assertions (do not trust this prompt over
  the code).

## Subtasks

### T017 --- Helper

`createAssetViaApi(token, propertyId, body: { name, category?, notes? })
-> Promise<string>` -- POST /api/assets, returns id. Also expose
`setAssetObsoleteViaApi(token, assetId, isObsolete)` (PATCH) if the
obsolete test seeds state via API.

### T018 --- WP08-1: create asset via UI

Property page -> fill `#asset-name` ("Boiler") + `#asset-category`
("Heating") -> submit -> card appears in Assets section, input cleared.

### T019 --- WP08-2: asset detail + inline rename

Seed asset via API -> open `/assets/{id}` -> name/category visible ->
inline rename via "Edit asset name" button/textbox/Enter -> heading
updates.

### T020 --- WP08-3: job scoped to asset

Seed asset -> property page -> create job with `#job-asset` select set
to the asset -> job detail shows the asset link -> asset detail page
lists the job.

### T021 --- WP08-4: definition inheritance

Seed asset -> create definition with `#jd-asset` selected (startDate
today so inline generation runs) -> asset detail page lists the
definition AND a generated job.

### T022 --- WP08-5: obsolete lifecycle

Seed asset + job scoped to it -> mark obsolete on the asset detail page
-> property page card shows "Obsolete" badge -> `#job-asset` dropdown no
longer offers it -> existing job still shows the asset link ->
reactivate -> dropdown offers it again.

### T023 --- WP08-6: unscoped work unaffected

Create a job leaving the asset select at "No asset" -> job detail shows
no asset link (SC-04 guard).

## Test Strategy

- `uniqueUser()` + fresh property per test; assets/jobs seeded via API
  except where the scenario is the UI flow itself.
- 6 tests, one describe "WP08: Assets".

## Definition of Done

- [ ] `npx playwright test e2e/wp08-assets.spec.ts` -> 6/6 pass
- [ ] Full suite passes locally and in the CI e2e job (33 tests)
- [ ] Helper(s) added to e2e/helpers/setup.ts
- [ ] Each test isolated; no production code changes

## Risks

- **String drift from WP03**: verify every locator against the real
  components first (established practice).
- **Dropdown option matching**: select options should be asserted via
  `selectOption({ label: ... })` or value=assetId -- use value.

## Run Command

```bash
polaris implement WP04 --base WP03
```

## Activity Log

- 2026-07-13T06:56:19Z – unknown – lane=for_review – Implemented on branch 008-assets-WP04; PR #91
