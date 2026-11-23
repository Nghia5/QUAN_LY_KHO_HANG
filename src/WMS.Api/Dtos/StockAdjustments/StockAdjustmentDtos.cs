using WMS.Api.Dtos.Common;
using WMS.Api.Dtos.Documents;
using WMS.Api.Models.Documents;
using WMS.Api.Services.Documents;

namespace WMS.Api.Dtos.StockAdjustments;

public sealed record StockAdjustmentLineRequest
{
    public int ProductId { get; init; }

    public int LocationId { get; init; }

    /// <summary>Signed: +5 adds 5, -5 removes 5. Never 0.</summary>
    public decimal AdjustmentQuantity { get; init; }

    public string? Note { get; init; }
}

public interface IStockAdjustmentPayload
{
    AdjustmentReason ReasonCode { get; }

    string Reason { get; }

    string? Note { get; }

    IReadOnlyList<StockAdjustmentLineRequest> Lines { get; }
}

public sealed record CreateStockAdjustmentRequest : IStockAdjustmentPayload
{
    public int WarehouseId { get; init; }

    public AdjustmentReason ReasonCode { get; init; }

    /// <summary>Mandatory explanation (min 5 characters).</summary>
    public string Reason { get; init; } = "";

    public string? Note { get; init; }

    public IReadOnlyList<StockAdjustmentLineRequest> Lines { get; init; } = [];
}

public sealed record UpdateStockAdjustmentRequest : IStockAdjustmentPayload
{
    public AdjustmentReason ReasonCode { get; init; }

    public string Reason { get; init; } = "";

    public string? Note { get; init; }

    public IReadOnlyList<StockAdjustmentLineRequest> Lines { get; init; } = [];
}

public sealed record StockAdjustmentQuery : PagedRequest
{
    public static readonly string[] SortableFields = ["documentNo", "status", "createdAt", "reasonCode"];

    public int? WarehouseId { get; init; }

    public DocumentStatus? Status { get; init; }

    public AdjustmentReason? ReasonCode { get; init; }

    public int? ProductId { get; init; }
}

public sealed record StockAdjustmentSummary(
    int Id,
    string DocumentNo,
    int WarehouseId,
    string WarehouseCode,
    DocumentStatus Status,
    AdjustmentReason ReasonCode,
    string Reason,
    int LineCount,
    decimal TotalIncrease,
    decimal TotalDecrease,
    int? CreatedBy,
    DateTime CreatedAt);

/// <param name="CurrentQuantity">Live stock (preview before the adjustment is applied).</param>
/// <param name="ExpectedAfterQuantity">CurrentQuantity + AdjustmentQuantity (preview).</param>
/// <param name="BeforeQuantity">Recorded when applied.</param>
/// <param name="AfterQuantity">Recorded when applied.</param>
public sealed record StockAdjustmentLineResponse(
    int Id,
    int ProductId,
    string ProductSku,
    string ProductName,
    string UnitOfMeasure,
    int LocationId,
    string LocationCode,
    decimal AdjustmentQuantity,
    decimal CurrentQuantity,
    decimal CurrentReservedQuantity,
    decimal ExpectedAfterQuantity,
    decimal? BeforeQuantity,
    decimal? AfterQuantity,
    string? Note);

public sealed record StockAdjustmentDetail(
    int Id,
    string DocumentNo,
    int WarehouseId,
    string WarehouseCode,
    DocumentStatus Status,
    AdjustmentReason ReasonCode,
    string Reason,
    string? Note,
    bool RequiresApproval,
    IReadOnlyList<StockAdjustmentLineResponse> Lines,
    DocumentWorkflowInfo Workflow,
    IReadOnlyList<DocumentAction> AvailableActions);
