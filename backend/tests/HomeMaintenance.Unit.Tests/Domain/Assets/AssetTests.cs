using HomeMaintenance.Domain.Assets;
using HomeMaintenance.Domain.Identity;
using Shouldly;

namespace HomeMaintenance.Unit.Tests.Domain.Assets;

public sealed class AssetTests
{
    private static readonly OwnerId Owner = new("owner-1");

    private static Asset NewAsset(
        string name = "Boiler",
        string? category = "Heating",
        string? notes = null)
        => Asset.Create("asset-1", Owner, "prop-1", name, category, notes);

    [Fact]
    public void Create_SetsFields_AndDefaultsToNotObsolete()
    {
        var asset = NewAsset(notes: "Installed 2020");

        asset.Id.ShouldBe("asset-1");
        asset.Owner.ShouldBe(Owner);
        asset.PropertyId.ShouldBe("prop-1");
        asset.Name.ShouldBe("Boiler");
        asset.Category.ShouldBe("Heating");
        asset.Notes.ShouldBe("Installed 2020");
        asset.IsObsolete.ShouldBeFalse();
    }

    [Fact]
    public void Create_TrimsName_AndNormalizesBlankOptionalsToNull()
    {
        var asset = Asset.Create("a", Owner, "p", "  Boiler  ", "  ", "");

        asset.Name.ShouldBe("Boiler");
        asset.Category.ShouldBeNull();
        asset.Notes.ShouldBeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_RejectsBlankName(string name)
        => Should.Throw<ArgumentException>(() => NewAsset(name: name));

    [Fact]
    public void Create_RejectsOverlongName()
        => Should.Throw<ArgumentException>(() => NewAsset(name: new string('x', 201)));

    [Fact]
    public void Create_RejectsOverlongCategory()
        => Should.Throw<ArgumentException>(() => NewAsset(category: new string('x', 101)));

    [Fact]
    public void Create_RejectsOverlongNotes()
        => Should.Throw<ArgumentException>(() => NewAsset(notes: new string('x', 2001)));

    [Fact]
    public void Create_RejectsMissingPropertyId()
        => Should.Throw<ArgumentException>(() => Asset.Create("a", Owner, " ", "Boiler"));

    [Fact]
    public void Rename_Trims_AndValidates()
    {
        var asset = NewAsset();

        asset.Rename("  New Boiler  ");
        asset.Name.ShouldBe("New Boiler");

        Should.Throw<ArgumentException>(() => asset.Rename(" "));
    }

    [Fact]
    public void SetCategory_And_SetNotes_ClearOnBlank()
    {
        var asset = NewAsset(notes: "note");

        asset.SetCategory(null);
        asset.SetNotes("  ");

        asset.Category.ShouldBeNull();
        asset.Notes.ShouldBeNull();
    }

    [Fact]
    public void SetObsolete_RoundTrips()
    {
        var asset = NewAsset();

        asset.SetObsolete(true);
        asset.IsObsolete.ShouldBeTrue();

        asset.SetObsolete(false);
        asset.IsObsolete.ShouldBeFalse();
    }
}
