using HomeMaintenance.Domain.Common;
using Shouldly;

namespace HomeMaintenance.Unit.Tests.Common;

// Concrete stub for testing the abstract Entity base class.
file sealed class TestEntity : Entity
{
    public TestEntity(string id) : base(id) { }
}

public sealed class EntityTests
{
    [Fact]
    public void Constructor_WithValidId_SetsId()
    {
        var entity = new TestEntity("abc123");
        entity.Id.ShouldBe("abc123");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithEmptyId_ThrowsArgumentException(string invalidId)
    {
        Should.Throw<ArgumentException>(() => new TestEntity(invalidId));
    }

    [Fact]
    public void Equals_SameId_ReturnsTrue()
    {
        var a = new TestEntity("id-1");
        var b = new TestEntity("id-1");

        a.ShouldBe(b);
        (a == b).ShouldBeTrue();
    }

    [Fact]
    public void Equals_DifferentId_ReturnsFalse()
    {
        var a = new TestEntity("id-1");
        var b = new TestEntity("id-2");

        a.ShouldNotBe(b);
        (a != b).ShouldBeTrue();
    }
}
