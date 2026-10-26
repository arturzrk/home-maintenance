using HomeMaintenance.Application.Assets.Commands;
using HomeMaintenance.Application.Common;
using HomeMaintenance.Application.Common.Interfaces;
using HomeMaintenance.Domain.Assets;
using HomeMaintenance.Domain.Identity;
using NSubstitute;
using Shouldly;

namespace HomeMaintenance.Unit.Tests.Application.Assets;

public sealed class UpdateAssetHandlerTests
{
    private static readonly OwnerId Alice = new("alice");

    [Fact]
    public async Task Handle_RenameOnly_UpdatesAsset_AndEmitsAudit()
    {
        var (handler, assets, audit, _) = NewHandler();
        var asset = Asset.Create("asset-1", Alice, "prop-1", "Old Name");
        assets.GetAsync("asset-1", Alice, Arg.Any<CancellationToken>()).Returns(asset);

        var result = await handler.Handle(new UpdateAssetCommand("asset-1", Name: "New Name"));

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Name.ShouldBe("New Name");

        await assets.Received(1).UpdateAsync(
            Arg.Is<Asset>(a => a.Name == "New Name"),
            Arg.Any<CancellationToken>());

        await audit.Received(1).RecordAsync(
            Arg.Is<AuditEvent>(e => e.EventType == AuditEventTypes.AssetUpdated
                                     && e.Payload!["name"]!.Equals("New Name")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_CategoryAndNotesAndObsolete_AppliesAllChanges()
    {
        var (handler, assets, audit, _) = NewHandler();
        var asset = Asset.Create("asset-1", Alice, "prop-1", "Boiler");
        assets.GetAsync("asset-1", Alice, Arg.Any<CancellationToken>()).Returns(asset);

        var result = await handler.Handle(new UpdateAssetCommand(
            "asset-1", Category: "Heating", Notes: "Serviced", IsObsolete: true));

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Category.ShouldBe("Heating");
        result.Value.IsObsolete.ShouldBeTrue();

        await audit.Received(1).RecordAsync(
            Arg.Is<AuditEvent>(e => e.Payload!["category"]!.Equals("Heating")
                                     && e.Payload["notes_changed"]!.Equals(true)
                                     && e.Payload["isObsolete"]!.Equals(true)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_NoFieldsProvided_ReturnsValidationError_AndDoesNotTouchRepository()
    {
        var (handler, assets, audit, _) = NewHandler();

        var result = await handler.Handle(new UpdateAssetCommand("asset-1"));

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBeOfType<ValidationError>();

        await assets.DidNotReceive().GetAsync(Arg.Any<string>(), Arg.Any<OwnerId>(), Arg.Any<CancellationToken>());
        await audit.DidNotReceive().RecordAsync(Arg.Any<AuditEvent>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_UnknownAsset_ReturnsNotFound()
    {
        var (handler, assets, _, _) = NewHandler();
        assets.GetAsync("missing", Alice, Arg.Any<CancellationToken>()).Returns((Asset?)null);

        var result = await handler.Handle(new UpdateAssetCommand("missing", Name: "New Name"));

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBeOfType<NotFoundError>();
    }

    [Fact]
    public async Task Handle_NameTooLong_ReturnsValidationError()
    {
        var (handler, assets, _, _) = NewHandler();
        var asset = Asset.Create("asset-1", Alice, "prop-1", "Boiler");
        assets.GetAsync("asset-1", Alice, Arg.Any<CancellationToken>()).Returns(asset);

        var result = await handler.Handle(new UpdateAssetCommand("asset-1", Name: new string('x', 201)));

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBeOfType<ValidationError>();
    }

    private static (UpdateAssetHandler handler,
                     IAssetRepository assets,
                     IAuditLog audit,
                     ICorrelationContext correlation) NewHandler()
    {
        var assets = Substitute.For<IAssetRepository>();
        var identity = Substitute.For<IIdentityProvider>();
        identity.CurrentOwner.Returns(Alice);
        var audit = Substitute.For<IAuditLog>();
        var correlation = Substitute.For<ICorrelationContext>();
        correlation.CurrentId.Returns("corr-1");
        var handler = new UpdateAssetHandler(assets, identity, audit, correlation);
        return (handler, assets, audit, correlation);
    }
}
