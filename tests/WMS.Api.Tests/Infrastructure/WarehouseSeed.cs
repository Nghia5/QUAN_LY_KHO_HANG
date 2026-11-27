using WMS.Api.Data;
using WMS.Api.Models;

namespace WMS.Api.Tests.Infrastructure;

/// <summary>Helpers to create a warehouse with a Zone > Rack > Shelf chain and bins.</summary>
public static class WarehouseSeed
{
    public static async Task<Product> ProductAsync(WmsDbContext context, string sku)
    {
        var product = new Product { Sku = sku, Name = $"Product {sku}", Category = "Test", UnitOfMeasure = "PCS" };
        context.Add(product);
        await context.SaveChangesAsync();
        return product;
    }

    public static async Task<(Warehouse Warehouse, WarehouseLocation Shelf, List<WarehouseLocation> Bins)> WarehouseAsync(
        WmsDbContext context,
        string code,
        int binCount)
    {
        var warehouse = new Warehouse { Code = code, Name = code };
        context.Add(warehouse);
        await context.SaveChangesAsync();

        var zone = await AddAsync(context, warehouse.Id, null, LocationType.Zone, "Z", "Z");
        var rack = await AddAsync(context, warehouse.Id, zone.Id, LocationType.Rack, "01", "Z-01");
        var shelf = await AddAsync(context, warehouse.Id, rack.Id, LocationType.Shelf, "01", "Z-01-01");

        var bins = new List<WarehouseLocation>();
        for (var i = 1; i <= binCount; i++)
        {
            bins.Add(await AddAsync(context, warehouse.Id, shelf.Id, LocationType.Bin, $"B{i}", $"Z-01-01-B{i}"));
        }

        return (warehouse, shelf, bins);
    }

    private static async Task<WarehouseLocation> AddAsync(
        WmsDbContext context, int warehouseId, int? parentId, LocationType type, string segment, string code)
    {
        var location = new WarehouseLocation
        {
            WarehouseId = warehouseId, ParentId = parentId, Type = type, SegmentCode = segment, Code = code, Name = code
        };
        context.Add(location);
        await context.SaveChangesAsync();
        return location;
    }
}
