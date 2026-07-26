namespace HomeMaintenance.Infrastructure.Scheduling;

/// <summary>
/// Bound from configuration section <c>Frontend</c>. <see cref="BaseUrl"/>
/// is the origin used to build absolute links (job pages, notification
/// settings) inside reminder digest emails - same per-environment shape
/// as <c>Cors:AllowedOrigins</c>.
/// </summary>
public sealed class FrontendOptions
{
    public const string SectionName = "Frontend";
    public string BaseUrl { get; set; } = "http://localhost:3000";
}
