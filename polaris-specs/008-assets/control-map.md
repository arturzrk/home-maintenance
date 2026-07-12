# Control Map -- 008-assets

## Flows

| # | Flow | Entry | Screens/Sections |
|---|------|-------|------------------|
| 1 | Create/list assets | Property detail page | New "Assets" section: create form (name, category), asset cards |
| 2 | View/edit asset | Click asset card | Asset detail page `/assets/{id}`: inline name, category/notes edit, obsolete toggle, Jobs list, Recurring definitions list |
| 3 | Scope job to asset | Property page "Create job" form | New optional asset dropdown (active assets only) |
| 4 | Scope definition to asset | Property page "Create recurring job" form | New optional asset dropdown (active assets only) |
| 5 | Obsolete lifecycle | Asset detail page | Toggle sets/clears isObsolete; property page card shows "Obsolete" badge |

## Shared Dependencies

| Dependency | Used by flows | Notes |
|------------|---------------|-------|
| Asset list for a property | 1, 3, 4 | Dropdowns filter to isObsolete=false; section lists all |
| `assetId` on Job/JobDefinition DTOs | 2, 3, 4 | Optional everywhere; generation inherits (FR-07) |
| InlineEditableText | 2 | Same component/aria-label pattern as property/job/definition names |
| Ownership guard | all | Cross-owner -> 404, matching existing aggregates |
