using HomeMaintenance.Application.Common;
using HomeMaintenance.Application.Common.Interfaces;
using HomeMaintenance.Application.JobDefinitions.Dto;
using HomeMaintenance.Domain.Identity;
using HomeMaintenance.Domain.JobDefinitions;

namespace HomeMaintenance.Application.JobDefinitions.Commands;

public sealed record UpdateJobDefinitionCommand(
    string Id,
    string? Name = null,
    ScheduleDefinitionDto? Schedule = null,
    IReadOnlyList<string>? AddStepDescriptions = null,
    IReadOnlyList<string>? RemoveStepTemplateIds = null,
    IReadOnlyList<string>? ReorderStepTemplateIds = null,
    IReadOnlyList<(string Id, string Description)>? EditStepTemplates = null);

public sealed class UpdateJobDefinitionHandler
{
    private readonly IJobDefinitionRepository _definitions;
    private readonly IIdentityProvider _identity;
    private readonly IAuditLog _audit;
    private readonly ICorrelationContext _correlation;

    public UpdateJobDefinitionHandler(
        IJobDefinitionRepository definitions,
        IIdentityProvider identity,
        IAuditLog audit,
        ICorrelationContext correlation)
    {
        _definitions = definitions;
        _identity = identity;
        _audit = audit;
        _correlation = correlation;
    }

    public async Task<Result<JobDefinitionDto>> Handle(
        UpdateJobDefinitionCommand cmd,
        CancellationToken ct = default)
    {
        var owner = _identity.CurrentOwner;

        var definition = await _definitions.GetAsync(cmd.Id, owner, ct);
        if (definition is null)
            return Result<JobDefinitionDto>.Failure(new NotFoundError("JobDefinition", cmd.Id));

        var pendingAudits = new List<(string EventType, Dictionary<string, object?> Payload)>();

        if (cmd.Name is not null)
        {
            try { definition.Rename(cmd.Name); }
            catch (ArgumentException ex)
            {
                return Result<JobDefinitionDto>.Failure(new ValidationError(ex.ParamName ?? "name", ex.Message));
            }
            pendingAudits.Add((AuditEventTypes.JobDefinitionRenamed, new Dictionary<string, object?> { ["name"] = cmd.Name }));
        }

        if (cmd.Schedule is not null)
        {
            ScheduleDefinition schedule;
            try { schedule = CreateJobDefinitionHandler.ParseSchedule(cmd.Schedule); }
            catch (Exception ex)
            {
                return Result<JobDefinitionDto>.Failure(new ValidationError("schedule", ex.Message));
            }
            definition.UpdateSchedule(schedule);
            pendingAudits.Add((AuditEventTypes.JobDefinitionScheduleChanged, new Dictionary<string, object?> { ["schedule"] = cmd.Schedule }));
        }

        if (cmd.RemoveStepTemplateIds is not null)
        {
            foreach (var id in cmd.RemoveStepTemplateIds)
            {
                var outcome = definition.RemoveStepTemplate(id);
                if (outcome == StepTemplateMutationOutcome.StepTemplateNotFound)
                    return Result<JobDefinitionDto>.Failure(new NotFoundError("StepTemplate", id));
            }
            pendingAudits.Add((AuditEventTypes.JobDefinitionStepTemplateMutated, new Dictionary<string, object?> { ["mutation_type"] = "removed" }));
        }

        if (cmd.ReorderStepTemplateIds is not null)
        {
            var outcome = definition.ReorderStepTemplates(cmd.ReorderStepTemplateIds);
            var error = outcome switch
            {
                ReorderStepTemplatesOutcome.WrongCount => "Must list every existing step template id exactly once.",
                ReorderStepTemplatesOutcome.DuplicateId => "Duplicate step template id in the order list.",
                ReorderStepTemplatesOutcome.UnknownId => "Unknown step template id in the order list.",
                _ => null,
            };
            if (error is not null)
                return Result<JobDefinitionDto>.Failure(new ValidationError("reorderStepTemplateIds", error));
            pendingAudits.Add((AuditEventTypes.JobDefinitionStepTemplateMutated, new Dictionary<string, object?> { ["mutation_type"] = "reordered" }));
        }

        if (cmd.EditStepTemplates is not null)
        {
            foreach (var (id, description) in cmd.EditStepTemplates)
            {
                StepTemplateMutationOutcome outcome;
                try { outcome = definition.EditStepTemplateDescription(id, description); }
                catch (ArgumentException ex)
                {
                    return Result<JobDefinitionDto>.Failure(new ValidationError("description", ex.Message));
                }
                if (outcome == StepTemplateMutationOutcome.StepTemplateNotFound)
                    return Result<JobDefinitionDto>.Failure(new NotFoundError("StepTemplate", id));
            }
            pendingAudits.Add((AuditEventTypes.JobDefinitionStepTemplateMutated, new Dictionary<string, object?> { ["mutation_type"] = "edited" }));
        }

        if (cmd.AddStepDescriptions is not null)
        {
            foreach (var description in cmd.AddStepDescriptions)
            {
                try { definition.AddStepTemplate(description); }
                catch (ArgumentException ex)
                {
                    return Result<JobDefinitionDto>.Failure(new ValidationError("description", ex.Message));
                }
            }
            pendingAudits.Add((AuditEventTypes.JobDefinitionStepTemplateMutated, new Dictionary<string, object?> { ["mutation_type"] = "added" }));
        }

        await _definitions.UpdateAsync(definition, ct);

        foreach (var (eventType, payload) in pendingAudits)
            await EmitAudit(eventType, cmd.Id, owner, payload, ct);

        return Result<JobDefinitionDto>.Success(definition.ToDto());
    }

    private Task EmitAudit(string eventType, string definitionId, OwnerId owner, Dictionary<string, object?> payload, CancellationToken ct)
        => _audit.RecordAsync(new AuditEvent(
            eventType,
            owner.Value,
            $"job_definition:{definitionId}",
            DateTime.UtcNow,
            _correlation.CurrentId,
            payload), ct);
}
