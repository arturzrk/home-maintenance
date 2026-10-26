using HomeMaintenance.Application.Common;
using HomeMaintenance.Application.Common.Interfaces;
using HomeMaintenance.Application.JobDefinitions.Dto;
using HomeMaintenance.Domain.Identity;
using HomeMaintenance.Domain.JobDefinitions;

namespace HomeMaintenance.Application.JobDefinitions.Commands;

public sealed record CreateJobDefinitionCommand(
    string PropertyId,
    string Name,
    ScheduleDefinitionDto Schedule,
    IReadOnlyList<string> StepDescriptions,
    string? AssetId = null);

public sealed class CreateJobDefinitionHandler
{
    private readonly IJobDefinitionRepository _definitions;
    private readonly IPropertyRepository _properties;
    private readonly IAssetRepository _assets;
    private readonly IIdentityProvider _identity;
    private readonly IDateTimeProvider _clock;
    private readonly JobGenerationService _generationService;
    private readonly IAuditLog _audit;
    private readonly ICorrelationContext _correlation;

    public CreateJobDefinitionHandler(
        IJobDefinitionRepository definitions,
        IPropertyRepository properties,
        IAssetRepository assets,
        IIdentityProvider identity,
        IDateTimeProvider clock,
        JobGenerationService generationService,
        IAuditLog audit,
        ICorrelationContext correlation)
    {
        _definitions = definitions;
        _properties = properties;
        _assets = assets;
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

        var guardError = await EnsurePropertyExists(cmd, owner, ct) ?? await ValidateAssetScope(cmd, owner, ct);
        if (guardError is not null)
            return Result<JobDefinitionDto>.Failure(guardError);

        var scheduleResult = TryCreate(() => ParseSchedule(cmd.Schedule), _ => "schedule");
        if (scheduleResult.IsFailure)
            return Result<JobDefinitionDto>.Failure(scheduleResult.Error!);

        var definitionResult = TryCreate(
            () => BuildDefinition(cmd, owner, scheduleResult.Value!),
            ex => ex.ParamName ?? "request");
        if (definitionResult.IsFailure)
            return Result<JobDefinitionDto>.Failure(definitionResult.Error!);
        var definition = definitionResult.Value!;

        await _definitions.AddAsync(definition, ct);
        await _generationService.GenerateForDefinition(definition, _clock.UtcToday, ct);
        await EmitCreatedAudit(cmd, definition, owner, ct);

        return Result<JobDefinitionDto>.Success(definition.ToDto());
    }

    private async Task<Error?> EnsurePropertyExists(CreateJobDefinitionCommand cmd, OwnerId owner, CancellationToken ct)
    {
        var property = await _properties.GetAsync(cmd.PropertyId, owner, ct);
        return property is null ? new NotFoundError("Property", cmd.PropertyId) : null;
    }

    private async Task<Error?> ValidateAssetScope(CreateJobDefinitionCommand cmd, OwnerId owner, CancellationToken ct)
    {
        if (cmd.AssetId is null)
            return null;
        return await Assets.AssetScopeValidation.CheckAsync(_assets, owner, cmd.PropertyId, cmd.AssetId, ct);
    }

    private static JobDefinition BuildDefinition(CreateJobDefinitionCommand cmd, OwnerId owner, ScheduleDefinition schedule)
        => JobDefinition.Create(
            IdFactory.NewId(),
            owner,
            cmd.PropertyId,
            cmd.Name,
            schedule,
            cmd.StepDescriptions,
            assetId: cmd.AssetId);

    private Task EmitCreatedAudit(CreateJobDefinitionCommand cmd, JobDefinition definition, OwnerId owner, CancellationToken ct)
        => _audit.RecordAsync(new AuditEvent(
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

    internal static ScheduleDefinition ParseSchedule(ScheduleDefinitionDto dto)
    {
        if (!Enum.TryParse<CadenceUnit>(dto.Unit, ignoreCase: true, out var unit))
            throw new ArgumentException($"Unknown cadence unit: {dto.Unit}", nameof(dto));
        return new ScheduleDefinition(unit, dto.Multiplier, dto.StartDate, dto.EndDate);
    }

    /// <summary>
    /// Runs <paramref name="create"/>, translating a domain ArgumentException into a
    /// ValidationError whose field is chosen by <paramref name="selectField"/> -- callers
    /// decide whether to trust the exception's own ParamName or report a fixed field name.
    /// </summary>
    private static Result<T> TryCreate<T>(Func<T> create, Func<ArgumentException, string> selectField)
    {
        try { return Result<T>.Success(create()); }
        catch (ArgumentException ex) { return Result<T>.Failure(new ValidationError(selectField(ex), ex.Message)); }
    }
}
