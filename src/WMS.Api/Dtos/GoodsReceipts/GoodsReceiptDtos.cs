using WMS.Api.Authorization;
using WMS.Api.Dtos.Common;
using WMS.Api.Dtos.Documents;
using WMS.Api.Models.Documents;
using WMS.Api.Services.Documents;

namespace WMS.Api.Dtos.GoodsReceipts;

public sealed record GoodsReceiptLineRequest
{
    public int ProductId { get; init; }

    /// <summary>Destination Bin in the receipt's warehouse.</summary>
    public int LocationId { get; init; }

    public decimal Quantity { get; init; }

    public decimal? UnitCost { get; init; }

    public string? BatchCode { get; init; }

    public DateOnly? ManufactureDate { get; init; }

    public DateOnly? ExpiryDate { get; init; }

    public string? Note { get; init; }
}

/// <summary>Fields shared by create and update requests (validated by one validator).</summary>
public interface IGoodsReceiptPayload
{
    int? SupplierId { get; }

    string? SupplierName { get; }

    string? ReferenceNo { get; }

    DateOnly? ReceiptDate { get; }

    string? Note { get; }

    IReadOnlyList<GoodsReceiptLineRequest> Lines { get; }
}

/// <summary>Creates a Draft receipt. The warehouse cannot be changed afterwards.</summary>
public sealed record CreateGoodsReceiptRequest : IGoodsReceiptPayload, IWarehouseRequest
{
    public int WarehouseId { get; init; }

    public int? SupplierId { get; init; }

    public string? SupplierName { get; init; }

    public string? ReferenceNo { get; init; }

    /// <summary>Defaults to today (UTC) when omitted.</summary>
    public DateOnly? ReceiptDate { get; init; }

    public string? Note { get; init; }

    public IReadOnlyList<GoodsReceiptLineRequest> Lines { get; init; } = [];
}

/// <summary>Replaces header fields and all lines of a Draft receipt.</summary>
public sealed record UpdateGoodsReceiptRequest : IGoodsReceiptPayload
{
    public int? SupplierId { get; init; }

    public string? SupplierName { get; init; }

    public string? ReferenceNo { get; init; }

    public DateOnly? ReceiptDate { get; init; }

    public string? Note { get; init; }

    public IReadOnlyList<GoodsReceiptLineRequest> Lines { get; init; } = [];
}

/// <summary>Keyword matches document number, reference number and supplier name.</summary>
public sealed record GoodsReceiptQuery : PagedRequest
{
    public static readonly string[] SortableFields = ["documentNo", "receiptDate", "status", "createdAt", "totalQuantity"];

    public int? WarehouseId { get; init; }

    public DocumentStatus? Status { get; init; }

    public int? SupplierId { get; init; }

    public int? ProductId { get; init; }

    public DateOnly? FromDate { get; init; }

    public DateOnly? ToDate { get; init; }
}

public sealed record GoodsReceiptSummary(
    int Id,
    string DocumentNo,
    int WarehouseId,
    string WarehouseCode,
    DocumentStatus Status,
    int? SupplierId,
    string? SupplierName,
    string? ReferenceNo,
    DateOnly ReceiptDate,
    int LineCount,
    decimal TotalQuantity,
    int? CreatedBy,
    DateTime CreatedAt);

public sealed record GoodsReceiptLineResponse(
    int Id,
    int ProductId,
    string ProductSku,
    string ProductName,
    string UnitOfMeasure,
    int LocationId,
    string LocationCode,
    decimal Quantity,
    decimal? UnitCost,
    string? BatchCode,
    DateOnly? ManufactureDate,
    DateOnly? ExpiryDate,
    string? Note);

public sealed record GoodsReceiptDetail(
    int Id,
    string DocumentNo,
    int WarehouseId,
    string WarehouseCode,
    string WarehouseName,
    DocumentStatus Status,
    int? SupplierId,
    string? SupplierName,
    string? ReferenceNo,
    DateOnly ReceiptDate,
    string? Note,
    decimal TotalQuantity,
    IReadOnlyList<GoodsReceiptLineResponse> Lines,
    DocumentWorkflowInfo Workflow,
    IReadOnlyList<DocumentAction> AvailableActions);
