using HomeMaintenance.Application.Common;
using HomeMaintenance.Application.Common.Interfaces;
using HomeMaintenance.Application.JobDefinitions.Dto;

namespace HomeMaintenance.Application.JobDefinitions.Queries;

public sealed record GetJobDefinitionQuery(string Id);

public sealed class GetJobDefinitionHandler
{
    private readonly IJobDefinitionRepository _definitions;
    private readonly IIdentityProvider _identity;

    public GetJobDefinitionHandler(IJobDefinitionRepository definitions, IIdentityProvider identity)
    {
        _definitions = definitions;
        _identity = identity;
    }

    public async Task<Result<JobDefinitionDto>> Handle(
        GetJobDefinitionQuery query,
        CancellationToken ct = default)
    {
        var definition = await _definitions.GetAsync(query.Id, _identity.CurrentOwner, ct);
        if (definition is null)
            return Result<JobDefinitionDto>.Failure(new NotFoundError("JobDefinition", query.Id));
        return Result<JobDefinitionDto>.Success(definition.ToDto());
    }
}
