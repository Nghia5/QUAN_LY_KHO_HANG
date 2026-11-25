namespace WMS.Api.Dtos;

/// <summary>Product details returned by the API.</summary>
public sealed record ProductResponse(
    int Id,
    string Sku,
    string Name,
    string? Description,
    string Category,
    string UnitOfMeasure,
    decimal SalePrice,
    bool IsActive);
