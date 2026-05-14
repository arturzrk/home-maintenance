using HomeMaintenance.Application.Common;
using HomeMaintenance.Application.Common.Interfaces;
using HomeMaintenance.Application.Properties.Commands;
using HomeMaintenance.Domain.Identity;
using HomeMaintenance.Domain.Properties;
using NSubstitute;
using Shouldly;

namespace HomeMaintenance.Unit.Tests.Application.Properties;

public sealed class CreatePropertyHandlerTests
{
    private static readonly OwnerId Alice = new("alice");

    [Fact]
    public async Task Handle_ValidName_AddsProperty_AndEmitsAudit_AndReturnsSuccess()
    {
        var (handler, repo, audit, correlation) = NewHandler();

        var result = await handler.Handle(new CreatePropertyCommand("Main House"));

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Name.ShouldBe("Main House");
        result.Value.Id.ShouldNotBeNullOrEmpty();

        await repo.Received(1).AddAsync(
            Arg.Is<Property>(p => p.Owner == Alice && p.Name == "Main House"),
            Arg.Any<CancellationToken>());

        await audit.Received(1).RecordAsync(
            Arg.Is<AuditEvent>(e => e.EventType == AuditEventTypes.PropertyCreated
                                     && e.Actor == "alice"
                                     && e.Target == $"property:{result.Value.Id}"
                                     && e.CorrelationId == "corr-1"
                                     && e.Payload!["name"]!.Equals("Main House")),
            Arg.Any<CancellationToken>());

        _ = correlation; // keep reference live; correlation source asserted via audit event
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Handle_EmptyName_ReturnsValidationError(string invalid)
    {
        var (handler, repo, audit, _) = NewHandler();

        var result = await handler.Handle(new CreatePropertyCommand(invalid));

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBeOfType<ValidationError>();
        ((ValidationError)result.Error!).Field.ShouldBe("name");

        await repo.DidNotReceive().AddAsync(Arg.Any<Property>(), Arg.Any<CancellationToken>());
        await audit.DidNotReceive().RecordAsync(Arg.Any<AuditEvent>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_NameTooLong_ReturnsValidationError()
    {
        var (handler, _, _, _) = NewHandler();

        var result = await handler.Handle(new CreatePropertyCommand(new string('x', 101)));

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBeOfType<ValidationError>();
    }

    private static (CreatePropertyHandler handler,
                    IPropertyRepository repo,
                    IAuditLog audit,
                    ICorrelationContext correlation) NewHandler()
    {
        var repo = Substitute.For<IPropertyRepository>();
        var identity = Substitute.For<IIdentityProvider>();
        identity.CurrentOwner.Returns(Alice);
        var audit = Substitute.For<IAuditLog>();
        var correlation = Substitute.For<ICorrelationContext>();
        correlation.CurrentId.Returns("corr-1");
        var handler = new CreatePropertyHandler(repo, identity, audit, correlation);
        return (handler, repo, audit, correlation);
    }
}
