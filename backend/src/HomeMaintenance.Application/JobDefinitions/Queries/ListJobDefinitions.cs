using HomeMaintenance.Application.Common;
using HomeMaintenance.Application.Common.Interfaces;
using HomeMaintenance.Application.JobDefinitions.Dto;

namespace HomeMaintenance.Application.JobDefinitions.Queries;

public sealed record ListJobDefinitionsQuery(string? PropertyId = null);

public sealed class ListJobDefinitionsHandler
{
    private readonly IJobDefinitionRepository _definitions;
    private readonly IIdentityProvider _identity;

    public ListJobDefinitionsHandler(IJobDefinitionRepository definitions, IIdentityProvider identity)
    {
        _definitions = definitions;
        _identity = identity;
    }

    public async Task<Result<IReadOnlyList<JobDefinitionDto>>> Handle(
        ListJobDefinitionsQuery query,
        CancellationToken ct = default)
    {
        var definitions = await _definitions.ListAsync(_identity.CurrentOwner, query.PropertyId, ct);
        return Result<IReadOnlyList<JobDefinitionDto>>.Success(
            definitions.Select(d => d.ToDto()).ToList());
    }
}
