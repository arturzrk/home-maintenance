using HomeMaintenance.Application.Common;
using HomeMaintenance.Application.Common.Interfaces;
using HomeMaintenance.Application.Jobs;
using HomeMaintenance.Application.Jobs.Dto;
using HomeMaintenance.Domain.Jobs;

namespace HomeMaintenance.Application.JobDefinitions.Commands;

public sealed record GenerateNextOccurrenceCommand(string DefinitionId);

public sealed class GenerateNextOccurrenceHandler
{
    private readonly IJobDefinitionRepository _definitions;
    private readonly IJobRepository _jobs;
    private readonly IIdentityProvider _identity;
    private readonly IAuditLog _audit;
    private readonly ICorrelationContext _correlation;

    public GenerateNextOccurrenceHandler(
        IJobDefinitionRepository definitions,
        IJobRepository jobs,
        IIdentityProvider identity,
        IAuditLog audit,
        ICorrelationContext correlation)
    {
        _definitions = definitions;
        _jobs = jobs;
        _identity = identity;
        _audit = audit;
        _correlation = correlation;
    }

    public async Task<Result<JobDetailDto>> Handle(
        GenerateNextOccurrenceCommand cmd,
        CancellationToken ct = default)
    {
        var owner = _identity.CurrentOwner;

        var definition = await _definitions.GetAsync(cmd.DefinitionId, owner, ct);
        if (definition is null)
            return Result<JobDetailDto>.Failure(new NotFoundError("JobDefinition", cmd.DefinitionId));

        var latestDueDate = await _jobs.LatestGeneratedJobDueDateAsync(definition.Id, ct);

        DateOnly nextOccurrence;
        if (latestDueDate is null)
        {
            nextOccurrence = definition.Schedule.StartDate;
        }
        else
        {
            var candidates = definition.Schedule
                .OccurrencesInRange(latestDueDate.Value.AddDays(1), latestDueDate.Value.AddYears(10))
                .Take(1)
                .ToList();

            if (candidates.Count == 0)
                return Result<JobDetailDto>.Failure(
                    new BusinessRuleError("no_future_occurrence", "The schedule has no future occurrences."));

            nextOccurrence = candidates[0];
        }

        if (await _jobs.HasGeneratedJobForOccurrenceAsync(definition.Id, nextOccurrence, ct))
            return Result<JobDetailDto>.Failure(
                new BusinessRuleError("next_occurrence_already_exists", "A job for the next occurrence already exists."));

        var job = Job.Create(
            IdFactory.NewId(),
            definition.Owner,
            definition.PropertyId,
            definition.Name,
            nextOccurrence,
            definition.StepTemplates.Select(st => st.Description),
            definition.Id);

        await _jobs.AddAsync(job, ct);

        await _audit.RecordAsync(new AuditEvent(
            AuditEventTypes.JobGenerated,
            owner.Value,
            $"job:{job.Id}",
            DateTime.UtcNow,
            _correlation.CurrentId,
            new Dictionary<string, object?>
            {
                ["trigger"] = "manual",
                ["definitionId"] = definition.Id,
                ["occurrenceDate"] = nextOccurrence.ToString("O"),
            }), ct);

        return Result<JobDetailDto>.Success(job.ToDetailDto());
    }
}
