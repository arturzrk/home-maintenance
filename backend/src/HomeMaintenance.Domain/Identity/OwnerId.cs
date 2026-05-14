namespace HomeMaintenance.Domain.Identity;

/// <summary>
/// Wraps the OIDC <c>sub</c> claim. Equality is by <see cref="Value"/>;
/// no user metadata is stored - identity metadata lives with the provider.
/// </summary>
public sealed record OwnerId
{
    public string Value { get; }

    public OwnerId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("OwnerId value cannot be null, empty or whitespace.", nameof(value));
        Value = value;
    }

    public static implicit operator string(OwnerId id) => id.Value;

    public override string ToString() => Value;
}
