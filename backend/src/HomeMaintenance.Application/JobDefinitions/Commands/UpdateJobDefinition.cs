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

        var audits = new AuditQueue();

        var error = ApplyRename(cmd, definition, audits)
            ?? ApplySchedule(cmd, definition, audits)
            ?? ApplyStepTemplateRemovals(cmd, definition, audits)
            ?? ApplyStepTemplateReorder(cmd, definition, audits)
            ?? ApplyStepTemplateEdits(cmd, definition, audits)
            ?? ApplyStepTemplateAdditions(cmd, definition, audits);
        if (error is not null)
            return Result<JobDefinitionDto>.Failure(error);

        await _definitions.UpdateAsync(definition, ct);

        foreach (var (eventType, payload) in audits)
            await EmitAudit(eventType, cmd.Id, owner, payload, ct);

        return Result<JobDefinitionDto>.Success(definition.ToDto());
    }

    private static Error? ApplyRename(UpdateJobDefinitionCommand cmd, JobDefinition definition, AuditQueue audits)
    {
        if (cmd.Name is null)
            return null;

        var error = AsValidationError(() => definition.Rename(cmd.Name), "name");
        if (error is not null)
            return error;

        audits.Add(AuditEventTypes.JobDefinitionRenamed, new Dictionary<string, object?> { ["name"] = cmd.Name });
        return null;
    }

    private static Error? ApplySchedule(UpdateJobDefinitionCommand cmd, JobDefinition definition, AuditQueue audits)
    {
        if (cmd.Schedule is null)
            return null;

        ScheduleDefinition schedule;
        try { schedule = CreateJobDefinitionHandler.ParseSchedule(cmd.Schedule); }
        catch (ArgumentException ex) { return new ValidationError("schedule", ex.Message); }

        definition.UpdateSchedule(schedule);
        audits.Add(AuditEventTypes.JobDefinitionScheduleChanged, new Dictionary<string, object?> { ["schedule"] = cmd.Schedule });
        return null;
    }

    private static Error? ApplyStepTemplateRemovals(UpdateJobDefinitionCommand cmd, JobDefinition definition, AuditQueue audits)
    {
        if (cmd.RemoveStepTemplateIds is null)
            return null;

        foreach (var id in cmd.RemoveStepTemplateIds)
        {
            if (definition.RemoveStepTemplate(id) == StepTemplateMutationOutcome.StepTemplateNotFound)
                return new NotFoundError("StepTemplate", id);
        }

        audits.AddStepTemplateMutation(StepTemplateMutationType.Removed);
        return null;
    }

    private static Error? ApplyStepTemplateReorder(UpdateJobDefinitionCommand cmd, JobDefinition definition, AuditQueue audits)
    {
        if (cmd.ReorderStepTemplateIds is null)
            return null;

        var message = definition.ReorderStepTemplates(cmd.ReorderStepTemplateIds) switch
        {
            ReorderStepTemplatesOutcome.WrongCount => "Must list every existing step template id exactly once.",
            ReorderStepTemplatesOutcome.DuplicateId => "Duplicate step template id in the order list.",
            ReorderStepTemplatesOutcome.UnknownId => "Unknown step template id in the order list.",
            _ => null,
        };
        if (message is not null)
            return new ValidationError("reorderStepTemplateIds", message);

        audits.AddStepTemplateMutation(StepTemplateMutationType.Reordered);
        return null;
    }

    private static Error? ApplyStepTemplateEdits(UpdateJobDefinitionCommand cmd, JobDefinition definition, AuditQueue audits)
    {
        if (cmd.EditStepTemplates is null)
            return null;

        foreach (var edit in cmd.EditStepTemplates)
        {
            var (outcome, error) = TryMutate(() => definition.EditStepTemplateDescription(edit.Id, edit.Description), "description");
            if (error is not null)
                return error;
            if (outcome == StepTemplateMutationOutcome.StepTemplateNotFound)
                return new NotFoundError("StepTemplate", edit.Id);
        }

        audits.AddStepTemplateMutation(StepTemplateMutationType.Edited);
        return null;
    }

    private static Error? ApplyStepTemplateAdditions(UpdateJobDefinitionCommand cmd, JobDefinition definition, AuditQueue audits)
    {
        if (cmd.AddStepDescriptions is null)
            return null;

        foreach (var description in cmd.AddStepDescriptions)
        {
            var error = AsValidationError(() => definition.AddStepTemplate(description), "description");
            if (error is not null)
                return error;
        }

        audits.AddStepTemplateMutation(StepTemplateMutationType.Added);
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

    private static bool IsEmptyRequest(UpdateJobDefinitionCommand cmd)
        => cmd.Name is null
            && cmd.Schedule is null
            && cmd.AddStepDescriptions is null
            && cmd.RemoveStepTemplateIds is null
            && cmd.ReorderStepTemplateIds is null
            && cmd.EditStepTemplates is null;

    /// <summary>Queues audit events to emit only after the whole update succeeds.</summary>
    private sealed class AuditQueue : List<(string EventType, Dictionary<string, object?> Payload)>
    {
        public void Add(string eventType, Dictionary<string, object?> payload) => Add((eventType, payload));

        public void AddStepTemplateMutation(string mutationType)
            => Add(AuditEventTypes.JobDefinitionStepTemplateMutated, new Dictionary<string, object?> { ["mutation_type"] = mutationType });
    }

    /// <summary>Tags for the step-template "mutation_type" audit payload field.</summary>
    private static class StepTemplateMutationType
    {
        public const string Removed = "removed";
        public const string Reordered = "reordered";
        public const string Edited = "edited";
        public const string Added = "added";
    }

    /// <summary>Runs <paramref name="mutate"/>, translating a domain ArgumentException into a ValidationError.</summary>
    private static Error? AsValidationError(Action mutate, string fallbackField)
    {
        try { mutate(); return null; }
        catch (ArgumentException ex) { return new ValidationError(ex.ParamName ?? fallbackField, ex.Message); }
    }

    /// <summary>Runs <paramref name="mutate"/>, returning its value or translating a domain ArgumentException into a ValidationError.</summary>
    private static (T? Value, Error? Error) TryMutate<T>(Func<T> mutate, string fallbackField)
    {
        try { return (mutate(), null); }
        catch (ArgumentException ex) { return (default, new ValidationError(ex.ParamName ?? fallbackField, ex.Message)); }
    }
}
