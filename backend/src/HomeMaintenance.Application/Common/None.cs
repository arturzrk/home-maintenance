namespace HomeMaintenance.Application.Common;

/// <summary>
/// Placeholder value type for <see cref="Result{T}"/> when the handler
/// has no payload to return on success.
/// </summary>
public readonly record struct None
{
    public static readonly None Value = default;
}
