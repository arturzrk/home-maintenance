using HomeMaintenance.Application.Common;
using HomeMaintenance.Application.Common.Interfaces;
using HomeMaintenance.Application.Properties.Dto;

namespace HomeMaintenance.Application.Properties.Commands;

public sealed record RenamePropertyCommand(string Id, string Name);

public sealed class RenamePropertyHandler
{
    private readonly IPropertyRepository _repo;
    private readonly IIdentityProvider _identity;
    private readonly IAuditLog _audit;
    private readonly ICorrelationContext _correlation;

    public RenamePropertyHandler(
        IPropertyRepository repo,
        IIdentityProvider identity,
        IAuditLog audit,
        ICorrelationContext correlation)
    {
        _repo = repo;
        _identity = identity;
        _audit = audit;
        _correlation = correlation;
    }

    public async Task<Result<PropertyDto>> Handle(
        RenamePropertyCommand cmd,
        CancellationToken ct = default)
    {
        var owner = _identity.CurrentOwner;
        var property = await _repo.GetAsync(cmd.Id, owner, ct);
        if (property is null)
            return Result<PropertyDto>.Failure(new NotFoundError("Property", cmd.Id));

        var oldName = property.Name;
        try
        {
            property.Rename(cmd.Name);
        }
        catch (ArgumentException ex)
        {
            return Result<PropertyDto>.Failure(new ValidationError("name", ex.Message));
        }

        await _repo.UpdateAsync(property, ct);

        await _audit.RecordAsync(new AuditEvent(
            AuditEventTypes.PropertyRenamed,
            owner.Value,
            $"property:{property.Id}",
            DateTime.UtcNow,
            _correlation.CurrentId,
            new Dictionary<string, object?>
            {
                ["old_name"] = oldName,
                ["new_name"] = property.Name,
            }), ct);

        return Result<PropertyDto>.Success(new PropertyDto(property.Id, property.Name));
    }
}
