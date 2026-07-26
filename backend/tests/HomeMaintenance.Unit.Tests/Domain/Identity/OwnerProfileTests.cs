using HomeMaintenance.Domain.Identity;
using Shouldly;

namespace HomeMaintenance.Unit.Tests.Domain.Identity;

public sealed class OwnerProfileTests
{
    private static readonly OwnerId Owner = new("owner-1");

    [Fact]
    public void Create_SetsFields_AndDefaultsToRemindersEnabled()
    {
        var profile = OwnerProfile.Create("profile-1", Owner, "alice@example.com");

        profile.Id.ShouldBe("profile-1");
        profile.Owner.ShouldBe(Owner);
        profile.Email.ShouldBe("alice@example.com");
        profile.RemindersEnabled.ShouldBeTrue();
    }

    [Fact]
    public void Create_TrimsEmail()
    {
        var profile = OwnerProfile.Create("profile-1", Owner, "  alice@example.com  ");

        profile.Email.ShouldBe("alice@example.com");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_RejectsBlankEmail(string email)
        => Should.Throw<ArgumentException>(() => OwnerProfile.Create("profile-1", Owner, email));

    [Fact]
    public void Create_RejectsNullOwner()
        => Should.Throw<ArgumentNullException>(() => OwnerProfile.Create("profile-1", null!, "alice@example.com"));

    [Fact]
    public void UpdateEmail_Trims_AndValidates()
    {
        var profile = OwnerProfile.Create("profile-1", Owner, "alice@example.com");

        profile.UpdateEmail("  alice+new@example.com  ");
        profile.Email.ShouldBe("alice+new@example.com");

        Should.Throw<ArgumentException>(() => profile.UpdateEmail(" "));
    }

    [Fact]
    public void SetRemindersEnabled_RoundTrips()
    {
        var profile = OwnerProfile.Create("profile-1", Owner, "alice@example.com");

        profile.SetRemindersEnabled(false);
        profile.RemindersEnabled.ShouldBeFalse();

        profile.SetRemindersEnabled(true);
        profile.RemindersEnabled.ShouldBeTrue();
    }
}
