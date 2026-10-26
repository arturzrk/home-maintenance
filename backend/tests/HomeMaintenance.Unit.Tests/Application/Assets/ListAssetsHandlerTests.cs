using HomeMaintenance.Application.Assets.Queries;
using HomeMaintenance.Application.Common;
using HomeMaintenance.Application.Common.Interfaces;
using HomeMaintenance.Domain.Assets;
using HomeMaintenance.Domain.Identity;
using HomeMaintenance.Domain.Properties;
using NSubstitute;
using Shouldly;

namespace HomeMaintenance.Unit.Tests.Application.Assets;

public sealed class ListAssetsHandlerTests
{
    private static readonly OwnerId Alice = new("alice");

    [Fact]
    public async Task Handle_ExistingProperty_ReturnsAssets()
    {
        var (handler, assets, properties) = NewHandler();
        properties.GetAsync("prop-1", Alice, Arg.Any<CancellationToken>())
            .Returns(Property.Create("prop-1", Alice, "Main House"));
        assets.ListByPropertyAsync("prop-1", Alice, Arg.Any<CancellationToken>())
            .Returns(new List<Asset>
            {
                Asset.Create("asset-1", Alice, "prop-1", "Boiler"),
                Asset.Create("asset-2", Alice, "prop-1", "Roof"),
            });

        var result = await handler.Handle(new ListAssetsQuery("prop-1"));

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Count.ShouldBe(2);
        result.Value.Select(a => a.Name).ShouldBe(["Boiler", "Roof"]);
    }

    [Fact]
    public async Task Handle_UnknownProperty_ReturnsNotFound()
    {
        var (handler, assets, properties) = NewHandler();
        properties.GetAsync("missing", Alice, Arg.Any<CancellationToken>()).Returns((Property?)null);

        var result = await handler.Handle(new ListAssetsQuery("missing"));

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBeOfType<NotFoundError>();
        await assets.DidNotReceive().ListByPropertyAsync(
            Arg.Any<string>(), Arg.Any<OwnerId>(), Arg.Any<CancellationToken>());
    }

    private static (ListAssetsHandler handler,
                     IAssetRepository assets,
                     IPropertyRepository properties) NewHandler()
    {
        var assets = Substitute.For<IAssetRepository>();
        var properties = Substitute.For<IPropertyRepository>();
        var identity = Substitute.For<IIdentityProvider>();
        identity.CurrentOwner.Returns(Alice);
        var handler = new ListAssetsHandler(assets, properties, identity);
        return (handler, assets, properties);
    }
}
