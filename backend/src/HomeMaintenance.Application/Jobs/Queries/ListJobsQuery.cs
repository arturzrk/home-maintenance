using HomeMaintenance.Domain.Jobs;

namespace HomeMaintenance.Application.Jobs.Queries;

public sealed record ListJobsQuery(string? PropertyId, JobStatus? Status, string? AssetId = null);
