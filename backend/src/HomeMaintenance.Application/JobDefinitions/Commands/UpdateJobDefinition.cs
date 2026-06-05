using HomeMaintenance.Application.Common;
using HomeMaintenance.Application.Common.Interfaces;
using HomeMaintenance.Application.JobDefinitions.Dto;
using HomeMaintenance.Domain.Identity;
using HomeMaintenance.Domain.JobDefinitions;

namespace HomeMaintenance.Application.JobDefinitions.Commands;

public sealed record UpdateJobDefinitionCommand(
    string Id,
    OwnerId Owner,
    string? Name = null,
    ScheduleDefinitionDto? Schedule = null,
    IReadOnlyList<string>? AddStepDescriptions = null,
    IReadOnlyList<string>? RemoveStepTemplateIds = null,
    IReadOnlyList<string>? ReorderStepTemplateIds = null,
    IReadOnlyList<(string Id, string Description)>? EditStepTemplates = null);

public sealed class UpdateJobDefinitionHandler
{
    private readonly IJobDefinitionRepository _definitions;
    private readonly IAuditLog _audit;
    private readonly ICorrelationContext _correlation;

    public UpdateJobDefinitionHandler(
        IJobDefinitionRepository definitions,
        IAuditLog audit,
        ICorrelationContext correlation)
    {
        _definitions = definitions;
        _audit = audit;
        _correlation = correlation;
    }

    public async Task<Result<JobDefinitionDto>> Handle(
        UpdateJobDefinitionCommand cmd,
        CancellationToken ct = default)
    {
        var definition = await _definitions.GetAsync(cmd.Id, cmd.Owner, ct);
        if (definition is null)
            return Result<JobDefinitionDto>.Failure(new NotFoundError("JobDefinition", cmd.Id));

        if (cmd.Name is not null)
        {
            try { definition.Rename(cmd.Name); }
            catch (ArgumentException ex)
            {
                return Result<JobDefinitionDto>.Failure(new ValidationError(ex.ParamName ?? "name", ex.Message));
            }
            await EmitAudit(AuditEventTypes.JobDefinitionRenamed, cmd, new Dictionary<string, object?> { ["name"] = cmd.Name }, ct);
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
            await EmitAudit(AuditEventTypes.JobDefinitionScheduleChanged, cmd, new Dictionary<string, object?> { ["schedule"] = cmd.Schedule }, ct);
        }

        if (cmd.RemoveStepTemplateIds is not null)
        {
            foreach (var id in cmd.RemoveStepTemplateIds)
            {
                var outcome = definition.RemoveStepTemplate(id);
                if (outcome == StepTemplateMutationOutcome.StepTemplateNotFound)
                    return Result<JobDefinitionDto>.Failure(new NotFoundError("StepTemplate", id));
            }
            await EmitAudit(AuditEventTypes.JobDefinitionStepTemplateMutated, cmd, new Dictionary<string, object?> { ["mutation_type"] = "removed" }, ct);
        }

        if (cmd.ReorderStepTemplateIds is not null)
        {
            var outcome = definition.ReorderStepTemplates(cmd.ReorderStepTemplateIds);
            if (outcome != ReorderStepTemplatesOutcome.Success)
                return Result<JobDefinitionDto>.Failure(new ValidationError("reorderStepTemplateIds", outcome.ToString()));
            await EmitAudit(AuditEventTypes.JobDefinitionStepTemplateMutated, cmd, new Dictionary<string, object?> { ["mutation_type"] = "reordered" }, ct);
        }

        if (cmd.EditStepTemplates is not null)
        {
            foreach (var (id, description) in cmd.EditStepTemplates)
            {
                var outcome = definition.EditStepTemplateDescription(id, description);
                if (outcome == StepTemplateMutationOutcome.StepTemplateNotFound)
                    return Result<JobDefinitionDto>.Failure(new NotFoundError("StepTemplate", id));
            }
            await EmitAudit(AuditEventTypes.JobDefinitionStepTemplateMutated, cmd, new Dictionary<string, object?> { ["mutation_type"] = "edited" }, ct);
        }

        if (cmd.AddStepDescriptions is not null)
        {
            foreach (var description in cmd.AddStepDescriptions)
                definition.AddStepTemplate(description);
            await EmitAudit(AuditEventTypes.JobDefinitionStepTemplateMutated, cmd, new Dictionary<string, object?> { ["mutation_type"] = "added" }, ct);
        }

        await _definitions.UpdateAsync(definition, ct);
        return Result<JobDefinitionDto>.Success(definition.ToDto());
    }

    private Task EmitAudit(string eventType, UpdateJobDefinitionCommand cmd, Dictionary<string, object?> payload, CancellationToken ct)
        => _audit.RecordAsync(new AuditEvent(
            eventType,
            cmd.Owner.Value,
            $"job_definition:{cmd.Id}",
            DateTime.UtcNow,
            _correlation.CurrentId,
            payload), ct);
}
