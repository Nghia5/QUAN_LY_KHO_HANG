namespace WMS.Api.Models.Documents;

/// <summary>
/// Stocktake (phiếu kiểm kê). Flow: Draft → Snapshot (InProgress, locations locked) → Count →
/// Submit/Compare (PendingApproval) → Approve → Adjust (STOCKTAKE_IN/OUT movements) → Completed.
/// SystemQuantity is captured once at snapshot and never overwritten.
/// </summary>
public sealed class Stocktake : WarehouseDocument
{
    public override DocumentFlow Flow => DocumentFlow.Stocktake;

    /// <summary>
    /// Locations to count (any level: a Zone includes all its bins). Empty = the whole warehouse.
    /// Stored as a JSON array.
    /// </summary>
    public List<int> ScopeLocationIds { get; set; } = [];

    public int? SnapshotBy { get; set; }

    public DateTime? SnapshotAt { get; set; }

    public int? AdjustedBy { get; set; }

    public DateTime? AdjustedAt { get; set; }

    public List<StocktakeLine> Lines { get; set; } = [];
}

public sealed class StocktakeLine
{
    public int Id { get; set; }

    public int StocktakeId { get; set; }

    public int WarehouseId { get; set; }

    public int ProductId { get; set; }

    /// <summary>Counted Bin.</summary>
    public int LocationId { get; set; }

    /// <summary>Inventory.Quantity at snapshot time (0 for items found during counting). Immutable.</summary>
    public decimal SystemQuantity { get; set; }

    /// <summary>Physically counted quantity; null until counted.</summary>
    public decimal? CountedQuantity { get; set; }

    /// <summary>CountedQuantity - SystemQuantity (persisted computed column; null until counted).</summary>
    public decimal? Difference { get; private set; }

    /// <summary>True when the line was added during counting (not in the snapshot).</summary>
    public bool IsAddedDuringCount { get; set; }

    public int? CountedBy { get; set; }

    public DateTime? CountedAt { get; set; }

    public string? Note { get; set; }

    public Product? Product { get; set; }

    public WarehouseLocation? Location { get; set; }
}
