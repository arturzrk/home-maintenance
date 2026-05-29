# Requirements Checklist --- 002-recurring-jobs

## Testability

| Requirement | Testable? | Notes |
|---|---|---|
| FR-101 Create JobDefinition | ✅ | POST endpoint; unit + integration tests |
| FR-102 List JobDefinitions | ✅ | GET endpoint filtered by PropertyId |
| FR-103 Read single JobDefinition | ✅ | GET by id |
| FR-104 Edit name + step templates | ✅ | PATCH endpoint; same patterns as Slice 1 |
| FR-105 Change schedule | ✅ | PATCH schedule; verify future runs use new cadence |
| FR-107 ScheduleDefinition validation | ✅ | Unit test; multiplier ≥ 1, unit enum |
| FR-108 StartDate required | ✅ | DTO validation |
| FR-109 EndDate optional + after StartDate | ✅ | Unit test |
| FR-110 Scheduler runs ≤24h | ✅ | BackgroundService timer assertion |
| FR-111 Generate occurrences in horizon | ✅ | Integration test with clock stub |
| FR-112 Generated job shape | ✅ | Assert all fields match definition snapshot |
| FR-113 Idempotency | ✅ | Run scheduler twice; assert no duplicates |
| FR-114 Respect EndDate | ✅ | Set EndDate = yesterday; run scheduler; assert no jobs |
| FR-115 Manual generate next | ✅ | POST endpoint; integration test |
| FR-116 Next occurrence logic | ✅ | Unit test on OccurrenceCalculator |
| FR-117 Duplicate rejection | ✅ | Business rule error when next already exists |
| FR-118 Generated job behaviour | ✅ | All Slice 1 mutation tests apply |
| FR-119 JobDefinitionId immutable | ✅ | Attempt to set; assert no change |
| FR-121 Ownership enforcement | ✅ | Cross-owner matrix test |
| FR-122 PropertyId ownership check | ✅ | Create with wrong propertyId; assert 404 |

## Success Criteria Measurability

| Criterion | Measurable? | Notes |
|---|---|---|
| SC-101 Upcoming jobs visible | ✅ | Job list count assertion |
| SC-102 Zero duplicates | ✅ | Count jobs after double scheduler run |
| SC-103 Generate next <500ms | ✅ | p95 timing in perf test |
| SC-104 Non-destructive edit | ✅ | Step count before/after assertion |
| SC-105 Zero cross-owner leakage | ✅ | Integration matrix |
| SC-106 401 on anonymous | ✅ | 401 matrix |

## Tech-Agnostic Check

- ✅ No implementation details in requirements (IHostedService is in Assumptions, not requirements)
- ✅ Success criteria expressed in terms of user-observable behaviour
- ✅ "Rolling 3-month horizon" is a product decision, not a technology choice

## Gaps / Issues

None. Spec passes all checklist items.
