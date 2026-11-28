using WMS.Api.Dtos.Common;
using WMS.Api.Models;

namespace WMS.Api.Dtos.StockMovements;

/// <summary>Movement history query. Keyword matches product SKU/name and reference number.</summary>
public sealed record StockMovementQuery : PagedRequest
{
    public static readonly string[] SortableFields = ["createdAt", "productSku", "quantity", "movementType", "referenceNo"];

    public int? WarehouseId { get; init; }

    public int? LocationId { get; init; }

    public int? ProductId { get; init; }

    public MovementType? MovementType { get; init; }

    public string? ReferenceType { get; init; }

    public int? ReferenceId { get; init; }

    public int? CreatedBy { get; init; }

    /// <summary>Inclusive lower bound (UTC).</summary>
    public DateTime? From { get; init; }

    /// <summary>Exclusive upper bound (UTC).</summary>
    public DateTime? To { get; init; }
}

public sealed record StockMovementResponse(
    long Id,
    int InventoryId,
    int ProductId,
    string ProductSku,
    string ProductName,
    int WarehouseId,
    string WarehouseCode,
    int LocationId,
    string LocationCode,
    MovementType MovementType,
    decimal Quantity,
    decimal QuantityBefore,
    decimal QuantityAfter,
    string ReferenceType,
    int? ReferenceId,
    string? ReferenceNo,
    string? Note,
    int? CreatedBy,
    DateTime CreatedAt);

/// <summary>Stock in/out/balance report ("Nhập - Xuất - Tồn") for a period [From, To).</summary>
public sealed record StockReportQuery : PagedRequest
{
    public static readonly string[] SortableFields = ["productSku", "productName", "warehouseCode", "inQuantity", "outQuantity", "closingQuantity"];

    public int? WarehouseId { get; init; }

    public int? ProductId { get; init; }

    public DateTime From { get; init; }

    public DateTime To { get; init; }
}

/// <summary>
/// Per product and warehouse: Opening (stock at From) + In - Out = Closing (stock at To).
/// Computed from the movement ledger, which records every quantity change.
/// </summary>
public sealed record StockReportRow(
    int ProductId,
    string ProductSku,
    string ProductName,
    string UnitOfMeasure,
    int WarehouseId,
    string WarehouseCode,
    decimal OpeningQuantity,
    decimal InQuantity,
    decimal OutQuantity,
    decimal ClosingQuantity);
