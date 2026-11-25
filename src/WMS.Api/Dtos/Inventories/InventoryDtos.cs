using WMS.Api.Dtos.Common;

namespace WMS.Api.Dtos.Inventories;

/// <summary>Stock status derived from quantities (see spec section 4 and 7).</summary>
public enum StockStatus
{
    /// <summary>AvailableQuantity &gt; MinQuantity.</summary>
    InStock = 1,

    /// <summary>0 &lt; AvailableQuantity &lt;= MinQuantity.</summary>
    LowStock = 2,

    /// <summary>AvailableQuantity = 0 (nothing left to issue).</summary>
    OutOfStock = 3
}

/// <summary>
/// Inventory list query. Keyword matches product SKU/name, location code and warehouse code.
/// </summary>
public sealed record InventoryQuery : PagedRequest
{
    public static readonly string[] SortableFields =
    [
        "productSku", "productName", "warehouseCode", "locationCode",
        "quantity", "reservedQuantity", "availableQuantity", "minQuantity", "updatedAt"
    ];

    public int? WarehouseId { get; init; }

    public int? LocationId { get; init; }

    public int? ProductId { get; init; }

    public string? Category { get; init; }

    public StockStatus? StockStatus { get; init; }

    /// <summary>When true, rows with Quantity = 0 are excluded.</summary>
    public bool? HasStock { get; init; }
}

public sealed record InventoryResponse(
    int Id,
    int ProductId,
    string ProductSku,
    string ProductName,
    string UnitOfMeasure,
    int WarehouseId,
    string WarehouseCode,
    string WarehouseName,
    int LocationId,
    string LocationCode,
    decimal Quantity,
    decimal ReservedQuantity,
    decimal AvailableQuantity,
    decimal MinQuantity,
    StockStatus StockStatus,
    DateTime? UpdatedAt);
