using WMS.Api.Authorization;
using WMS.Api.Dtos.Common;
using WMS.Api.Dtos.Documents;
using WMS.Api.Models.Documents;
using WMS.Api.Services.Documents;

namespace WMS.Api.Dtos.Stocktakes;

/// <summary>Creates a Draft stocktake. Empty scope = the whole warehouse.</summary>
public sealed record CreateStocktakeRequest : IWarehouseRequest
{
    public int WarehouseId { get; init; }

    /// <summary>Zones/racks/shelves/bins to count (a parent includes all its bins).</summary>
    public IReadOnlyList<int> ScopeLocationIds { get; init; } = [];

    public string? Note { get; init; }
}

public sealed record UpdateStocktakeRequest
{
    public IReadOnlyList<int> ScopeLocationIds { get; init; } = [];

    public string? Note { get; init; }
}

public sealed record StocktakeCountItem
{
    public int ProductId { get; init; }

    public int LocationId { get; init; }

    /// <summary>Physically counted quantity (&gt;= 0).</summary>
    public decimal CountedQuantity { get; init; }

    public string? Note { get; init; }
}

/// <summary>
/// Records counts. Items are matched by product + bin; an item not in the snapshot is added
/// (goods found during counting).
/// </summary>
public sealed record StocktakeCountRequest
{
    public IReadOnlyList<StocktakeCountItem> Items { get; init; } = [];
}

public sealed record StocktakeQuery : PagedRequest
{
    public static readonly string[] SortableFields = ["documentNo", "status", "createdAt"];

    public int? WarehouseId { get; init; }

    public DocumentStatus? Status { get; init; }
}

public sealed record StocktakeSummary(
    int Id,
    string DocumentNo,
    int WarehouseId,
    string WarehouseCode,
    DocumentStatus Status,
    int LineCount,
    int CountedLines,
    int LinesWithDifference,
    int? CreatedBy,
    DateTime CreatedAt);

public sealed record StocktakeLineResponse(
    int Id,
    int ProductId,
    string ProductSku,
    string ProductName,
    string UnitOfMeasure,
    int LocationId,
    string LocationCode,
    decimal SystemQuantity,
    decimal? CountedQuantity,
    decimal? Difference,
    bool IsAddedDuringCount,
    int? CountedBy,
    DateTime? CountedAt,
    string? Note);

/// <param name="TotalSurplus">Sum of positive differences (found more than the system).</param>
/// <param name="TotalShortage">Sum of negative differences, as a positive number.</param>
public sealed record StocktakeTotals(
    int LineCount,
    int CountedLines,
    int UncountedLines,
    int LinesWithDifference,
    decimal TotalSurplus,
    decimal TotalShortage);

public sealed record StocktakeDetail(
    int Id,
    string DocumentNo,
    int WarehouseId,
    string WarehouseCode,
    DocumentStatus Status,
    IReadOnlyList<int> ScopeLocationIds,
    string? Note,
    int? SnapshotBy,
    DateTime? SnapshotAt,
    int? AdjustedBy,
    DateTime? AdjustedAt,
    StocktakeTotals Totals,
    IReadOnlyList<StocktakeLineResponse> Lines,
    DocumentWorkflowInfo Workflow,
    IReadOnlyList<DocumentAction> AvailableActions);
