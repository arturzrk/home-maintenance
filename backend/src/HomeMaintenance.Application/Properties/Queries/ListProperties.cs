using HomeMaintenance.Application.Common;
using HomeMaintenance.Application.Common.Interfaces;
using HomeMaintenance.Application.Properties.Dto;

namespace HomeMaintenance.Application.Properties.Queries;

public sealed record ListPropertiesQuery;

public sealed class ListPropertiesHandler
{
    private readonly IPropertyRepository _repo;
    private readonly IIdentityProvider _identity;

    public ListPropertiesHandler(IPropertyRepository repo, IIdentityProvider identity)
    {
        _repo = repo;
        _identity = identity;
    }

    public async Task<Result<PropertyListDto>> Handle(
        ListPropertiesQuery _,
        CancellationToken ct = default)
    {
        var properties = await _repo.ListAsync(_identity.CurrentOwner, ct);
        var dtos = properties.Select(p => new PropertyDto(p.Id, p.Name)).ToList();
        return Result<PropertyListDto>.Success(new PropertyListDto(dtos));
    }
}
