using HomeMaintenance.Application.Common;
using HomeMaintenance.Application.Common.Interfaces;
using HomeMaintenance.Application.JobDefinitions.Dto;
using HomeMaintenance.Domain.Identity;

namespace HomeMaintenance.Application.JobDefinitions.Queries;

public sealed record ListJobDefinitionsQuery(OwnerId Owner, string? PropertyId = null);

public sealed class ListJobDefinitionsHandler
{
    private readonly IJobDefinitionRepository _definitions;

    public ListJobDefinitionsHandler(IJobDefinitionRepository definitions)
        => _definitions = definitions;

    public async Task<Result<IReadOnlyList<JobDefinitionDto>>> Handle(
        ListJobDefinitionsQuery query,
        CancellationToken ct = default)
    {
        var definitions = await _definitions.ListAsync(query.Owner, query.PropertyId, ct);
        return Result<IReadOnlyList<JobDefinitionDto>>.Success(
            definitions.Select(d => d.ToDto()).ToList());
    }
}
