namespace HomeMaintenance.Application.JobDefinitions.Queries;

public sealed record ListJobDefinitionsQuery(string? PropertyId = null, string? AssetId = null);
