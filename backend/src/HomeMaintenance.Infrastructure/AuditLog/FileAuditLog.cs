using System.Text.Json;
using System.Text.Json.Serialization;
using HomeMaintenance.Application.Common.Interfaces;
using Microsoft.Extensions.Options;

namespace HomeMaintenance.Infrastructure.AuditLog;

/// <summary>
/// File-backed audit log writer. Appends one JSON object per line to
/// the configured sink path under a <see cref="SemaphoreSlim"/> so
/// concurrent writers don't interleave. Acceptable for personal-scale
/// Slice 1; production swaps in a managed sink behind the same
/// <see cref="IAuditLog"/> interface (constitution Phase 2 + research.md R7).
/// </summary>
public sealed class FileAuditLog : IAuditLog
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false,
    };

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly string _path;

    public FileAuditLog(IOptions<AuditLogOptions> options)
    {
        _path = options.Value.SinkPath;
        var dir = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
    }

    public async Task RecordAsync(AuditEvent evt, CancellationToken ct = default)
    {
        var line = JsonSerializer.Serialize(evt, JsonOptions);
        await _gate.WaitAsync(ct);
        try
        {
            await File.AppendAllLinesAsync(_path, new[] { line }, ct);
        }
        finally
        {
            _gate.Release();
        }
    }
}

/// <summary>
/// Bound from configuration section <c>AuditLog</c>. Defaults to
/// <c>audit-trail/property-job-step.jsonl</c> relative to the host's
/// content root, which the constitution declares gitignored.
/// </summary>
public sealed class AuditLogOptions
{
    public const string SectionName = "AuditLog";
    public string SinkPath { get; set; } = "audit-trail/property-job-step.jsonl";
}
