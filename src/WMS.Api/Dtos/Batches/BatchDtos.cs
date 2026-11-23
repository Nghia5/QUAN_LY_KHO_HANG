using WMS.Api.Dtos.Common;

namespace WMS.Api.Dtos.Batches;

public enum ExpiryStatus
{
    /// <summary>No expiry date.</summary>
    NoExpiry = 1,

    /// <summary>Expires after the near-expiry window.</summary>
    Valid = 2,

    /// <summary>Expires today or within the near-expiry window (default 30 days).</summary>
    NearExpiry = 3,

    /// <summary>Expiry date is before today.</summary>
    Expired = 4
}

public enum PickStrategy
{
    /// <summary>First Expired, First Out: earliest expiry date first.</summary>
    Fefo = 1,

    /// <summary>First In, First Out: oldest lot (manufacture date, then registration) first.</summary>
    Fifo = 2
}

public sealed record CreateBatchRequest
{
    public int ProductId { get; init; }

    public required string BatchCode { get; init; }

    public DateOnly? ManufactureDate { get; init; }

    public DateOnly? ExpiryDate { get; init; }

    public string? Note { get; init; }
}

/// <summary>Batch code and product cannot change; dates can be corrected (audited).</summary>
public sealed record UpdateBatchRequest
{
    public DateOnly? ManufactureDate { get; init; }

    public DateOnly? ExpiryDate { get; init; }

    public string? Note { get; init; }
}

/// <summary>Keyword matches batch code, product SKU and name.</summary>
public sealed record BatchQuery : PagedRequest
{
    public static readonly string[] SortableFields = ["batchCode", "expiryDate", "manufactureDate", "createdAt", "productSku"];

    public int? ProductId { get; init; }

    /// <summary>Quantity is counted only in this warehouse; with HasStock, only batches stocked there.</summary>
    public int? WarehouseId { get; init; }

    public ExpiryStatus? ExpiryStatus { get; init; }

    /// <summary>Batches expiring from today up to N days ahead (not expired yet).</summary>
    public int? ExpiringWithinDays { get; init; }

    public bool? HasStock { get; init; }
}

public sealed record BatchResponse(
    int Id,
    int ProductId,
    string ProductSku,
    string ProductName,
    string BatchCode,
    DateOnly? ManufactureDate,
    DateOnly? ExpiryDate,
    int? DaysToExpiry,
    ExpiryStatus ExpiryStatus,
    decimal Quantity,
    string? Note,
    DateTime CreatedAt);

public sealed record BatchLotResponse(
    int WarehouseId,
    string WarehouseCode,
    int LocationId,
    string LocationCode,
    decimal Quantity);

public sealed record BatchDetail(BatchResponse Batch, IReadOnlyList<BatchLotResponse> Lots);

public sealed record ExpiryAlertQuery : PagedRequest
{
    public int? WarehouseId { get; init; }

    public int? ProductId { get; init; }

    /// <summary>Near-expiry window in days; defaults to Warehouse:NearExpiryDays.</summary>
    public int? Days { get; init; }

    /// <summary>NearExpiry or Expired; null = both.</summary>
    public ExpiryStatus? Status { get; init; }
}

/// <summary>Stock of a lot at a bin that is expired or about to expire (feeds NEAR_EXPIRY / EXPIRED alerts).</summary>
public sealed record ExpiryAlertRow(
    int BatchId,
    string BatchCode,
    int ProductId,
    string ProductSku,
    string ProductName,
    int WarehouseId,
    string WarehouseCode,
    int LocationId,
    string LocationCode,
    decimal Quantity,
    DateOnly ExpiryDate,
    int DaysToExpiry,
    ExpiryStatus Status);

public sealed record PickSuggestionQuery
{
    public int ProductId { get; init; }

    public int WarehouseId { get; init; }

    public decimal Quantity { get; init; }

    public PickStrategy Strategy { get; init; } = PickStrategy.Fefo;

    public bool IncludeExpired { get; init; }
}

/// <param name="BatchId">null = stock without lot information (always picked last).</param>
public sealed record PickSuggestionLine(
    int? BatchId,
    string? BatchCode,
    DateOnly? ManufactureDate,
    DateOnly? ExpiryDate,
    int LocationId,
    string LocationCode,
    decimal AvailableQuantity,
    decimal PickQuantity);

/// <param name="Shortage">Requested quantity that cannot be covered (0 when fully covered).</param>
public sealed record PickSuggestion(
    PickStrategy Strategy,
    decimal RequestedQuantity,
    decimal SuggestedQuantity,
    decimal Shortage,
    IReadOnlyList<PickSuggestionLine> Lines);
