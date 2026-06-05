using HomeMaintenance.Application.Common;
using HomeMaintenance.Application.Common.Interfaces;
using HomeMaintenance.Application.JobDefinitions.Dto;
using HomeMaintenance.Domain.Identity;

namespace HomeMaintenance.Application.JobDefinitions.Queries;

public sealed record GetJobDefinitionQuery(string Id, OwnerId Owner);

public sealed class GetJobDefinitionHandler
{
    private readonly IJobDefinitionRepository _definitions;

    public GetJobDefinitionHandler(IJobDefinitionRepository definitions)
        => _definitions = definitions;

    public async Task<Result<JobDefinitionDto>> Handle(
        GetJobDefinitionQuery query,
        CancellationToken ct = default)
    {
        var definition = await _definitions.GetAsync(query.Id, query.Owner, ct);
        if (definition is null)
            return Result<JobDefinitionDto>.Failure(new NotFoundError("JobDefinition", query.Id));
        return Result<JobDefinitionDto>.Success(definition.ToDto());
    }
}
