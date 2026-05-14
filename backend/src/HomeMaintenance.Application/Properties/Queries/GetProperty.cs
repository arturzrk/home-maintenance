using HomeMaintenance.Application.Common;
using HomeMaintenance.Application.Common.Interfaces;
using HomeMaintenance.Application.Properties.Dto;

namespace HomeMaintenance.Application.Properties.Queries;

public sealed record GetPropertyQuery(string Id);

public sealed class GetPropertyHandler
{
    private readonly IPropertyRepository _repo;
    private readonly IIdentityProvider _identity;

    public GetPropertyHandler(IPropertyRepository repo, IIdentityProvider identity)
    {
        _repo = repo;
        _identity = identity;
    }

    public async Task<Result<PropertyDto>> Handle(
        GetPropertyQuery query,
        CancellationToken ct = default)
    {
        var property = await _repo.GetAsync(query.Id, _identity.CurrentOwner, ct);
        return property is null
            ? Result<PropertyDto>.Failure(new NotFoundError("Property", query.Id))
            : Result<PropertyDto>.Success(new PropertyDto(property.Id, property.Name));
    }
}
