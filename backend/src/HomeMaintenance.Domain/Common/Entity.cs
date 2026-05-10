namespace HomeMaintenance.Domain.Common;

/// <summary>
/// Base class for all domain entities. Enforces identity equality
/// and ensures every entity has a stable string identifier (MongoDB ObjectId string).
/// </summary>
public abstract class Entity
{
    public string Id { get; protected set; } = string.Empty;

    protected Entity() { }

    protected Entity(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("Entity id cannot be empty.", nameof(id));

        Id = id;
    }

    public override bool Equals(object? obj)
    {
        if (obj is not Entity other) return false;
        if (ReferenceEquals(this, other)) return true;
        if (GetType() != other.GetType()) return false;

        return Id == other.Id;
    }

    public override int GetHashCode() => Id.GetHashCode();

    public static bool operator ==(Entity? left, Entity? right) =>
        left?.Equals(right) ?? right is null;

    public static bool operator !=(Entity? left, Entity? right) =>
        !(left == right);
}
