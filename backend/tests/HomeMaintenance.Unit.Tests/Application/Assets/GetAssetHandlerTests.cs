using HomeMaintenance.Application.Assets.Queries;
using HomeMaintenance.Application.Common;
using HomeMaintenance.Application.Common.Interfaces;
using HomeMaintenance.Domain.Assets;
using HomeMaintenance.Domain.Identity;
using NSubstitute;
using Shouldly;

namespace HomeMaintenance.Unit.Tests.Application.Assets;

public sealed class GetAssetHandlerTests
{
    private static readonly OwnerId Alice = new("alice");

    [Fact]
    public async Task Handle_ExistingAsset_ReturnsDto()
    {
        var (handler, assets) = NewHandler();
        var asset = Asset.Create("asset-1", Alice, "prop-1", "Boiler");
        assets.GetAsync("asset-1", Alice, Arg.Any<CancellationToken>()).Returns(asset);

        var result = await handler.Handle(new GetAssetQuery("asset-1"));

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Name.ShouldBe("Boiler");
    }

    [Fact]
    public async Task Handle_UnknownAsset_ReturnsNotFound()
    {
        var (handler, assets) = NewHandler();
        assets.GetAsync("missing", Alice, Arg.Any<CancellationToken>()).Returns((Asset?)null);

        var result = await handler.Handle(new GetAssetQuery("missing"));

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBeOfType<NotFoundError>();
    }

    private static (GetAssetHandler handler, IAssetRepository assets) NewHandler()
    {
        var assets = Substitute.For<IAssetRepository>();
        var identity = Substitute.For<IIdentityProvider>();
        identity.CurrentOwner.Returns(Alice);
        var handler = new GetAssetHandler(assets, identity);
        return (handler, assets);
    }
}
