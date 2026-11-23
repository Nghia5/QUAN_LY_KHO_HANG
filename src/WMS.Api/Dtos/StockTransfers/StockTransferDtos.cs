using WMS.Api.Dtos.Common;
using WMS.Api.Dtos.Documents;
using WMS.Api.Models.Documents;
using WMS.Api.Services.Documents;

namespace WMS.Api.Dtos.StockTransfers;

public sealed record StockTransferLineRequest
{
    public int ProductId { get; init; }

    /// <summary>Bin in the source warehouse.</summary>
    public int FromLocationId { get; init; }

    /// <summary>Bin in the destination warehouse.</summary>
    public int ToLocationId { get; init; }

    public decimal Quantity { get; init; }

    public string? Note { get; init; }
}

public interface IStockTransferPayload
{
    string? Reason { get; }

    string? Note { get; }

    IReadOnlyList<StockTransferLineRequest> Lines { get; }
}

/// <summary>
/// Creates a Draft transfer. Use the same warehouse for source and destination to move goods
/// between bins. Warehouses cannot be changed afterwards.
/// </summary>
public sealed record CreateStockTransferRequest : IStockTransferPayload
{
    /// <summary>Source warehouse.</summary>
    public int WarehouseId { get; init; }

    public int DestinationWarehouseId { get; init; }

    public string? Reason { get; init; }

    public string? Note { get; init; }

    public IReadOnlyList<StockTransferLineRequest> Lines { get; init; } = [];
}

/// <summary>Replaces reason, note and all lines of a Draft transfer.</summary>
public sealed record UpdateStockTransferRequest : IStockTransferPayload
{
    public string? Reason { get; init; }

    public string? Note { get; init; }

    public IReadOnlyList<StockTransferLineRequest> Lines { get; init; } = [];
}

/// <summary>
/// Keyword matches the document number. <see cref="WarehouseId"/> matches source OR destination.
/// </summary>
public sealed record StockTransferQuery : PagedRequest
{
    public static readonly string[] SortableFields = ["documentNo", "status", "createdAt", "totalQuantity"];

    public int? WarehouseId { get; init; }

    public int? SourceWarehouseId { get; init; }

    public int? DestinationWarehouseId { get; init; }

    public DocumentStatus? Status { get; init; }

    public int? ProductId { get; init; }

    public bool? IsInternal { get; init; }
}

public sealed record StockTransferSummary(
    int Id,
    string DocumentNo,
    int SourceWarehouseId,
    string SourceWarehouseCode,
    int DestinationWarehouseId,
    string DestinationWarehouseCode,
    bool IsInternal,
    DocumentStatus Status,
    int LineCount,
    decimal TotalQuantity,
    int? CreatedBy,
    DateTime CreatedAt);

/// <param name="AvailableAtSource">Current available stock at the source bin (live, for the UI).</param>
public sealed record StockTransferLineResponse(
    int Id,
    int ProductId,
    string ProductSku,
    string ProductName,
    string UnitOfMeasure,
    int FromLocationId,
    string FromLocationCode,
    int ToLocationId,
    string ToLocationCode,
    decimal Quantity,
    decimal AvailableAtSource,
    string? Note);

public sealed record TransferProgressInfo(
    int? PickedBy,
    DateTime? PickedAt,
    int? ShippedBy,
    DateTime? ShippedAt,
    int? ReceivedBy,
    DateTime? ReceivedAt);

public sealed record StockTransferDetail(
    int Id,
    string DocumentNo,
    int SourceWarehouseId,
    string SourceWarehouseCode,
    int DestinationWarehouseId,
    string DestinationWarehouseCode,
    bool IsInternal,
    DocumentStatus Status,
    string? Reason,
    string? Note,
    decimal TotalQuantity,
    IReadOnlyList<StockTransferLineResponse> Lines,
    DocumentWorkflowInfo Workflow,
    TransferProgressInfo Progress,
    IReadOnlyList<DocumentAction> AvailableActions);
