using System.ComponentModel.DataAnnotations;

namespace HomeMaintenance.Application.Properties.Dto;

/// <summary>API response shape for a single Property.</summary>
public sealed record PropertyDto(string Id, string Name);

/// <summary>API response shape for the list endpoint.</summary>
public sealed record PropertyListDto(IReadOnlyList<PropertyDto> Properties);

/// <summary>Request body for POST /api/properties.</summary>
public sealed record CreatePropertyRequest(
    [property: Required]
    [property: StringLength(100, MinimumLength = 1)]
    string Name);

/// <summary>Request body for PATCH /api/properties/{id}.</summary>
public sealed record RenamePropertyRequest(
    [property: Required]
    [property: StringLength(100, MinimumLength = 1)]
    string Name);
