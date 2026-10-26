using HomeMaintenance.Application.Assets.Commands;
using HomeMaintenance.Application.Common;
using HomeMaintenance.Application.Common.Interfaces;
using HomeMaintenance.Domain.Assets;
using HomeMaintenance.Domain.Identity;
using HomeMaintenance.Domain.Properties;
using NSubstitute;
using Shouldly;

namespace HomeMaintenance.Unit.Tests.Application.Assets;

public sealed class CreateAssetHandlerTests
{
    private static readonly OwnerId Alice = new("alice");

    [Fact]
    public async Task Handle_ValidCommand_AddsAsset_AndEmitsAudit_AndReturnsSuccess()
    {
        var (handler, assets, properties, audit, correlation) = NewHandler();
        properties.GetAsync("prop-1", Alice, Arg.Any<CancellationToken>())
            .Returns(Property.Create("prop-1", Alice, "Main House"));

        var result = await handler.Handle(new CreateAssetCommand("prop-1", "Boiler", "Heating", "Serviced yearly"));

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Name.ShouldBe("Boiler");
        result.Value.Category.ShouldBe("Heating");

        await assets.Received(1).AddAsync(
            Arg.Is<Asset>(a => a.Owner == Alice && a.PropertyId == "prop-1" && a.Name == "Boiler"),
            Arg.Any<CancellationToken>());

        await audit.Received(1).RecordAsync(
            Arg.Is<AuditEvent>(e => e.EventType == AuditEventTypes.AssetCreated
                                     && e.Actor == "alice"
                                     && e.Target == $"asset:{result.Value.Id}"
                                     && e.CorrelationId == "corr-1"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_UnknownProperty_ReturnsNotFound_AndDoesNotAddAsset()
    {
        var (handler, assets, properties, audit, _) = NewHandler();
        properties.GetAsync("missing", Alice, Arg.Any<CancellationToken>())
            .Returns((Property?)null);

        var result = await handler.Handle(new CreateAssetCommand("missing", "Boiler"));

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBeOfType<NotFoundError>();

        await assets.DidNotReceive().AddAsync(Arg.Any<Asset>(), Arg.Any<CancellationToken>());
        await audit.DidNotReceive().RecordAsync(Arg.Any<AuditEvent>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_EmptyName_ReturnsValidationError()
    {
        var (handler, _, properties, _, _) = NewHandler();
        properties.GetAsync("prop-1", Alice, Arg.Any<CancellationToken>())
            .Returns(Property.Create("prop-1", Alice, "Main House"));

        var result = await handler.Handle(new CreateAssetCommand("prop-1", "   "));

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBeOfType<ValidationError>();
    }

    private static (CreateAssetHandler handler,
                     IAssetRepository assets,
                     IPropertyRepository properties,
                     IAuditLog audit,
                     ICorrelationContext correlation) NewHandler()
    {
        var assets = Substitute.For<IAssetRepository>();
        var properties = Substitute.For<IPropertyRepository>();
        var identity = Substitute.For<IIdentityProvider>();
        identity.CurrentOwner.Returns(Alice);
        var audit = Substitute.For<IAuditLog>();
        var correlation = Substitute.For<ICorrelationContext>();
        correlation.CurrentId.Returns("corr-1");
        var handler = new CreateAssetHandler(assets, properties, identity, audit, correlation);
        return (handler, assets, properties, audit, correlation);
    }
}
