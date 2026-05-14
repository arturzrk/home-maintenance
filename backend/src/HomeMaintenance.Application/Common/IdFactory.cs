namespace HomeMaintenance.Application.Common;

/// <summary>
/// Single-source-of-truth id factory. Repositories and handlers use this
/// so id encoding never drifts.
/// </summary>
public static class IdFactory
{
    public static string NewId() => Guid.NewGuid().ToString("N");
}
