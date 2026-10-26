using HomeMaintenance.Application.Assets;
using HomeMaintenance.Application.Common;
using HomeMaintenance.Application.Common.Interfaces;
using HomeMaintenance.Domain.Assets;
using HomeMaintenance.Domain.Identity;
using NSubstitute;
using Shouldly;

namespace HomeMaintenance.Unit.Tests.Application.Assets;

public sealed class AssetScopeValidationTests
{
    private static readonly OwnerId Alice = new("alice");

    [Fact]
    public async Task CheckAsync_ValidAsset_ReturnsNull()
    {
        var assets = Substitute.For<IAssetRepository>();
        var asset = Asset.Create("asset-1", Alice, "prop-1", "Boiler");
        assets.GetAsync("asset-1", Alice, Arg.Any<CancellationToken>()).Returns(asset);

        var error = await AssetScopeValidation.CheckAsync(assets, Alice, "prop-1", "asset-1", CancellationToken.None);

        error.ShouldBeNull();
    }

    [Fact]
    public async Task CheckAsync_UnknownAsset_ReturnsNotFound()
    {
        var assets = Substitute.For<IAssetRepository>();
        assets.GetAsync("missing", Alice, Arg.Any<CancellationToken>()).Returns((Asset?)null);

        var error = await AssetScopeValidation.CheckAsync(assets, Alice, "prop-1", "missing", CancellationToken.None);

        error.ShouldBeOfType<NotFoundError>();
    }

    [Fact]
    public async Task CheckAsync_AssetBelongsToDifferentProperty_ReturnsBusinessRule()
    {
        var assets = Substitute.For<IAssetRepository>();
        var asset = Asset.Create("asset-1", Alice, "prop-other", "Boiler");
        assets.GetAsync("asset-1", Alice, Arg.Any<CancellationToken>()).Returns(asset);

        var error = await AssetScopeValidation.CheckAsync(assets, Alice, "prop-1", "asset-1", CancellationToken.None);

        var businessError = error.ShouldBeOfType<BusinessRuleError>();
        businessError.Rule.ShouldBe("asset_property_mismatch");
    }

    [Fact]
    public async Task CheckAsync_ObsoleteAsset_ReturnsBusinessRule()
    {
        var assets = Substitute.For<IAssetRepository>();
        var asset = Asset.Create("asset-1", Alice, "prop-1", "Boiler");
        asset.SetObsolete(true);
        assets.GetAsync("asset-1", Alice, Arg.Any<CancellationToken>()).Returns(asset);

        var error = await AssetScopeValidation.CheckAsync(assets, Alice, "prop-1", "asset-1", CancellationToken.None);

        var businessError = error.ShouldBeOfType<BusinessRuleError>();
        businessError.Rule.ShouldBe("asset_obsolete");
    }
}
