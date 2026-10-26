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
    IReadOnlyList<StepTemplateEdit>? EditStepTemplates = null);

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
        if (IsEmptyRequest(cmd))
            return Result<JobDefinitionDto>.Failure(
                new ValidationError("request", "Provide at least one change to apply."));

        var owner = _identity.CurrentOwner;

        var definition = await _definitions.GetAsync(cmd.Id, owner, ct);
        if (definition is null)
            return Result<JobDefinitionDto>.Failure(new NotFoundError("JobDefinition", cmd.Id));

        var pendingAudits = new List<(string EventType, Dictionary<string, object?> Payload)>();

        var error = ApplyRename(cmd, definition, pendingAudits)
            ?? ApplySchedule(cmd, definition, pendingAudits)
            ?? ApplyStepTemplateRemovals(cmd, definition, pendingAudits)
            ?? ApplyStepTemplateReorder(cmd, definition, pendingAudits)
            ?? ApplyStepTemplateEdits(cmd, definition, pendingAudits)
            ?? ApplyStepTemplateAdditions(cmd, definition, pendingAudits);
        if (error is not null)
            return Result<JobDefinitionDto>.Failure(error);

        await _definitions.UpdateAsync(definition, ct);

        foreach (var (eventType, payload) in pendingAudits)
            await EmitAudit(eventType, cmd.Id, owner, payload, ct);

        return Result<JobDefinitionDto>.Success(definition.ToDto());
    }

    private static bool IsEmptyRequest(UpdateJobDefinitionCommand cmd)
        => cmd.Name is null
            && cmd.Schedule is null
            && cmd.AddStepDescriptions is null
            && cmd.RemoveStepTemplateIds is null
            && cmd.ReorderStepTemplateIds is null
            && cmd.EditStepTemplates is null;

    private static Error? ApplyRename(
        UpdateJobDefinitionCommand cmd,
        JobDefinition definition,
        List<(string EventType, Dictionary<string, object?> Payload)> pendingAudits)
    {
        if (cmd.Name is null)
            return null;

        try { definition.Rename(cmd.Name); }
        catch (ArgumentException ex)
        {
            return new ValidationError(ex.ParamName ?? "name", ex.Message);
        }
        pendingAudits.Add((AuditEventTypes.JobDefinitionRenamed, new Dictionary<string, object?> { ["name"] = cmd.Name }));
        return null;
    }

    private static Error? ApplySchedule(
        UpdateJobDefinitionCommand cmd,
        JobDefinition definition,
        List<(string EventType, Dictionary<string, object?> Payload)> pendingAudits)
    {
        if (cmd.Schedule is null)
            return null;

        ScheduleDefinition schedule;
        try { schedule = CreateJobDefinitionHandler.ParseSchedule(cmd.Schedule); }
        catch (ArgumentException ex)
        {
            return new ValidationError("schedule", ex.Message);
        }
        definition.UpdateSchedule(schedule);
        pendingAudits.Add((AuditEventTypes.JobDefinitionScheduleChanged, new Dictionary<string, object?> { ["schedule"] = cmd.Schedule }));
        return null;
    }

    private static Error? ApplyStepTemplateRemovals(
        UpdateJobDefinitionCommand cmd,
        JobDefinition definition,
        List<(string EventType, Dictionary<string, object?> Payload)> pendingAudits)
    {
        if (cmd.RemoveStepTemplateIds is null)
            return null;

        foreach (var id in cmd.RemoveStepTemplateIds)
        {
            var outcome = definition.RemoveStepTemplate(id);
            if (outcome == StepTemplateMutationOutcome.StepTemplateNotFound)
                return new NotFoundError("StepTemplate", id);
        }
        pendingAudits.Add((AuditEventTypes.JobDefinitionStepTemplateMutated, new Dictionary<string, object?> { ["mutation_type"] = "removed" }));
        return null;
    }

    private static Error? ApplyStepTemplateReorder(
        UpdateJobDefinitionCommand cmd,
        JobDefinition definition,
        List<(string EventType, Dictionary<string, object?> Payload)> pendingAudits)
    {
        if (cmd.ReorderStepTemplateIds is null)
            return null;

        var outcome = definition.ReorderStepTemplates(cmd.ReorderStepTemplateIds);
        var message = outcome switch
        {
            ReorderStepTemplatesOutcome.WrongCount => "Must list every existing step template id exactly once.",
            ReorderStepTemplatesOutcome.DuplicateId => "Duplicate step template id in the order list.",
            ReorderStepTemplatesOutcome.UnknownId => "Unknown step template id in the order list.",
            _ => null,
        };
        if (message is not null)
            return new ValidationError("reorderStepTemplateIds", message);
        pendingAudits.Add((AuditEventTypes.JobDefinitionStepTemplateMutated, new Dictionary<string, object?> { ["mutation_type"] = "reordered" }));
        return null;
    }

    private static Error? ApplyStepTemplateEdits(
        UpdateJobDefinitionCommand cmd,
        JobDefinition definition,
        List<(string EventType, Dictionary<string, object?> Payload)> pendingAudits)
    {
        if (cmd.EditStepTemplates is null)
            return null;

        foreach (var edit in cmd.EditStepTemplates)
        {
            StepTemplateMutationOutcome outcome;
            try { outcome = definition.EditStepTemplateDescription(edit.Id, edit.Description); }
            catch (ArgumentException ex)
            {
                return new ValidationError("description", ex.Message);
            }
            if (outcome == StepTemplateMutationOutcome.StepTemplateNotFound)
                return new NotFoundError("StepTemplate", edit.Id);
        }
        pendingAudits.Add((AuditEventTypes.JobDefinitionStepTemplateMutated, new Dictionary<string, object?> { ["mutation_type"] = "edited" }));
        return null;
    }

    private static Error? ApplyStepTemplateAdditions(
        UpdateJobDefinitionCommand cmd,
        JobDefinition definition,
        List<(string EventType, Dictionary<string, object?> Payload)> pendingAudits)
    {
        if (cmd.AddStepDescriptions is null)
            return null;

        foreach (var description in cmd.AddStepDescriptions)
        {
            try { definition.AddStepTemplate(description); }
            catch (ArgumentException ex)
            {
                return new ValidationError("description", ex.Message);
            }
        }
        pendingAudits.Add((AuditEventTypes.JobDefinitionStepTemplateMutated, new Dictionary<string, object?> { ["mutation_type"] = "added" }));
        return null;
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
