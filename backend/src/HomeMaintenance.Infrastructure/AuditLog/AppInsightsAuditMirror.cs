using System.Diagnostics;
using HomeMaintenance.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;

namespace HomeMaintenance.Infrastructure.AuditLog;

/// <summary>
/// Decorator around any <see cref="IAuditLog"/> that adds two
/// cross-cutting behaviours for the Mongo production path:
///
/// <list type="number">
/// <item><description>
/// <b>Failure semantics</b>: if the primary audit insert fails the
/// exception is caught, logged at Error (so an audit gap is never
/// silent), and <em>not</em> re-thrown — the domain request still
/// succeeds. Returning 5xx for a non-idempotent command (e.g. property
/// creation) would invite a duplicate-creating retry, which is worse
/// than a missing audit row at personal scale. Upgrade path: both
/// domain writes and audit writes share the same database, so a
/// multi-document transaction or outbox can be introduced later without
/// schema changes.
/// </description></item>
/// <item><description>
/// <b>App Insights mirror</b> (best-effort): on success, emits an
/// OpenTelemetry <see cref="Activity"/> carrying only the audit
/// envelope (EventType, Actor, Target, Timestamp, CorrelationId).
/// <b>Payload is intentionally omitted</b> — it may contain
/// user-entered content that must not be forwarded to a store with
/// different access/retention than the privacy policy describes.
/// The activity is a no-op when no OTel listener is registered
/// (i.e. when <c>APPLICATIONINSIGHTS_CONNECTION_STRING</c> is unset),
/// so local dev is unaffected.
/// </description></item>
/// </list>
/// </summary>
public sealed class AppInsightsAuditMirror : IAuditLog
{
    /// <summary>
    /// OTel source name. Registered via <c>AddSource</c> in Program.cs
    /// when the Azure Monitor distro is active. When no listener
    /// subscribes, <see cref="ActivitySource.StartActivity"/> returns
    /// null and the mirror is a true no-op.
    /// </summary>
    public const string ActivitySourceName = "HomeMaintenance.Audit";

    private static readonly ActivitySource AuditSource = new(ActivitySourceName);

    private readonly IAuditLog _inner;
    private readonly ILogger<AppInsightsAuditMirror> _logger;

    public AppInsightsAuditMirror(
        IAuditLog inner,
        ILogger<AppInsightsAuditMirror> logger)
    {
        _inner = inner;
        _logger = logger;
    }

    public async Task RecordAsync(AuditEvent evt, CancellationToken ct = default)
    {
        // ── Primary write ────────────────────────────────────────────────
        try
        {
            await _inner.RecordAsync(evt, ct);
        }
        catch (Exception ex)
        {
            // Log at Error so the gap is observable in App Insights and
            // any other log sink; include the correlation id so the
            // missing event can be correlated to the request trace.
            _logger.LogError(
                ex,
                "Audit insert failed for {EventType} (Actor={Actor}, CorrelationId={CorrelationId}). " +
                "The domain mutation has already committed. " +
                "Investigate and replay the event manually if required.",
                evt.EventType, evt.Actor, evt.CorrelationId);

            // Documented decision: do NOT re-throw. The domain mutation
            // is durable; a 5xx here would invite a non-idempotent retry.
            return;
        }

        // ── App Insights mirror (best-effort) ───────────────────────────
        // Envelope only — Payload is never forwarded.
        try
        {
            using var activity = AuditSource.StartActivity(evt.EventType, ActivityKind.Internal);
            if (activity is not null)
            {
                activity.SetTag("audit.actor",         evt.Actor);
                activity.SetTag("audit.target",        evt.Target);
                activity.SetTag("audit.correlationId", evt.CorrelationId);
                activity.SetTag("audit.timestamp",     evt.Timestamp.ToString("O"));
                // Payload intentionally NOT included — privacy boundary.
            }
        }
        catch (Exception ex)
        {
            // Mirror failures are swallowed; the Mongo write is
            // the system of record and has already succeeded.
            _logger.LogDebug(ex,
                "App Insights mirror failed for event {EventType} ({CorrelationId}) — primary write was successful",
                evt.EventType, evt.CorrelationId);
        }
    }
}
