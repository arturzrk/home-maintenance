using HomeMaintenance.Application.Common;
using HomeMaintenance.Application.Common.Interfaces;
using HomeMaintenance.Application.Properties.Dto;
using HomeMaintenance.Domain.Properties;

namespace HomeMaintenance.Application.Properties.Commands;

public sealed record CreatePropertyCommand(string Name);

public sealed class CreatePropertyHandler
{
    private readonly IPropertyRepository _repo;
    private readonly IIdentityProvider _identity;
    private readonly IAuditLog _audit;
    private readonly ICorrelationContext _correlation;

    public CreatePropertyHandler(
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
        CreatePropertyCommand cmd,
        CancellationToken ct = default)
    {
        Property property;
        try
        {
            property = Property.Create(IdFactory.NewId(), _identity.CurrentOwner, cmd.Name);
        }
        catch (ArgumentException ex)
        {
            return Result<PropertyDto>.Failure(
                new ValidationError("name", ex.Message));
        }

        await _repo.AddAsync(property, ct);

        await _audit.RecordAsync(new AuditEvent(
            AuditEventTypes.PropertyCreated,
            property.Owner.Value,
            $"property:{property.Id}",
            DateTime.UtcNow,
            _correlation.CurrentId,
            new Dictionary<string, object?> { ["name"] = property.Name }), ct);

        return Result<PropertyDto>.Success(new PropertyDto(property.Id, property.Name));
    }
}
