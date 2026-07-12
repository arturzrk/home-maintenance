using HomeMaintenance.Application.Jobs.Dto;
using HomeMaintenance.Domain.Jobs;

namespace HomeMaintenance.Application.Jobs;

internal static class JobMappings
{
    public static JobDetailDto ToDetailDto(this Job job)
        => new(
            job.Id,
            job.PropertyId,
            job.Name,
            job.DueDate,
            job.Status,
            job.CompletedAt,
            job.Steps.Select(s => new StepDto(
                s.Id, s.Order, s.Description, s.IsCompleted, s.CompletedAt)).ToList(),
            job.JobDefinitionId,
            job.AssetId);

    public static JobSummaryDto ToSummaryDto(this Job job)
        => new(
            job.Id,
            job.PropertyId,
            job.Name,
            job.DueDate,
            job.Status,
            job.CompletedAt,
            job.Steps.Count,
            job.Steps.Count(s => s.IsCompleted),
            job.JobDefinitionId,
            job.AssetId);
}
