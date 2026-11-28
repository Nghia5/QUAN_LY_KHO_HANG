using WMS.Api.Dtos.Common;
using WMS.Api.Models;

namespace WMS.Api.Dtos.Locations;

/// <summary>
/// Payload for creating a location. The full code is built as "{parent.Code}-{SegmentCode}"
/// (or just SegmentCode for zones). Type and parent cannot be changed later.
/// </summary>
public sealed record CreateLocationRequest
{
    public LocationType Type { get; init; }

    /// <summary>Required for Rack/Shelf/Bin, must be null for Zone.</summary>
    public int? ParentId { get; init; }

    public required string SegmentCode { get; init; }

    public required string Name { get; init; }

    public string? Description { get; init; }
}

public sealed record UpdateLocationRequest
{
    public required string Name { get; init; }

    public string? Description { get; init; }
}

/// <summary>List query inside one warehouse; keyword matches Code or Name.</summary>
public sealed record LocationQuery : PagedRequest
{
    public static readonly string[] SortableFields = ["code", "name", "type", "createdAt"];

    public LocationType? Type { get; init; }

    public int? ParentId { get; init; }

    public bool? IsActive { get; init; }
}

public sealed record LocationResponse(
    int Id,
    int WarehouseId,
    int? ParentId,
    LocationType Type,
    string SegmentCode,
    string Code,
    string Name,
    string? Description,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public sealed record LocationTreeNode(
    int Id,
    LocationType Type,
    string Code,
    string Name,
    bool IsActive,
    IReadOnlyList<LocationTreeNode> Children);
