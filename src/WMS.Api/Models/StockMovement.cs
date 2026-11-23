namespace WMS.Api.Models;

/// <summary>Kind of stock change. *In types increase stock (Quantity &gt; 0), *Out types decrease it.</summary>
public enum MovementType
{
    Receipt = 1,
    Issue = 2,
    TransferIn = 3,
    TransferOut = 4,
    AdjustmentIn = 5,
    AdjustmentOut = 6,
    StocktakeIn = 7,
    StocktakeOut = 8
}

public static class MovementTypeExtensions
{
    public static bool IsInbound(this MovementType type) => type is
        MovementType.Receipt or MovementType.TransferIn or MovementType.AdjustmentIn or MovementType.StocktakeIn;
}

/// <summary>
/// Append-only stock ledger entry. Exactly one row is written for every change of
/// <see cref="Inventory.Quantity"/>, in the same transaction. Rows are never updated or deleted
/// (enforced by the application and by a database trigger).
/// </summary>
public sealed class StockMovement
{
    public long Id { get; set; }

    public int InventoryId { get; set; }

    public int ProductId { get; set; }

    public int WarehouseId { get; set; }

    public int LocationId { get; set; }

    public MovementType MovementType { get; set; }

    /// <summary>Signed change: positive for inbound, negative for outbound. Never 0.</summary>
    public decimal Quantity { get; set; }

    /// <summary>Inventory.Quantity right before the change (read from the locked row).</summary>
    public decimal QuantityBefore { get; set; }

    /// <summary>Inventory.Quantity right after the change. Always QuantityBefore + Quantity.</summary>
    public decimal QuantityAfter { get; set; }

    /// <summary>Source document type, e.g. GOODS_RECEIPT, GOODS_ISSUE, STOCK_TRANSFER.</summary>
    public required string ReferenceType { get; set; }

    public int? ReferenceId { get; set; }

    public string? ReferenceNo { get; set; }

    public string? Note { get; set; }

    public int? CreatedBy { get; set; }

    public DateTime CreatedAt { get; set; }

    public Product? Product { get; set; }

    public Warehouse? Warehouse { get; set; }

    public WarehouseLocation? Location { get; set; }
}
