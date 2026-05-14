using HomeMaintenance.Domain.Common;
using HomeMaintenance.Domain.Identity;

namespace HomeMaintenance.Domain.Properties;

/// <summary>
/// A homeowner's property (e.g. "Main House"). Aggregate root.
/// </summary>
public sealed class Property : Entity
{
    public OwnerId Owner { get; private set; }
    public string Name { get; private set; }

    private Property(string id, OwnerId owner, string name) : base(id)
    {
        Owner = owner;
        Name = name;
    }

    /// <summary>
    /// Creates a new Property. Use this for the create flow.
    /// </summary>
    public static Property Create(string id, OwnerId owner, string name)
    {
        ArgumentNullException.ThrowIfNull(owner);
        Validate(name);
        return new Property(id, owner, name.Trim());
    }

    /// <summary>
    /// Reconstructs a Property from persisted state. Used by repository
    /// mappers; does NOT re-run validation so that stored records created
    /// under looser constraints still load.
    /// </summary>
    internal static Property Hydrate(string id, OwnerId owner, string name)
        => new(id, owner, name);

    public void Rename(string newName)
    {
        Validate(newName);
        Name = newName.Trim();
    }

    private static void Validate(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Property name cannot be null, empty or whitespace.", nameof(name));
        if (name.Trim().Length > 100)
            throw new ArgumentException("Property name must be 1..100 characters.", nameof(name));
    }
}
