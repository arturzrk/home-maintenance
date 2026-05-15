using HomeMaintenance.Domain.Common;

namespace HomeMaintenance.Domain.Jobs;

/// <summary>
/// A single checklist item inside a Job. Lifecycle is owned by the
/// parent Job aggregate; every mutator on Step is <c>internal</c> so
/// only the aggregate can call it.
/// </summary>
public sealed class Step : Entity
{
    public int Order { get; private set; }
    public string Description { get; private set; }
    public bool IsCompleted { get; private set; }
    public DateTime? CompletedAt { get; private set; }

    private Step(string id, int order, string description) : base(id)
    {
        Order = order;
        Description = description;
        IsCompleted = false;
        CompletedAt = null;
    }

    internal static Step Create(string id, int order, string description)
    {
        Validate(description);
        return new Step(id, order, description.Trim());
    }

    /// <summary>
    /// Reconstructs a Step from persisted state (no validation re-run).
    /// </summary>
    internal static Step Hydrate(
        string id,
        int order,
        string description,
        bool isCompleted,
        DateTime? completedAt)
        => new(id, order, description)
        {
            IsCompleted = isCompleted,
            CompletedAt = completedAt,
        };

    internal void SetOrder(int order) => Order = order;

    internal void EditDescription(string newDescription)
    {
        Validate(newDescription);
        Description = newDescription.Trim();
    }

    internal void Tick(DateTime now)
    {
        if (IsCompleted) return; // idempotent
        IsCompleted = true;
        CompletedAt = now;
    }

    internal void Untick()
    {
        if (!IsCompleted) return; // idempotent
        IsCompleted = false;
        CompletedAt = null;
    }

    private static void Validate(string description)
    {
        if (string.IsNullOrWhiteSpace(description))
            throw new ArgumentException("Step description cannot be null, empty or whitespace.", nameof(description));
        if (description.Trim().Length > 500)
            throw new ArgumentException("Step description must be 1..500 characters.", nameof(description));
    }
}
