using HomeMaintenance.Application.Common;
using HomeMaintenance.Application.Common.Interfaces;
using HomeMaintenance.Application.Properties.Commands;
using HomeMaintenance.Application.Properties.Queries;
using HomeMaintenance.Domain.Identity;
using HomeMaintenance.Domain.Properties;
using NSubstitute;
using Shouldly;

namespace HomeMaintenance.Unit.Tests.Application.Properties;

public sealed class RenamePropertyHandlerTests
{
    private static readonly OwnerId Alice = new("alice");

    [Fact]
    public async Task Handle_OwnedProperty_RenamesAndEmitsAudit()
    {
        var existing = Property.Create("p1", Alice, "Old Name");
        var (handler, repo, audit, _) = NewRenameHandler();
        repo.GetAsync("p1", Alice, Arg.Any<CancellationToken>()).Returns(existing);

        var result = await handler.Handle(new RenamePropertyCommand("p1", "New Name"));

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Name.ShouldBe("New Name");
        await repo.Received(1).UpdateAsync(existing, Arg.Any<CancellationToken>());
        await audit.Received(1).RecordAsync(
            Arg.Is<AuditEvent>(e => e.EventType == AuditEventTypes.PropertyRenamed
                                     && e.Payload!["old_name"]!.Equals("Old Name")
                                     && e.Payload["new_name"]!.Equals("New Name")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_UnknownOrNotOwned_ReturnsNotFound()
    {
        var (handler, repo, audit, _) = NewRenameHandler();
        repo.GetAsync("p1", Alice, Arg.Any<CancellationToken>()).Returns((Property?)null);

        var result = await handler.Handle(new RenamePropertyCommand("p1", "New Name"));

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBeOfType<NotFoundError>();
        await repo.DidNotReceive().UpdateAsync(Arg.Any<Property>(), Arg.Any<CancellationToken>());
        await audit.DidNotReceive().RecordAsync(Arg.Any<AuditEvent>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_InvalidName_ReturnsValidationError_AndDoesNotPersist()
    {
        var existing = Property.Create("p1", Alice, "Old Name");
        var (handler, repo, _, _) = NewRenameHandler();
        repo.GetAsync("p1", Alice, Arg.Any<CancellationToken>()).Returns(existing);

        var result = await handler.Handle(new RenamePropertyCommand("p1", ""));

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBeOfType<ValidationError>();
        await repo.DidNotReceive().UpdateAsync(Arg.Any<Property>(), Arg.Any<CancellationToken>());
    }

    private static (RenamePropertyHandler handler,
                    IPropertyRepository repo,
                    IAuditLog audit,
                    ICorrelationContext correlation) NewRenameHandler()
    {
        var repo = Substitute.For<IPropertyRepository>();
        var identity = Substitute.For<IIdentityProvider>();
        identity.CurrentOwner.Returns(Alice);
        var audit = Substitute.For<IAuditLog>();
        var correlation = Substitute.For<ICorrelationContext>();
        correlation.CurrentId.Returns("corr-1");
        var handler = new RenamePropertyHandler(repo, identity, audit, correlation);
        return (handler, repo, audit, correlation);
    }
}

public sealed class ListPropertiesHandlerTests
{
    private static readonly OwnerId Alice = new("alice");

    [Fact]
    public async Task Handle_ReturnsCallersPropertiesOnly()
    {
        var repo = Substitute.For<IPropertyRepository>();
        var identity = Substitute.For<IIdentityProvider>();
        identity.CurrentOwner.Returns(Alice);
        repo.ListAsync(Alice, Arg.Any<CancellationToken>())
            .Returns(new[]
            {
                Property.Create("p1", Alice, "Beach Cabin"),
                Property.Create("p2", Alice, "Main House"),
            });

        var handler = new ListPropertiesHandler(repo, identity);
        var result = await handler.Handle(new ListPropertiesQuery());

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Properties.Count.ShouldBe(2);
        result.Value.Properties[0].Name.ShouldBe("Beach Cabin");
        result.Value.Properties[1].Name.ShouldBe("Main House");
    }

    [Fact]
    public async Task Handle_EmptyResultSet_ReturnsEmptyList()
    {
        var repo = Substitute.For<IPropertyRepository>();
        var identity = Substitute.For<IIdentityProvider>();
        identity.CurrentOwner.Returns(Alice);
        repo.ListAsync(Alice, Arg.Any<CancellationToken>())
            .Returns(Array.Empty<Property>());

        var handler = new ListPropertiesHandler(repo, identity);
        var result = await handler.Handle(new ListPropertiesQuery());

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Properties.ShouldBeEmpty();
    }
}

public sealed class GetPropertyHandlerTests
{
    private static readonly OwnerId Alice = new("alice");

    [Fact]
    public async Task Handle_OwnedProperty_ReturnsDto()
    {
        var repo = Substitute.For<IPropertyRepository>();
        var identity = Substitute.For<IIdentityProvider>();
        identity.CurrentOwner.Returns(Alice);
        repo.GetAsync("p1", Alice, Arg.Any<CancellationToken>())
            .Returns(Property.Create("p1", Alice, "Main House"));

        var handler = new GetPropertyHandler(repo, identity);
        var result = await handler.Handle(new GetPropertyQuery("p1"));

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Id.ShouldBe("p1");
        result.Value.Name.ShouldBe("Main House");
    }

    [Fact]
    public async Task Handle_NotOwnedOrUnknown_ReturnsNotFound()
    {
        var repo = Substitute.For<IPropertyRepository>();
        var identity = Substitute.For<IIdentityProvider>();
        identity.CurrentOwner.Returns(Alice);
        repo.GetAsync("p1", Alice, Arg.Any<CancellationToken>()).Returns((Property?)null);

        var handler = new GetPropertyHandler(repo, identity);
        var result = await handler.Handle(new GetPropertyQuery("p1"));

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBeOfType<NotFoundError>();
    }
}
