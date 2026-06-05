using HomeMaintenance.Domain.Common;
using HomeMaintenance.Domain.Identity;

namespace HomeMaintenance.Domain.Jobs;

/// <summary>
/// Outcome of a step-targeted mutation. Aggregate methods that lookup
/// a step by id return <c>StepNotFound</c> when the id is unknown so
/// handlers can map to a 404 without reaching for exceptions.
/// </summary>
public enum StepMutationOutcome
{
    Success,
    StepNotFound,
}

/// <summary>
/// Outcome of an explicit <see cref="Job.Complete"/> attempt. The
/// three failure modes match the spec's FR-018 contract.
/// </summary>
public enum CompleteJobOutcome
{
    Success,
    AlreadyCompleted,
    NoSteps,
    StepsIncomplete,
}

/// <summary>
/// Outcome of <see cref="Job.ReorderSteps"/> validation.
/// </summary>
public enum ReorderStepsOutcome
{
    Success,
    WrongCount,
    DuplicateId,
    UnknownId,
}

/// <summary>
/// A one-shot maintenance Job (Slice 1). Aggregate root.
/// </summary>
public sealed class Job : Entity
{
    private readonly List<Step> _steps = new();

    public OwnerId Owner { get; private set; }
    public string PropertyId { get; private set; }
    public string Name { get; private set; }
    public DateOnly? DueDate { get; private set; }
    public JobStatus Status { get; private set; }
    public DateTime? CompletedAt { get; private set; }

    public IReadOnlyList<Step> Steps => _steps.AsReadOnly();
    public string? JobDefinitionId { get; private set; }

    private Job(string id, OwnerId owner, string propertyId, string name, DateOnly? dueDate)
        : base(id)
    {
        Owner = owner;
        PropertyId = propertyId;
        Name = name;
        DueDate = dueDate;
        Status = JobStatus.Active;
        CompletedAt = null;
    }

    public static Job Create(
        string id,
        OwnerId owner,
        string propertyId,
        string name,
        DateOnly? dueDate,
        IEnumerable<string> initialStepDescriptions,
        string? jobDefinitionId = null)
    {
        ArgumentNullException.ThrowIfNull(owner);
        if (string.IsNullOrWhiteSpace(propertyId))
            throw new ArgumentException("PropertyId is required.", nameof(propertyId));
        ValidateName(name);

        var job = new Job(NormaliseId(id), owner, propertyId, name.Trim(), dueDate)
        {
            JobDefinitionId = jobDefinitionId,
        };
        var order = 0;
        foreach (var description in initialStepDescriptions)
        {
            // Step.Create throws ArgumentException for invalid description;
            // wrapping nothing - the outer handler maps to ValidationError.
            job._steps.Add(Step.Create(NewStepId(), order++, description));
        }
        return job;
    }

    /// <summary>
    /// Reconstructs a Job from persisted state. Used only by repository
    /// mappers; bypasses validation.
    /// </summary>
    internal static Job Hydrate(
        string id,
        OwnerId owner,
        string propertyId,
        string name,
        DateOnly? dueDate,
        JobStatus status,
        DateTime? completedAt,
        IEnumerable<Step> steps,
        string? jobDefinitionId = null)
    {
        var job = new Job(id, owner, propertyId, name, dueDate)
        {
            Status = status,
            CompletedAt = completedAt,
            JobDefinitionId = jobDefinitionId,
        };
        job._steps.AddRange(steps);
        return job;
    }

    // ---- Job-level mutations ----

    public void Rename(string newName)
    {
        EnsureActive();
        ValidateName(newName);
        Name = newName.Trim();
    }

    public void SetDueDate(DateOnly? dueDate)
    {
        EnsureActive();
        DueDate = dueDate;
    }

    // ---- Step list mutations ----

    public Step AddStep(string description)
    {
        EnsureActive();
        var step = Step.Create(NewStepId(), _steps.Count, description);
        _steps.Add(step);
        return step;
    }

    public StepMutationOutcome RemoveStep(string stepId)
    {
        EnsureActive();
        var step = _steps.FirstOrDefault(s => s.Id == stepId);
        if (step is null) return StepMutationOutcome.StepNotFound;
        _steps.Remove(step);
        RenumberSteps();
        return StepMutationOutcome.Success;
    }

    public ReorderStepsOutcome ReorderSteps(IReadOnlyList<string> orderedStepIds)
    {
        EnsureActive();
        if (orderedStepIds.Count != _steps.Count)
            return ReorderStepsOutcome.WrongCount;
        if (orderedStepIds.Distinct().Count() != orderedStepIds.Count)
            return ReorderStepsOutcome.DuplicateId;

        var newOrder = new List<Step>(orderedStepIds.Count);
        foreach (var id in orderedStepIds)
        {
            var step = _steps.FirstOrDefault(s => s.Id == id);
            if (step is null) return ReorderStepsOutcome.UnknownId;
            newOrder.Add(step);
        }

        _steps.Clear();
        _steps.AddRange(newOrder);
        RenumberSteps();
        return ReorderStepsOutcome.Success;
    }

    public StepMutationOutcome EditStepDescription(string stepId, string newDescription)
    {
        EnsureActive();
        var step = _steps.FirstOrDefault(s => s.Id == stepId);
        if (step is null) return StepMutationOutcome.StepNotFound;
        step.EditDescription(newDescription);
        return StepMutationOutcome.Success;
    }

    public StepMutationOutcome TickStep(string stepId, DateTime now)
    {
        EnsureActive();
        var step = _steps.FirstOrDefault(s => s.Id == stepId);
        if (step is null) return StepMutationOutcome.StepNotFound;
        step.Tick(now);
        return StepMutationOutcome.Success;
    }

    public StepMutationOutcome UntickStep(string stepId)
    {
        EnsureActive();
        var step = _steps.FirstOrDefault(s => s.Id == stepId);
        if (step is null) return StepMutationOutcome.StepNotFound;
        step.Untick();
        return StepMutationOutcome.Success;
    }

    // ---- Lifecycle ----

    public CompleteJobOutcome Complete(DateTime now)
    {
        if (Status == JobStatus.Completed) return CompleteJobOutcome.AlreadyCompleted;
        if (_steps.Count == 0) return CompleteJobOutcome.NoSteps;
        if (_steps.Any(s => !s.IsCompleted)) return CompleteJobOutcome.StepsIncomplete;

        Status = JobStatus.Completed;
        CompletedAt = now;
        return CompleteJobOutcome.Success;
    }

    // ---- Helpers ----

    private void EnsureActive()
    {
        if (Status != JobStatus.Active)
            throw new InvalidOperationException("Job is completed; mutation not allowed.");
    }

    private void RenumberSteps()
    {
        for (var i = 0; i < _steps.Count; i++)
            _steps[i].SetOrder(i);
    }

    private static void ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Job name cannot be null, empty or whitespace.", nameof(name));
        if (name.Trim().Length > 200)
            throw new ArgumentException("Job name must be 1..200 characters.", nameof(name));
    }

    private static string NormaliseId(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("Job id cannot be empty.", nameof(id));
        return id;
    }

    private static string NewStepId() => Guid.NewGuid().ToString("N");
}
