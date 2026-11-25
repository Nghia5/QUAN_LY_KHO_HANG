using WMS.Api.Dtos.Common;

namespace WMS.Api.Dtos.Warehouses;

/// <summary>Payload for creating a warehouse.</summary>
public sealed record CreateWarehouseRequest
{
    public required string Code { get; init; }

    public required string Name { get; init; }

    public string? Address { get; init; }

    public string? Phone { get; init; }

    public string? Description { get; init; }

    public int? ManagerId { get; init; }
}

/// <summary>Payload for updating a warehouse. Code is immutable after creation.</summary>
public sealed record UpdateWarehouseRequest
{
    public required string Name { get; init; }

    public string? Address { get; init; }

    public string? Phone { get; init; }

    public string? Description { get; init; }

    public int? ManagerId { get; init; }
}

/// <summary>List query: keyword matches Code, Name or Address.</summary>
public sealed record WarehouseQuery : PagedRequest
{
    public static readonly string[] SortableFields = ["code", "name", "createdAt", "isActive"];

    public bool? IsActive { get; init; }

    public int? ManagerId { get; init; }
}

public sealed record WarehouseResponse(
    int Id,
    string Code,
    string Name,
    string? Address,
    string? Phone,
    string? Description,
    int? ManagerId,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? UpdatedAt);
