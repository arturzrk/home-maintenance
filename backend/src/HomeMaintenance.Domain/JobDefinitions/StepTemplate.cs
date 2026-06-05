using HomeMaintenance.Domain.Common;

namespace HomeMaintenance.Domain.JobDefinitions;

public sealed class StepTemplate : Entity
{
    public int Order { get; private set; }
    public string Description { get; private set; }

    private StepTemplate(string id, int order, string description) : base(id)
    {
        Order = order;
        Description = description;
    }

    public static StepTemplate Create(string id, int order, string description)
    {
        Validate(description);
        return new StepTemplate(id, order, description.Trim());
    }

    internal static StepTemplate Hydrate(string id, int order, string description)
        => new(id, order, description);

    public void EditDescription(string newDescription)
    {
        Validate(newDescription);
        Description = newDescription.Trim();
    }

    internal void SetOrder(int order) => Order = order;

    private static void Validate(string description)
    {
        if (string.IsNullOrWhiteSpace(description))
            throw new ArgumentException("StepTemplate description cannot be null, empty or whitespace.", nameof(description));
        if (description.Trim().Length > 500)
            throw new ArgumentException("StepTemplate description must be 1..500 characters.", nameof(description));
    }
}
