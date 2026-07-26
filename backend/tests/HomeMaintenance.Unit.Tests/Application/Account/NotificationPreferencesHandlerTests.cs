using HomeMaintenance.Application.Account.Commands;
using HomeMaintenance.Application.Account.Queries;
using HomeMaintenance.Application.Common;
using HomeMaintenance.Application.Common.Interfaces;
using HomeMaintenance.Domain.Identity;
using NSubstitute;
using Shouldly;

namespace HomeMaintenance.Unit.Tests.Application.Account;

public sealed class GetNotificationPreferencesHandlerTests
{
    private static readonly OwnerId Alice = new("alice");

    [Fact]
    public async Task Handle_ProfileExists_ReturnsStoredValues()
    {
        var repo = Substitute.For<IOwnerProfileRepository>();
        var identity = Substitute.For<IIdentityProvider>();
        identity.CurrentOwner.Returns(Alice);
        repo.GetAsync(Alice, Arg.Any<CancellationToken>())
            .Returns(OwnerProfile.Create("p1", Alice, "alice@example.com"));

        var handler = new GetNotificationPreferencesHandler(repo, identity);
        var result = await handler.Handle(new GetNotificationPreferencesQuery());

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Email.ShouldBe("alice@example.com");
        result.Value.RemindersEnabled.ShouldBeTrue();
    }

    [Fact]
    public async Task Handle_NoProfileYet_DefaultsToRemindersEnabled_WithNullEmail()
    {
        var repo = Substitute.For<IOwnerProfileRepository>();
        var identity = Substitute.For<IIdentityProvider>();
        identity.CurrentOwner.Returns(Alice);
        repo.GetAsync(Alice, Arg.Any<CancellationToken>()).Returns((OwnerProfile?)null);

        var handler = new GetNotificationPreferencesHandler(repo, identity);
        var result = await handler.Handle(new GetNotificationPreferencesQuery());

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Email.ShouldBeNull();
        result.Value.RemindersEnabled.ShouldBeTrue();
    }
}

public sealed class UpdateNotificationPreferencesHandlerTests
{
    private static readonly OwnerId Alice = new("alice");

    [Fact]
    public async Task Handle_ProfileExists_PersistsToggle_AndEmitsAudit()
    {
        var existing = OwnerProfile.Create("p1", Alice, "alice@example.com");
        var (handler, repo, audit, _) = NewHandler();
        repo.GetAsync(Alice, Arg.Any<CancellationToken>()).Returns(existing);

        var result = await handler.Handle(new UpdateNotificationPreferencesCommand(false));

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Email.ShouldBe("alice@example.com");
        result.Value.RemindersEnabled.ShouldBeFalse();

        await repo.Received(1).UpdateRemindersEnabledAsync(Alice, false, Arg.Any<CancellationToken>());
        await audit.Received(1).RecordAsync(
            Arg.Is<AuditEvent>(e => e.EventType == AuditEventTypes.NotificationPreferencesUpdated
                                     && e.Payload!["remindersEnabled"]!.Equals(false)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_NoProfileYet_ReturnsNotFound_AndDoesNotPersist()
    {
        var (handler, repo, audit, _) = NewHandler();
        repo.GetAsync(Alice, Arg.Any<CancellationToken>()).Returns((OwnerProfile?)null);

        var result = await handler.Handle(new UpdateNotificationPreferencesCommand(false));

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBeOfType<NotFoundError>();
        await repo.DidNotReceive().UpdateRemindersEnabledAsync(
            Arg.Any<OwnerId>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
        await audit.DidNotReceive().RecordAsync(Arg.Any<AuditEvent>(), Arg.Any<CancellationToken>());
    }

    private static (UpdateNotificationPreferencesHandler handler,
                    IOwnerProfileRepository repo,
                    IAuditLog audit,
                    ICorrelationContext correlation) NewHandler()
    {
        var repo = Substitute.For<IOwnerProfileRepository>();
        var identity = Substitute.For<IIdentityProvider>();
        identity.CurrentOwner.Returns(Alice);
        var audit = Substitute.For<IAuditLog>();
        var correlation = Substitute.For<ICorrelationContext>();
        correlation.CurrentId.Returns("corr-1");
        var handler = new UpdateNotificationPreferencesHandler(repo, identity, audit, correlation);
        return (handler, repo, audit, correlation);
    }
}
