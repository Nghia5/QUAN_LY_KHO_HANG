using Microsoft.EntityFrameworkCore;
using WMS.Api.Data;
using WMS.Api.Exceptions;
using WMS.Api.Models;

namespace WMS.Api.Services.Documents;

/// <summary>
/// Loads, in 3 queries, the master data needed to validate the lines of a document
/// (warehouse, products, locations of that warehouse) and offers per-line checks.
/// </summary>
public sealed class DocumentLineValidator
{
    private DocumentLineValidator(
        string warehouseCode,
        IReadOnlyDictionary<int, ProductInfo> products,
        IReadOnlyDictionary<int, LocationInfo> locations)
    {
        WarehouseCode = warehouseCode;
        Products = products;
        Locations = locations;
    }

    public string WarehouseCode { get; }

    public IReadOnlyDictionary<int, ProductInfo> Products { get; }

    public IReadOnlyDictionary<int, LocationInfo> Locations { get; }

    public sealed record ProductInfo(int Id, string Sku, bool IsActive);

    public sealed record LocationInfo(int Id, string Code, LocationType Type, bool IsActive);

    /// <summary>Throws 422 when the warehouse does not exist or is inactive.</summary>
    public static async Task<DocumentLineValidator> LoadAsync(
        WmsDbContext dbContext,
        int warehouseId,
        IReadOnlyCollection<(int ProductId, int LocationId)> lines,
        CancellationToken cancellationToken)
    {
        var warehouse = await dbContext.Set<Warehouse>()
            .AsNoTracking()
            .Where(w => w.Id == warehouseId)
            .Select(w => new { w.Code, w.IsActive })
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new BusinessRuleException($"Warehouse {warehouseId} does not exist.");
        if (!warehouse.IsActive)
        {
            throw new BusinessRuleException($"Warehouse '{warehouse.Code}' is inactive.");
        }

        var productIds = lines.Select(line => line.ProductId).Distinct().ToList();
        var products = await dbContext.Set<Product>()
            .AsNoTracking()
            .Where(p => productIds.Contains(p.Id))
            .Select(p => new ProductInfo(p.Id, p.Sku, p.IsActive))
            .ToDictionaryAsync(p => p.Id, cancellationToken);

        var locationIds = lines.Select(line => line.LocationId).Distinct().ToList();
        var locations = await dbContext.Set<WarehouseLocation>()
            .AsNoTracking()
            .Where(l => locationIds.Contains(l.Id) && l.WarehouseId == warehouseId)
            .Select(l => new LocationInfo(l.Id, l.Code, l.Type, l.IsActive))
            .ToDictionaryAsync(l => l.Id, cancellationToken);

        return new DocumentLineValidator(warehouse.Code, products, locations);
    }

    public void CheckProduct(List<string> errors, string prefix, int productId, bool requireActive)
    {
        if (!Products.TryGetValue(productId, out var product))
        {
            errors.Add($"{prefix} product {productId} does not exist.");
        }
        else if (requireActive && !product.IsActive)
        {
            errors.Add($"{prefix} product '{product.Sku}' is inactive.");
        }
    }

    public void CheckBin(List<string> errors, string prefix, int locationId, bool requireActive, string notBinMessage)
    {
        if (!Locations.TryGetValue(locationId, out var location))
        {
            errors.Add($"{prefix} location {locationId} does not belong to warehouse '{WarehouseCode}'.");
        }
        else if (location.Type != LocationType.Bin)
        {
            errors.Add($"{prefix} '{location.Code}' is a {location.Type}; {notBinMessage}.");
        }
        else if (requireActive && !location.IsActive)
        {
            errors.Add($"{prefix} location '{location.Code}' is inactive.");
        }
    }

    /// <summary>Messages for keys that appear on more than one line (1-based line numbers).</summary>
    public static IEnumerable<string> Duplicates(IReadOnlyList<object> keys, string message) =>
        keys.Select((key, index) => (Key: key, Number: index + 1))
            .GroupBy(item => item.Key)
            .Where(group => group.Count() > 1)
            .Select(group => $"Lines {string.Join(", ", group.Select(item => item.Number))} {message}");

    public static void ThrowIfAny(List<string> errors)
    {
        if (errors.Count > 0)
        {
            throw new BusinessRuleException(string.Join(" ", errors));
        }
    }
}
