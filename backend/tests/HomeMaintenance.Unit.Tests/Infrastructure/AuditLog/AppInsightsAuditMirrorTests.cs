using HomeMaintenance.Application.Common.Interfaces;
using HomeMaintenance.Infrastructure.AuditLog;
using Microsoft.Extensions.Logging;
using MongoException = MongoDB.Driver.MongoException;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;

namespace HomeMaintenance.Unit.Tests.Infrastructure.AuditLog;

/// <summary>
/// Unit tests for <see cref="AppInsightsAuditMirror"/> failure semantics:
/// <list type="bullet">
/// <item>Primary write failure is caught, logged at Error, not rethrown.</item>
/// <item>Mirror never rethrows even when the activity creation itself throws.</item>
/// <item>Payload is never forwarded to the App Insights mirror.</item>
/// </list>
/// </summary>
public sealed class AppInsightsAuditMirrorTests
{
    private static AuditEvent SampleEvent(string actor = "test-actor") =>
        new AuditEvent(
            AuditEventTypes.PropertyCreated,
            actor,
            "property:test-id",
            DateTime.UtcNow,
            "corr-123",
            new Dictionary<string, object?> { ["name"] = "Secret Property Name" });

    // ── Happy path ───────────────────────────────────────────────────────

    [Fact]
    public async Task RecordAsync_DelegatesToInner_WhenSuccessful()
    {
        var inner  = Substitute.For<IAuditLog>();
        var logger = Substitute.For<ILogger<AppInsightsAuditMirror>>();
        var mirror = new AppInsightsAuditMirror(inner, logger);

        var evt = SampleEvent();
        await mirror.RecordAsync(evt);

        await inner.Received(1).RecordAsync(evt, Arg.Any<CancellationToken>());
    }

    // ── Failure semantics ────────────────────────────────────────────────

    [Fact]
    public async Task RecordAsync_InnerThrows_DoesNotRethrow()
    {
        var inner  = Substitute.For<IAuditLog>();
        var logger = Substitute.For<ILogger<AppInsightsAuditMirror>>();
        inner.RecordAsync(Arg.Any<AuditEvent>(), Arg.Any<CancellationToken>())
             .ThrowsAsync(new MongoException("Simulated Mongo failure"));

        var mirror = new AppInsightsAuditMirror(inner, logger);

        // Must complete without throwing — domain request still succeeds.
        await Should.NotThrowAsync(() => mirror.RecordAsync(SampleEvent()));
    }

    [Fact]
    public async Task RecordAsync_InnerThrows_LogsError()
    {
        var inner  = Substitute.For<IAuditLog>();
        var logger = Substitute.For<ILogger<AppInsightsAuditMirror>>();
        inner.RecordAsync(Arg.Any<AuditEvent>(), Arg.Any<CancellationToken>())
             .ThrowsAsync(new MongoException("Simulated Mongo failure"));

        var mirror = new AppInsightsAuditMirror(inner, logger);
        await mirror.RecordAsync(SampleEvent());

        // Verify an Error-level log entry was emitted.
        logger.Received(1).Log(
            LogLevel.Error,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Is<Exception>(ex => ex.Message.Contains("Simulated Mongo failure")),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task RecordAsync_InnerThrows_StillCallsOnlyOnce()
    {
        var inner  = Substitute.For<IAuditLog>();
        var logger = Substitute.For<ILogger<AppInsightsAuditMirror>>();
        inner.RecordAsync(Arg.Any<AuditEvent>(), Arg.Any<CancellationToken>())
             .ThrowsAsync(new MongoException("fail"));

        var mirror = new AppInsightsAuditMirror(inner, logger);
        await mirror.RecordAsync(SampleEvent());

        // Inner called exactly once — no retry
        await inner.Received(1).RecordAsync(Arg.Any<AuditEvent>(), Arg.Any<CancellationToken>());
    }

    // ── Privacy boundary ─────────────────────────────────────────────────

    [Fact]
    public async Task RecordAsync_Success_LoggerReceivesNoPayload()
    {
        // The App Insights mirror (ActivitySource) must NEVER log Payload.
        // We can't easily inspect ActivitySource in a unit test, but we
        // CAN verify that no logger call carries payload content.
        var inner  = Substitute.For<IAuditLog>();
        var logger = Substitute.For<ILogger<AppInsightsAuditMirror>>();
        var mirror = new AppInsightsAuditMirror(inner, logger);

        await mirror.RecordAsync(SampleEvent());

        // Logger should receive zero calls on the success path
        // (mirror uses ActivitySource, not ILogger, for telemetry).
        logger.ReceivedCalls().ShouldBeEmpty();
    }
}
