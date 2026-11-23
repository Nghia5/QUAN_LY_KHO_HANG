namespace WMS.Api.Models;

/// <summary>
/// A lot of one product (mã lô) with optional manufacture and expiry dates.
/// Stock per lot and bin is kept in <see cref="InventoryLot"/>; the batch quantity is their sum.
/// </summary>
public sealed class Batch
{
    public int Id { get; set; }

    public int ProductId { get; set; }

    /// <summary>Unique per product, stored upper-case.</summary>
    public required string BatchCode { get; set; }

    public DateOnly? ManufactureDate { get; set; }

    public DateOnly? ExpiryDate { get; set; }

    public string? Note { get; set; }

    public int? CreatedBy { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public Product? Product { get; set; }

    public List<InventoryLot> Lots { get; set; } = [];
}

/// <summary>
/// Quantity of one batch inside one inventory row (product + bin).
/// Invariant (kept by the stock ledger): SUM(lots of a row) &lt;= Inventory.Quantity; the rest is
/// stock without lot information.
/// </summary>
public sealed class InventoryLot
{
    public int Id { get; set; }

    public int InventoryId { get; set; }

    public int BatchId { get; set; }

    /// <summary>Denormalized so composite FKs force batch and inventory to be of the same product.</summary>
    public int ProductId { get; set; }

    public decimal Quantity { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public Inventory? Inventory { get; set; }

    public Batch? Batch { get; set; }
}
