namespace WMS.Api.Models.Documents;

/// <summary>
/// Stock transfer (phiếu chuyển kho) between two warehouses or between bins of one warehouse.
/// <see cref="WarehouseDocument.WarehouseId"/> is the source warehouse.
/// Flow: Draft → PendingApproval → Approved (source reserved) → Picked → InTransit (TRANSFER_OUT)
/// → Received (TRANSFER_IN) → Completed. Cancel is possible until Picked (reservation released).
/// </summary>
public sealed class StockTransfer : WarehouseDocument
{
    public override DocumentFlow Flow => DocumentFlow.Transfer;

    public int DestinationWarehouseId { get; set; }

    /// <summary>True when source and destination are the same warehouse (bin-to-bin move).</summary>
    public bool IsInternal => WarehouseId == DestinationWarehouseId;

    public string? Reason { get; set; }

    public int? PickedBy { get; set; }

    public DateTime? PickedAt { get; set; }

    public int? ShippedBy { get; set; }

    public DateTime? ShippedAt { get; set; }

    public int? ReceivedBy { get; set; }

    public DateTime? ReceivedAt { get; set; }

    public Warehouse? DestinationWarehouse { get; set; }

    public List<StockTransferLine> Lines { get; set; } = [];
}

public sealed class StockTransferLine
{
    public int Id { get; set; }

    public int StockTransferId { get; set; }

    /// <summary>Source warehouse (copied from the header).</summary>
    public int WarehouseId { get; set; }

    /// <summary>Destination warehouse (copied from the header).</summary>
    public int DestinationWarehouseId { get; set; }

    public int ProductId { get; set; }

    /// <summary>Bin in the source warehouse.</summary>
    public int FromLocationId { get; set; }

    /// <summary>Bin in the destination warehouse.</summary>
    public int ToLocationId { get; set; }

    /// <summary>Always &gt; 0.</summary>
    public decimal Quantity { get; set; }

    public string? Note { get; set; }

    public Product? Product { get; set; }

    public WarehouseLocation? FromLocation { get; set; }

    public WarehouseLocation? ToLocation { get; set; }
}
