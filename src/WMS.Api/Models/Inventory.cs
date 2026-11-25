namespace WMS.Api.Models;

/// <summary>
/// Stock of one product at one location (Bin) of one warehouse.
/// Quantities only change through warehouse documents (receipt, issue, transfer, stocktake,
/// adjustment) and every change of <see cref="Quantity"/> is recorded as a stock movement.
/// </summary>
public sealed class Inventory
{
    public int Id { get; set; }

    public int ProductId { get; set; }

    public int WarehouseId { get; set; }

    /// <summary>Must be a location of <see cref="WarehouseId"/> (enforced by a composite FK).</summary>
    public int LocationId { get; set; }

    /// <summary>Physical quantity on hand. Never negative.</summary>
    public decimal Quantity { get; set; }

    /// <summary>Held for approved issues/transfers that are not completed yet. 0 &lt;= Reserved &lt;= Quantity.</summary>
    public decimal ReservedQuantity { get; set; }

    /// <summary>
    /// Quantity - ReservedQuantity. Persisted computed column calculated by SQL Server;
    /// read-only for the application.
    /// </summary>
    public decimal AvailableQuantity { get; private set; }

    /// <summary>LOW_STOCK threshold: alert when 0 &lt; AvailableQuantity &lt;= MinQuantity.</summary>
    public decimal MinQuantity { get; set; }

    /// <summary>Optimistic concurrency token.</summary>
    public byte[] RowVersion { get; set; } = [];

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public Product? Product { get; set; }

    public Warehouse? Warehouse { get; set; }

    public WarehouseLocation? Location { get; set; }
}
