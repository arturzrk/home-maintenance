using HomeMaintenance.Domain.Identity;
using HomeMaintenance.Domain.JobDefinitions;
using HomeMaintenance.Domain.Jobs;
using Shouldly;

namespace HomeMaintenance.Unit.Tests.Domain.Assets;

public sealed class AssetScopedWorkTests
{
    private static readonly OwnerId Owner = new("owner-1");

    private static ScheduleDefinition MonthlySchedule()
        => new(CadenceUnit.Month, 1, new DateOnly(2026, 6, 1), null);

    [Fact]
    public void JobCreate_CarriesAssetId()
    {
        var job = Job.Create(
            "job-1", Owner, "prop-1", "Service boiler", null,
            Array.Empty<string>(), assetId: "asset-1");

        job.AssetId.ShouldBe("asset-1");
    }

    [Fact]
    public void JobCreate_DefaultsAssetIdToNull()
    {
        var job = Job.Create(
            "job-1", Owner, "prop-1", "Service boiler", null, Array.Empty<string>());

        job.AssetId.ShouldBeNull();
    }

    [Fact]
    public void JobDefinitionCreate_CarriesAssetId()
    {
        var def = JobDefinition.Create(
            "def-1", Owner, "prop-1", "Service boiler", MonthlySchedule(),
            Array.Empty<string>(), assetId: "asset-1");

        def.AssetId.ShouldBe("asset-1");
    }

    [Fact]
    public void JobDefinitionCreate_DefaultsAssetIdToNull()
    {
        var def = JobDefinition.Create(
            "def-1", Owner, "prop-1", "Service boiler", MonthlySchedule(),
            Array.Empty<string>());

        def.AssetId.ShouldBeNull();
    }
}
