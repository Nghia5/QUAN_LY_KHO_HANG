using System.ComponentModel.DataAnnotations;

namespace WMS.Api.Dtos;

/// <summary>Payload for creating a product.</summary>
public sealed record CreateProductRequest
{
    [Required, StringLength(50)]
    public required string Sku { get; init; }

    [Required, StringLength(200)]
    public required string Name { get; init; }

    [StringLength(1000)]
    public string? Description { get; init; }

    [Required, StringLength(100)]
    public required string Category { get; init; }

    [Required, StringLength(50)]
    public required string UnitOfMeasure { get; init; }

    [Range(typeof(decimal), "0", "9999999999999999.99")]
    public decimal SalePrice { get; init; }

    public bool IsActive { get; init; } = true;
}
