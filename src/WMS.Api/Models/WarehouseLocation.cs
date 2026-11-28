namespace WMS.Api.Models;

/// <summary>Fixed hierarchy inside a warehouse: Zone &gt; Rack &gt; Shelf &gt; Bin.</summary>
public enum LocationType
{
    Zone = 1,
    Rack = 2,
    Shelf = 3,
    Bin = 4
}

public sealed class WarehouseLocation
{
    public int Id { get; set; }

    public int WarehouseId { get; set; }

    /// <summary>Null for zones; otherwise a location of the level directly above, in the same warehouse.</summary>
    public int? ParentId { get; set; }

    public LocationType Type { get; set; }

    /// <summary>Segment entered by the user, e.g. "01". Stored upper-case.</summary>
    public required string SegmentCode { get; set; }

    /// <summary>Full code built from the hierarchy, e.g. "A-01-02-03". Unique per warehouse.</summary>
    public required string Code { get; set; }

    public required string Name { get; set; }

    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; }

    public int? CreatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public int? UpdatedBy { get; set; }

    public Warehouse? Warehouse { get; set; }

    public WarehouseLocation? Parent { get; set; }

    public List<WarehouseLocation> Children { get; set; } = [];

    /// <summary>The only type allowed as a parent of <paramref name="type"/> (null = root).</summary>
    public static LocationType? RequiredParentType(LocationType type) => type switch
    {
        LocationType.Zone => null,
        LocationType.Rack => LocationType.Zone,
        LocationType.Shelf => LocationType.Rack,
        LocationType.Bin => LocationType.Shelf,
        _ => throw new ArgumentOutOfRangeException(nameof(type))
    };
}
