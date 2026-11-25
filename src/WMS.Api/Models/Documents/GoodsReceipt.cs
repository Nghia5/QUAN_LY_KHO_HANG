namespace WMS.Api.Models.Documents;

/// <summary>
/// Goods receipt (phiếu nhập kho). Stock is increased only when the receipt is completed:
/// Draft → PendingApproval → Approved → Completed.
/// </summary>
public sealed class GoodsReceipt : WarehouseDocument
{
    /// <summary>Supplier id. The FK/validation is added when the Supplier module exists.</summary>
    public int? SupplierId { get; set; }

    public string? SupplierName { get; set; }

    /// <summary>Supplier delivery note / invoice / purchase order number.</summary>
    public string? ReferenceNo { get; set; }

    public DateOnly ReceiptDate { get; set; }

    public List<GoodsReceiptLine> Lines { get; set; } = [];
}

public sealed class GoodsReceiptLine
{
    public int Id { get; set; }

    public int GoodsReceiptId { get; set; }

    /// <summary>Copied from the header; composite FKs force header, line and location into one warehouse.</summary>
    public int WarehouseId { get; set; }

    public int ProductId { get; set; }

    /// <summary>Destination Bin.</summary>
    public int LocationId { get; set; }

    /// <summary>Always &gt; 0.</summary>
    public decimal Quantity { get; set; }

    public decimal? UnitCost { get; set; }

    /// <summary>Optional lot information (managed as Batch entities in W-12).</summary>
    public string? BatchCode { get; set; }

    public DateOnly? ManufactureDate { get; set; }

    public DateOnly? ExpiryDate { get; set; }

    public string? Note { get; set; }

    public Product? Product { get; set; }

    public WarehouseLocation? Location { get; set; }
}
