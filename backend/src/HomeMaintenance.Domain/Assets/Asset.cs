using HomeMaintenance.Domain.Common;
using HomeMaintenance.Domain.Identity;

namespace HomeMaintenance.Domain.Assets;

/// <summary>
/// A physical thing on a property that maintenance work can be scoped to
/// (e.g. "Boiler", "Roof"). Aggregate root. Assets are never deleted:
/// the app-wide convention is to mark them obsolete instead, preserving
/// the full maintenance history of replaced equipment.
/// </summary>
public sealed class Asset : Entity
{
    public const int NameMaxLength = 200;
    public const int CategoryMaxLength = 100;
    public const int NotesMaxLength = 2000;

    public OwnerId Owner { get; private set; }
    public string PropertyId { get; private set; }
    public string Name { get; private set; }
    public string? Category { get; private set; }
    public string? Notes { get; private set; }
    public bool IsObsolete { get; private set; }

    private Asset(
        string id,
        OwnerId owner,
        string propertyId,
        string name,
        string? category,
        string? notes,
        bool isObsolete) : base(id)
    {
        Owner = owner;
        PropertyId = propertyId;
        Name = name;
        Category = category;
        Notes = notes;
        IsObsolete = isObsolete;
    }

    /// <summary>
    /// Creates a new Asset. Use this for the create flow.
    /// </summary>
    public static Asset Create(
        string id,
        OwnerId owner,
        string propertyId,
        string name,
        string? category = null,
        string? notes = null)
    {
        ArgumentNullException.ThrowIfNull(owner);
        if (string.IsNullOrWhiteSpace(propertyId))
            throw new ArgumentException("PropertyId is required.", nameof(propertyId));
        ValidateName(name);
        var normalizedCategory = NormalizeOptional(category, CategoryMaxLength, nameof(category));
        var normalizedNotes = NormalizeOptional(notes, NotesMaxLength, nameof(notes));
        return new Asset(id, owner, propertyId, name.Trim(), normalizedCategory, normalizedNotes, isObsolete: false);
    }

    /// <summary>
    /// Reconstructs an Asset from persisted state. Used by repository
    /// mappers; does NOT re-run validation so that stored records created
    /// under looser constraints still load.
    /// </summary>
    internal static Asset Hydrate(
        string id,
        OwnerId owner,
        string propertyId,
        string name,
        string? category,
        string? notes,
        bool isObsolete)
        => new(id, owner, propertyId, name, category, notes, isObsolete);

    public void Rename(string newName)
    {
        ValidateName(newName);
        Name = newName.Trim();
    }

    /// <summary>Sets or clears the category (null/whitespace clears).</summary>
    public void SetCategory(string? category)
        => Category = NormalizeOptional(category, CategoryMaxLength, nameof(category));

    /// <summary>Sets or clears the notes (null/whitespace clears).</summary>
    public void SetNotes(string? notes)
        => Notes = NormalizeOptional(notes, NotesMaxLength, nameof(notes));

    public void SetObsolete(bool isObsolete) => IsObsolete = isObsolete;

    private static void ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Asset name cannot be null, empty or whitespace.", nameof(name));
        if (name.Trim().Length > NameMaxLength)
            throw new ArgumentException($"Asset name must be 1..{NameMaxLength} characters.", nameof(name));
    }

    private static string? NormalizeOptional(string? value, int maxLength, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        var trimmed = value.Trim();
        if (trimmed.Length > maxLength)
            throw new ArgumentException($"Value must be at most {maxLength} characters.", paramName);
        return trimmed;
    }
}
