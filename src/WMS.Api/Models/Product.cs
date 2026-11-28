namespace WMS.Api.Models;

public sealed class Product
{
    public int Id { get; set; }

    public required string Sku { get; set; }

    public required string Name { get; set; }

    public string? Description { get; set; }

    public required string Category { get; set; }

    public required string UnitOfMeasure { get; set; }

    public decimal SalePrice { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>Main supplier of the product (optional).</summary>
    public int? SupplierId { get; set; }

    public SupplierModel? Supplier { get; set; }
}
