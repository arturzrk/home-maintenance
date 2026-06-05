using HomeMaintenance.Application.Common;
using HomeMaintenance.Application.Common.Interfaces;
using HomeMaintenance.Domain.Jobs;
using HomeMaintenance.Domain.JobDefinitions;

namespace HomeMaintenance.Application.JobDefinitions;

public sealed class JobGenerationService
{
    private readonly IJobRepository _jobs;
    private readonly IAuditLog _audit;

    public JobGenerationService(IJobRepository jobs, IAuditLog audit)
    {
        _jobs = jobs;
        _audit = audit;
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
                definition.Id);

            await _jobs.AddAsync(job, ct);

            await _audit.RecordAsync(new AuditEvent(
                AuditEventTypes.JobGenerated,
                "system",
                $"job:{job.Id}",
                DateTime.UtcNow,
                string.Empty,
                new Dictionary<string, object?>
                {
                    ["trigger"] = "scheduler",
                    ["definitionId"] = definition.Id,
                    ["occurrenceDate"] = occurrence.ToString("O"),
                }), ct);
        }
    }
}
