namespace WMS.Api.Models.Documents;

public enum AdjustmentReason
{
    Damaged = 1,
    Lost = 2,
    Found = 3,
    Expired = 4,
    DataCorrection = 5,
    Other = 6
}

/// <summary>
/// Stock adjustment (phiếu điều chỉnh tồn kho): the only way to correct stock outside
/// receipts/issues/transfers/stocktakes. Draft → submit → approve (applies and completes).
/// When approval is not required by policy, submit applies it directly.
/// </summary>
public sealed class StockAdjustment : WarehouseDocument
{
    public AdjustmentReason ReasonCode { get; set; }

    /// <summary>Mandatory explanation.</summary>
    public string Reason { get; set; } = "";

    public List<StockAdjustmentLine> Lines { get; set; } = [];
}

public sealed class StockAdjustmentLine
{
    public int Id { get; set; }

    public int StockAdjustmentId { get; set; }

    public int WarehouseId { get; set; }

    public int ProductId { get; set; }

    /// <summary>Bin whose stock is adjusted.</summary>
    public int LocationId { get; set; }

    /// <summary>Signed change, never 0 (positive adds stock, negative removes it).</summary>
    public decimal AdjustmentQuantity { get; set; }

    /// <summary>Stock right before the adjustment was applied (null until applied).</summary>
    public decimal? BeforeQuantity { get; set; }

    /// <summary>Stock right after the adjustment (= Before + Adjustment, never negative).</summary>
    public decimal? AfterQuantity { get; set; }

    public string? Note { get; set; }

    public Product? Product { get; set; }

    public WarehouseLocation? Location { get; set; }
}
