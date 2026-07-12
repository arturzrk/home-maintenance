---
work_package_id: WP02
title: 'Backend: assetId on Jobs and JobDefinitions'
lane: "for_review"
dependencies: ["WP01"]
base_branch: main
subtasks: [T006, T007, T008, T009, T010]
test_status: required
test_file: backend/tests/HomeMaintenance.Integration.Tests/Assets/AssetScopedWorkTests.cs
domain: backend-logic
shell_pid: "8313"
---

# WP02 - Backend: assetId on Jobs and JobDefinitions

## Objective

Thread an optional `AssetId` through Job and JobDefinition: accepted and
validated at creation, inherited by generated jobs, exposed on DTOs,
filterable on list endpoints. Backward compatible -- existing documents
have no assetId and must keep working (spec SC-04).

## Context

- `Job.Create(...)` already takes an optional `jobDefinitionId` --
  `assetId` follows the identical pattern (Domain/Jobs/Job.cs:68).
- Generation paths that must inherit: `JobGenerationService`
  (inline 3-month horizon at definition create/update) and
  `GenerateNextOccurrenceHandler`.
- Validation source of truth: FR-06 (asset must exist, same owner AND
  same property) + FR-08 (must not be obsolete at creation time).
- List filters precedent: jobs list supports `propertyId`/`definitionId`
  query params; definitions list supports `propertyId`.

## Subtasks

### T006 --- Domain

Optional `AssetId` (string?) on Job and JobDefinition, set via Create
only (no setter -- re-assignment is out of scope). Update
`Job.Create`/`JobDefinition.Create` signatures and all call sites.

### T007 --- Creation validation

CreateJobCommand + CreateJobDefinitionCommand gain `AssetId?`. When
present, load the asset via IAssetRepository: not found or cross-owner
-> NotFoundError("Asset", id); different propertyId -> BusinessRuleError
("asset_property_mismatch", ...); IsObsolete -> BusinessRuleError
("asset_obsolete", "An obsolete asset cannot be assigned to new work.").
API request DTOs (`CreateJobRequest`, `CreateJobDefinitionApiRequest`)
gain `AssetId?`.

### T008 --- Generation inheritance

`JobGenerationService.GenerateForDefinition` and
`GenerateNextOccurrenceHandler` pass `definition.AssetId` into
`Job.Create` (FR-07).

### T009 --- DTOs + list filters

`JobSummaryDto`, `JobDetailDto`, `JobDefinitionDto` gain `AssetId?`
(nullable, defaulted -- keeps TestJson deserialization of old shapes
working). Jobs list endpoint + query accept `assetId` param;
JobDefinitions list likewise. Mongo repositories filter accordingly.

### T010 --- Tests

Unit: Job/JobDefinition Create carries assetId; generation inheritance.
Integration (`Assets/AssetScopedWorkTests.cs`): create job/definition
with valid assetId -> dto echoes it; assetId of another owner -> 404;
asset from a different property -> 400 asset_property_mismatch; obsolete
asset -> 400 asset_obsolete; definition with assetId + inline generation
-> generated jobs carry assetId; generate-next inherits; list filters
return only matching work; creating work WITHOUT assetId still works
(SC-04 regression guard).

## Definition of Done

- [ ] `dotnet test` green
- [ ] Full validation matrix covered by integration tests
- [ ] Existing e2e suites still pass locally (no observable change)
- [ ] DTO additions are nullable/optional -- no breaking API change

## Risks

- **Call-site sprawl**: Job.Create call sites include tests; update
  with named arguments to keep diffs readable.
- **Old documents**: Mongo docs without assetId must deserialize to
  null -- nullable field, no migration.

## Run Command

```bash
polaris implement WP02 --base WP01
```

## Activity Log

- 2026-07-12T17:12:48Z – unknown – lane=doing – Implementing assetId threading
- 2026-07-12T17:12:50Z – unknown – lane=testing – dotnet test + e2e running
- 2026-07-12T17:12:51Z – unknown – lane=for_review – 169 unit + 197 integration green (15 new); 27/27 e2e unaffected; PR #87
