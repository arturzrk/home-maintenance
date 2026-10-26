using HomeMaintenance.Application.Common.Interfaces;
using HomeMaintenance.Application.JobDefinitions;
using HomeMaintenance.Application.JobDefinitions.Commands;
using HomeMaintenance.Application.JobDefinitions.Dto;
using HomeMaintenance.Domain.Assets;
using HomeMaintenance.Domain.Identity;
using HomeMaintenance.Domain.JobDefinitions;
using HomeMaintenance.Domain.Jobs;
using HomeMaintenance.Domain.Properties;
using NSubstitute;
using Shouldly;

namespace HomeMaintenance.Unit.Tests.Application.JobDefinitions;

public sealed class CreateJobDefinitionHandlerTests
{
    private static readonly OwnerId Alice = new("alice");
    private const string PropertyId = "prop-1";

    private static ScheduleDefinitionDto MonthlyScheduleDto()
        => new("Month", 1, new DateOnly(2026, 1, 1), null);

    private static (
        IJobDefinitionRepository defs,
        IPropertyRepository properties,
        IAssetRepository assets,
        IDateTimeProvider clock,
        IJobRepository jobs,
        IAuditLog audit,
        ICorrelationContext correlation,
        CreateJobDefinitionHandler handler) Build()
    {
        var defs = Substitute.For<IJobDefinitionRepository>();
        var properties = Substitute.For<IPropertyRepository>();
        var assets = Substitute.For<IAssetRepository>();
        var identity = Substitute.For<IIdentityProvider>();
        identity.CurrentOwner.Returns(Alice);
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcToday.Returns(new DateOnly(2026, 6, 1));
        var jobs = Substitute.For<IJobRepository>();
        var audit = Substitute.For<IAuditLog>();
        var correlation = Substitute.For<ICorrelationContext>();
        correlation.CurrentId.Returns("corr-1");

        var generationService = new JobGenerationService(jobs, audit, correlation);
        var handler = new CreateJobDefinitionHandler(defs, properties, assets, identity, clock, generationService, audit, correlation);
        return (defs, properties, assets, clock, jobs, audit, correlation, handler);
    }

    [Fact]
    public async Task Success_CreatesDefinitionAndGeneratesJobs()
    {
        var (defs, properties, _, _, jobs, audit, _, handler) = Build();
        properties.GetAsync(PropertyId, Alice, Arg.Any<CancellationToken>())
            .Returns(Property.Create("prop-1", Alice, "Main House"));

        var cmd = new CreateJobDefinitionCommand(
            PropertyId, "Boiler service", MonthlyScheduleDto(), new[] { "Shut off gas" });

        var result = await handler.Handle(cmd);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Name.ShouldBe("Boiler service");
        result.Value.StepTemplates.Count.ShouldBe(1);
        await defs.Received(1).AddAsync(Arg.Any<JobDefinition>(), Arg.Any<CancellationToken>());
        await jobs.Received().AddAsync(Arg.Any<Job>(), Arg.Any<CancellationToken>());
        await audit.Received(1).RecordAsync(
            Arg.Is<AuditEvent>(e => e.EventType == AuditEventTypes.JobDefinitionCreated),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvalidSchedule_MultiplierZero_ReturnsValidationError()
    {
        var (_, properties, _, _, _, _, _, handler) = Build();
        properties.GetAsync(PropertyId, Alice, Arg.Any<CancellationToken>())
            .Returns(Property.Create("prop-1", Alice, "Main House"));

        var badSchedule = new ScheduleDefinitionDto("Month", 0, new DateOnly(2026, 1, 1), null);
        var cmd = new CreateJobDefinitionCommand(PropertyId, "Test", badSchedule, Array.Empty<string>());

        var result = await handler.Handle(cmd);

        result.IsFailure.ShouldBeTrue();
        result.Error!.Code.ShouldBe("validation");
    }

    [Fact]
    public async Task CrossPropertyOwnership_ReturnsNotFoundError()
    {
        var (_, properties, _, _, _, _, _, handler) = Build();
        properties.GetAsync(PropertyId, Alice, Arg.Any<CancellationToken>())
            .Returns((Property?)null);

        var cmd = new CreateJobDefinitionCommand(
            PropertyId, "Test", MonthlyScheduleDto(), Array.Empty<string>());

        var result = await handler.Handle(cmd);

        result.IsFailure.ShouldBeTrue();
        result.Error!.Code.ShouldBe("not_found");
    }

    [Fact]
    public async Task WithValidAssetId_ScopesDefinitionToAsset()
    {
        var (defs, properties, assets, _, _, _, _, handler) = Build();
        properties.GetAsync(PropertyId, Alice, Arg.Any<CancellationToken>())
            .Returns(Property.Create("prop-1", Alice, "Main House"));
        var asset = Asset.Create("asset-1", Alice, PropertyId, "Boiler");
        assets.GetAsync("asset-1", Alice, Arg.Any<CancellationToken>()).Returns(asset);

        var cmd = new CreateJobDefinitionCommand(
            PropertyId, "Boiler service", MonthlyScheduleDto(), Array.Empty<string>(), AssetId: "asset-1");

        var result = await handler.Handle(cmd);

        result.IsSuccess.ShouldBeTrue();
        await defs.Received(1).AddAsync(
            Arg.Is<JobDefinition>(d => d.AssetId == "asset-1"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WithAssetFromDifferentProperty_ReturnsBusinessRuleError()
    {
        var (_, properties, assets, _, _, _, _, handler) = Build();
        properties.GetAsync(PropertyId, Alice, Arg.Any<CancellationToken>())
            .Returns(Property.Create("prop-1", Alice, "Main House"));
        var asset = Asset.Create("asset-1", Alice, "prop-other", "Boiler");
        assets.GetAsync("asset-1", Alice, Arg.Any<CancellationToken>()).Returns(asset);

        var cmd = new CreateJobDefinitionCommand(
            PropertyId, "Boiler service", MonthlyScheduleDto(), Array.Empty<string>(), AssetId: "asset-1");

        var result = await handler.Handle(cmd);

        result.IsFailure.ShouldBeTrue();
        result.Error!.Code.ShouldBe("business_rule");
    }

    [Fact]
    public async Task EmptyName_ReturnsValidationError()
    {
        var (_, properties, _, _, _, _, _, handler) = Build();
        properties.GetAsync(PropertyId, Alice, Arg.Any<CancellationToken>())
            .Returns(Property.Create("prop-1", Alice, "Main House"));

        var cmd = new CreateJobDefinitionCommand(PropertyId, "", MonthlyScheduleDto(), Array.Empty<string>());

        var result = await handler.Handle(cmd);

        result.IsFailure.ShouldBeTrue();
        result.Error!.Code.ShouldBe("validation");
    }
}
