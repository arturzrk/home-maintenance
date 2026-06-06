using HomeMaintenance.Application.Common;
using HomeMaintenance.Application.Common.Interfaces;
using HomeMaintenance.Application.JobDefinitions.Dto;
using HomeMaintenance.Domain.JobDefinitions;

namespace HomeMaintenance.Application.JobDefinitions.Commands;

public sealed record CreateJobDefinitionCommand(
    string PropertyId,
    string Name,
    ScheduleDefinitionDto Schedule,
    IReadOnlyList<string> StepDescriptions);

public sealed class CreateJobDefinitionHandler
{
    private readonly IJobDefinitionRepository _definitions;
    private readonly IPropertyRepository _properties;
    private readonly IIdentityProvider _identity;
    private readonly IDateTimeProvider _clock;
    private readonly JobGenerationService _generationService;
    private readonly IAuditLog _audit;
    private readonly ICorrelationContext _correlation;

    public CreateJobDefinitionHandler(
        IJobDefinitionRepository definitions,
        IPropertyRepository properties,
        IIdentityProvider identity,
        IDateTimeProvider clock,
        JobGenerationService generationService,
        IAuditLog audit,
        ICorrelationContext correlation)
    {
        _definitions = definitions;
        _properties = properties;
        _identity = identity;
        _clock = clock;
        _generationService = generationService;
        _audit = audit;
        _correlation = correlation;
    }

    public async Task<Result<JobDefinitionDto>> Handle(
        CreateJobDefinitionCommand cmd,
        CancellationToken ct = default)
    {
        var owner = _identity.CurrentOwner;

        var property = await _properties.GetAsync(cmd.PropertyId, owner, ct);
        if (property is null)
            return Result<JobDefinitionDto>.Failure(new NotFoundError("Property", cmd.PropertyId));

        ScheduleDefinition schedule;
        try
        {
            schedule = ParseSchedule(cmd.Schedule);
        }
        catch (Exception ex)
        {
            return Result<JobDefinitionDto>.Failure(new ValidationError("schedule", ex.Message));
        }

        JobDefinition definition;
        try
        {
            definition = JobDefinition.Create(
                IdFactory.NewId(),
                owner,
                cmd.PropertyId,
                cmd.Name,
                schedule,
                cmd.StepDescriptions);
        }
        catch (ArgumentException ex)
        {
            return Result<JobDefinitionDto>.Failure(new ValidationError(ex.ParamName ?? "request", ex.Message));
        }

        await _definitions.AddAsync(definition, ct);
        await _generationService.GenerateForDefinition(definition, _clock.UtcToday, ct);

        await _audit.RecordAsync(new AuditEvent(
            AuditEventTypes.JobDefinitionCreated,
            owner.Value,
            $"job_definition:{definition.Id}",
            DateTime.UtcNow,
            _correlation.CurrentId,
            new Dictionary<string, object?>
            {
                ["propertyId"] = cmd.PropertyId,
                ["name"] = definition.Name,
                ["stepCount"] = definition.StepTemplates.Count,
                ["schedule"] = cmd.Schedule,
            }), ct);

        return Result<JobDefinitionDto>.Success(definition.ToDto());
    }

    internal static ScheduleDefinition ParseSchedule(ScheduleDefinitionDto dto)
    {
        if (!Enum.TryParse<CadenceUnit>(dto.Unit, ignoreCase: true, out var unit))
            throw new ArgumentException($"Unknown cadence unit: {dto.Unit}", nameof(dto));
        return new ScheduleDefinition(unit, dto.Multiplier, dto.StartDate, dto.EndDate);
    }
}
