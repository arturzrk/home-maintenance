using HomeMaintenance.Application.Common;
using HomeMaintenance.Application.Common.Interfaces;
using HomeMaintenance.Application.Jobs.Dto;
using HomeMaintenance.Domain.Jobs;

namespace HomeMaintenance.Application.Jobs.Commands;

public sealed record CreateJobCommand(
    string PropertyId,
    string Name,
    DateOnly? DueDate,
    IReadOnlyList<string> StepDescriptions,
    string? AssetId = null);

public sealed class CreateJobHandler
{
    private readonly IJobRepository _jobs;
    private readonly IPropertyRepository _properties;
    private readonly IAssetRepository _assets;
    private readonly IIdentityProvider _identity;
    private readonly IAuditLog _audit;
    private readonly ICorrelationContext _correlation;

    public CreateJobHandler(
        IJobRepository jobs,
        IPropertyRepository properties,
        IAssetRepository assets,
        IIdentityProvider identity,
        IAuditLog audit,
        ICorrelationContext correlation)
    {
        _jobs = jobs;
        _properties = properties;
        _assets = assets;
        _identity = identity;
        _audit = audit;
        _correlation = correlation;
    }

    public async Task<Result<JobDetailDto>> Handle(
        CreateJobCommand cmd,
        CancellationToken ct = default)
    {
        var owner = _identity.CurrentOwner;

        // Cross-aggregate ownership check: caller must own the parent
        // Property. Returning NotFoundError matches the no-leak rule
        // for unknown vs. not-owned (FR-008).
        var property = await _properties.GetAsync(cmd.PropertyId, owner, ct);
        if (property is null)
            return Result<JobDetailDto>.Failure(new NotFoundError("Property", cmd.PropertyId));

        if (cmd.AssetId is not null)
        {
            var scopeError = await Assets.AssetScopeValidation.CheckAsync(
                _assets, owner, cmd.PropertyId, cmd.AssetId, ct);
            if (scopeError is not null)
                return Result<JobDetailDto>.Failure(scopeError);
        }

        Job job;
        try
        {
            job = Job.Create(
                IdFactory.NewId(),
                owner,
                cmd.PropertyId,
                cmd.Name,
                cmd.DueDate,
                cmd.StepDescriptions,
                assetId: cmd.AssetId);
        }
        catch (ArgumentException ex)
        {
            return Result<JobDetailDto>.Failure(new ValidationError(ex.ParamName ?? "request", ex.Message));
        }

        await _jobs.AddAsync(job, ct);

        await _audit.RecordAsync(new AuditEvent(
            AuditEventTypes.JobCreated,
            owner.Value,
            $"job:{job.Id}",
            DateTime.UtcNow,
            _correlation.CurrentId,
            new Dictionary<string, object?>
            {
                ["propertyId"] = cmd.PropertyId,
                ["name"] = job.Name,
                ["dueDate"] = job.DueDate?.ToString("O"),
                ["stepCount"] = job.Steps.Count,
            }), ct);

        return Result<JobDetailDto>.Success(job.ToDetailDto());
    }
}
