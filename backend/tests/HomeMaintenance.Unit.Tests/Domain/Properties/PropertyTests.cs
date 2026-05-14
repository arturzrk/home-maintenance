using HomeMaintenance.Domain.Identity;
using HomeMaintenance.Domain.Properties;
using Shouldly;

namespace HomeMaintenance.Unit.Tests.Domain.Properties;

public sealed class PropertyTests
{
    private static readonly OwnerId Owner = new("alice");

    [Fact]
    public void Create_WithValidName_AssignsTrimmedName()
    {
        var property = Property.Create("id-1", Owner, "  Main House  ");
        property.Id.ShouldBe("id-1");
        property.Owner.ShouldBe(Owner);
        property.Name.ShouldBe("Main House");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithEmptyOrWhitespaceName_Throws(string? invalid)
    {
        Should.Throw<ArgumentException>(() => Property.Create("id-1", Owner, invalid!));
    }

    [Fact]
    public void Create_WithName101Chars_Throws()
    {
        var name = new string('x', 101);
        Should.Throw<ArgumentException>(() => Property.Create("id-1", Owner, name));
    }

    [Fact]
    public void Create_WithName100Chars_Succeeds()
    {
        var name = new string('x', 100);
        var property = Property.Create("id-1", Owner, name);
        property.Name.ShouldBe(name);
    }

    [Fact]
    public void Create_WithNullOwner_Throws()
    {
        Should.Throw<ArgumentNullException>(() => Property.Create("id-1", null!, "Main House"));
    }

    [Fact]
    public void Rename_WithValidName_UpdatesAndTrims()
    {
        var property = Property.Create("id-1", Owner, "Main House");
        property.Rename("  Beach Cabin  ");
        property.Name.ShouldBe("Beach Cabin");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Rename_WithEmptyName_Throws(string invalid)
    {
        var property = Property.Create("id-1", Owner, "Main House");
        Should.Throw<ArgumentException>(() => property.Rename(invalid));
        property.Name.ShouldBe("Main House"); // unchanged
    }

    [Fact]
    public void Rename_WithName101Chars_Throws()
    {
        var property = Property.Create("id-1", Owner, "Main House");
        Should.Throw<ArgumentException>(() => property.Rename(new string('x', 101)));
    }

    [Fact]
    public void Create_TwoPropertiesSameOwner_AreNotEqual()
    {
        var a = Property.Create("id-1", Owner, "House A");
        var b = Property.Create("id-2", Owner, "House B");
        a.ShouldNotBe(b);
    }

    [Fact]
    public void Hydrate_BypassesValidation()
    {
        // Older records may have data that would fail current validation;
        // Hydrate must still load them.
        var property = Property.Hydrate("id-1", Owner, "");
        property.Name.ShouldBe("");
    }
}
