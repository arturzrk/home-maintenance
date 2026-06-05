using HomeMaintenance.Domain.Common;
using HomeMaintenance.Domain.Identity;

namespace HomeMaintenance.Domain.JobDefinitions;

public enum StepTemplateMutationOutcome { Success, StepTemplateNotFound }

public enum ReorderStepTemplatesOutcome { Success, WrongCount, DuplicateId, UnknownId }

public sealed class JobDefinition : Entity
{
    private readonly List<StepTemplate> _stepTemplates = new();

    public OwnerId Owner { get; private set; }
    public string PropertyId { get; private set; }
    public string Name { get; private set; }
    public ScheduleDefinition Schedule { get; private set; }

    public IReadOnlyList<StepTemplate> StepTemplates => _stepTemplates.AsReadOnly();

    private JobDefinition(string id, OwnerId owner, string propertyId, string name, ScheduleDefinition schedule)
        : base(id)
    {
        Owner = owner;
        PropertyId = propertyId;
        Name = name;
        Schedule = schedule;
    }

    public static JobDefinition Create(
        string id,
        OwnerId owner,
        string propertyId,
        string name,
        ScheduleDefinition schedule,
        IEnumerable<string> initialDescriptions)
    {
        ArgumentNullException.ThrowIfNull(owner);
        if (string.IsNullOrWhiteSpace(propertyId))
            throw new ArgumentException("PropertyId is required.", nameof(propertyId));
        ArgumentNullException.ThrowIfNull(schedule);
        ValidateName(name);

        var def = new JobDefinition(NormaliseId(id), owner, propertyId, name.Trim(), schedule);
        var order = 0;
        foreach (var description in initialDescriptions)
            def._stepTemplates.Add(StepTemplate.Create(NewStepTemplateId(), order++, description));
        return def;
    }

    internal static JobDefinition Hydrate(
        string id,
        OwnerId owner,
        string propertyId,
        string name,
        ScheduleDefinition schedule,
        IEnumerable<StepTemplate> stepTemplates)
    {
        var def = new JobDefinition(id, owner, propertyId, name, schedule);
        def._stepTemplates.AddRange(stepTemplates);
        return def;
    }

    public void Rename(string newName)
    {
        ValidateName(newName);
        Name = newName.Trim();
    }

    public void UpdateSchedule(ScheduleDefinition schedule)
    {
        ArgumentNullException.ThrowIfNull(schedule);
        Schedule = schedule;
    }

    public StepTemplateMutationOutcome AddStepTemplate(string description)
    {
        _stepTemplates.Add(StepTemplate.Create(NewStepTemplateId(), _stepTemplates.Count, description));
        return StepTemplateMutationOutcome.Success;
    }

    public StepTemplateMutationOutcome RemoveStepTemplate(string id)
    {
        var template = _stepTemplates.FirstOrDefault(s => s.Id == id);
        if (template is null) return StepTemplateMutationOutcome.StepTemplateNotFound;
        _stepTemplates.Remove(template);
        RenumberStepTemplates();
        return StepTemplateMutationOutcome.Success;
    }

    public ReorderStepTemplatesOutcome ReorderStepTemplates(IReadOnlyList<string> orderedIds)
    {
        if (orderedIds.Count != _stepTemplates.Count)
            return ReorderStepTemplatesOutcome.WrongCount;
        if (orderedIds.Distinct().Count() != orderedIds.Count)
            return ReorderStepTemplatesOutcome.DuplicateId;

        var newOrder = new List<StepTemplate>(orderedIds.Count);
        foreach (var id in orderedIds)
        {
            var template = _stepTemplates.FirstOrDefault(s => s.Id == id);
            if (template is null) return ReorderStepTemplatesOutcome.UnknownId;
            newOrder.Add(template);
        }

        _stepTemplates.Clear();
        _stepTemplates.AddRange(newOrder);
        RenumberStepTemplates();
        return ReorderStepTemplatesOutcome.Success;
    }

    public StepTemplateMutationOutcome EditStepTemplateDescription(string id, string description)
    {
        var template = _stepTemplates.FirstOrDefault(s => s.Id == id);
        if (template is null) return StepTemplateMutationOutcome.StepTemplateNotFound;
        template.EditDescription(description);
        return StepTemplateMutationOutcome.Success;
    }

    private void RenumberStepTemplates()
    {
        for (var i = 0; i < _stepTemplates.Count; i++)
            _stepTemplates[i].SetOrder(i);
    }

    private static void ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("JobDefinition name cannot be null, empty or whitespace.", nameof(name));
        if (name.Trim().Length > 200)
            throw new ArgumentException("JobDefinition name must be 1..200 characters.", nameof(name));
    }

    private static string NormaliseId(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("JobDefinition id cannot be empty.", nameof(id));
        return id;
    }

    private static string NewStepTemplateId() => Guid.NewGuid().ToString("N");
}
