using System.Text.Json;
using HomeMaintenance.Application.Common.Interfaces;
using HomeMaintenance.Infrastructure.AuditLog;
using Microsoft.Extensions.Options;
using Shouldly;

namespace HomeMaintenance.Unit.Tests.Infrastructure.AuditLog;

public sealed class FileAuditLogTests : IDisposable
{
    private readonly string _tempDir;

    public FileAuditLogTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"audit-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public async Task Append_WritesValidJsonLine()
    {
        var path = Path.Combine(_tempDir, "trail.jsonl");
        var log = NewLog(path);
        var evt = NewEvent("property.created", "alice", "property:p1");

        await log.RecordAsync(evt);

        var lines = await File.ReadAllLinesAsync(path);
        lines.Length.ShouldBe(1);

        var parsed = JsonSerializer.Deserialize<AuditEvent>(lines[0]);
        parsed.ShouldNotBeNull();
        parsed!.EventType.ShouldBe("property.created");
        parsed.Actor.ShouldBe("alice");
        parsed.Target.ShouldBe("property:p1");
        parsed.CorrelationId.ShouldBe(evt.CorrelationId);
    }

    [Fact]
    public async Task Append_ConcurrentCalls_AllRecordsPresent()
    {
        var path = Path.Combine(_tempDir, "concurrent.jsonl");
        var log = NewLog(path);

        var tasks = Enumerable.Range(0, 100)
            .Select(i => log.RecordAsync(NewEvent("step.ticked", "alice", $"step:{i}")));
        await Task.WhenAll(tasks);

        var lines = await File.ReadAllLinesAsync(path);
        lines.Length.ShouldBe(100);

        // Every line is valid JSON and the target ids are all present.
        var targets = lines
            .Select(l => JsonSerializer.Deserialize<AuditEvent>(l)!.Target)
            .ToHashSet();
        targets.Count.ShouldBe(100);
    }

    [Fact]
    public async Task Append_AfterRestart_AppendsNotTruncates()
    {
        var path = Path.Combine(_tempDir, "restart.jsonl");

        var first = NewLog(path);
        await first.RecordAsync(NewEvent("auth.signin_success", "alice", null));

        var second = NewLog(path);
        await second.RecordAsync(NewEvent("auth.signin_success", "bob", null));

        var lines = await File.ReadAllLinesAsync(path);
        lines.Length.ShouldBe(2);

        JsonSerializer.Deserialize<AuditEvent>(lines[0])!.Actor.ShouldBe("alice");
        JsonSerializer.Deserialize<AuditEvent>(lines[1])!.Actor.ShouldBe("bob");
    }

    [Fact]
    public async Task Append_OmitsNullPayloadField()
    {
        var path = Path.Combine(_tempDir, "no-payload.jsonl");
        var log = NewLog(path);

        await log.RecordAsync(NewEvent("auth.signin_success", "alice", null));

        var raw = await File.ReadAllTextAsync(path);
        raw.ShouldNotContain("\"Payload\":null");
        raw.ShouldNotContain("\"payload\":null");
    }

    private static FileAuditLog NewLog(string path)
        => new(Options.Create(new AuditLogOptions { SinkPath = path }));

    private static AuditEvent NewEvent(string eventType, string actor, string? target)
        => new(eventType, actor, target, DateTime.UtcNow, Guid.NewGuid().ToString("N"));
}
