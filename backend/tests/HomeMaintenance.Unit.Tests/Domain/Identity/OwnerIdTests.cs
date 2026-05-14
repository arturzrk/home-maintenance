using HomeMaintenance.Domain.Identity;
using Shouldly;

namespace HomeMaintenance.Unit.Tests.Domain.Identity;

public sealed class OwnerIdTests
{
    [Fact]
    public void Construction_WithValidValue_SetsValue()
    {
        var id = new OwnerId("google-oauth2|117583928473651098234");
        id.Value.ShouldBe("google-oauth2|117583928473651098234");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Construction_WithEmptyValue_Throws(string? invalid)
    {
        Should.Throw<ArgumentException>(() => new OwnerId(invalid!));
    }

    [Fact]
    public void Equality_SameValue_IsEqual()
    {
        var a = new OwnerId("user-1");
        var b = new OwnerId("user-1");

        a.ShouldBe(b);
        (a == b).ShouldBeTrue();
        a.GetHashCode().ShouldBe(b.GetHashCode());
    }

    [Fact]
    public void Equality_DifferentValue_IsNotEqual()
    {
        var a = new OwnerId("user-1");
        var b = new OwnerId("user-2");

        a.ShouldNotBe(b);
        (a != b).ShouldBeTrue();
    }

    [Fact]
    public void ToString_ReturnsValue()
    {
        var id = new OwnerId("user-1");
        id.ToString().ShouldBe("user-1");
    }

    [Fact]
    public void ImplicitStringConversion_Works()
    {
        var id = new OwnerId("user-1");
        string asString = id;
        asString.ShouldBe("user-1");
    }
}
