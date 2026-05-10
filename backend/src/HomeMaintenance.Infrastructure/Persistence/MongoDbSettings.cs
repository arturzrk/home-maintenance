namespace HomeMaintenance.Infrastructure.Persistence;

/// <summary>
/// Strongly-typed configuration POCO for MongoDB connection settings.
/// Bound from appsettings.json section "MongoDB".
/// </summary>
public sealed class MongoDbSettings
{
    public const string SectionName = "MongoDB";

    public string ConnectionString { get; init; } = string.Empty;
    public string DatabaseName { get; init; } = string.Empty;
}
