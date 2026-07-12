using HomeMaintenance.Application.Common;
using HomeMaintenance.Application.Common.Interfaces;
using HomeMaintenance.Domain.Jobs;
using HomeMaintenance.Domain.JobDefinitions;

namespace HomeMaintenance.Application.JobDefinitions;

public sealed class JobGenerationService
{
    private readonly IJobRepository _jobs;
    private readonly IAuditLog _audit;
    private readonly ICorrelationContext _correlation;

    public JobGenerationService(IJobRepository jobs, IAuditLog audit, ICorrelationContext correlation)
    {
        _jobs = jobs;
        _audit = audit;
        _correlation = correlation;
    }

    public async Task GenerateForDefinition(
        JobDefinition definition,
        DateOnly today,
        CancellationToken ct = default)
    {
        var horizon = today.AddMonths(3);
        var occurrences = definition.Schedule.OccurrencesInRange(today, horizon);

        foreach (var occurrence in occurrences)
        {
            if (await _jobs.HasGeneratedJobForOccurrenceAsync(definition.Id, occurrence, ct))
                continue;

            var job = Job.Create(
                IdFactory.NewId(),
                definition.Owner,
                definition.PropertyId,
                definition.Name,
                occurrence,
                definition.StepTemplates.Select(st => st.Description),
                definition.Id,
                assetId: definition.AssetId);

            await _jobs.AddAsync(job, ct);

            await _audit.RecordAsync(new AuditEvent(
                AuditEventTypes.JobGenerated,
                "system",
                $"job:{job.Id}",
                DateTime.UtcNow,
                _correlation.CurrentId,
                new Dictionary<string, object?>
                {
                    ["trigger"] = "scheduler",
                    ["definitionId"] = definition.Id,
                    ["occurrenceDate"] = occurrence.ToString("O"),
                }), ct);
        }
    }
}
