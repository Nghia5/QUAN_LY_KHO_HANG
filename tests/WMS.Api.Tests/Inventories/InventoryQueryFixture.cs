using WMS.Api.Models;
using WMS.Api.Tests.Infrastructure;

namespace WMS.Api.Tests.Inventories;

/// <summary>
/// Deterministic data set (20 inventory rows):
/// <list type="bullet">
/// <item>Products P01..P10; odd = "Food", even = "Tools".</item>
/// <item>WH1 bin B1: every product, Quantity = i*10, MinQuantity = 25 (P01, P02 low stock; P03..P10 in stock).</item>
/// <item>WH1 bin B2: P01..P05 with Quantity = 0 (out of stock, no stock).</item>
/// <item>WH2 bin C1: P01..P05 with Quantity = 100 fully reserved (out of stock, has stock).</item>
/// </list>
/// </summary>
public sealed class InventoryQueryFixture : IAsyncLifetime
{
    public SqlServerDatabaseFixture Database { get; } = new();

    public int Warehouse1Id { get; private set; }

    public int Warehouse2Id { get; private set; }

    public int BinB1Id { get; private set; }

    public int BinB2Id { get; private set; }

    public int BinC1Id { get; private set; }

    public Dictionary<int, int> ProductIds { get; } = [];

    public async Task InitializeAsync()
    {
        await Database.InitializeAsync();
        await using var context = Database.CreateContext();

        var products = Enumerable.Range(1, 10)
            .Select(i => new Product
            {
                Sku = $"SKU-P{i:00}",
                Name = $"Product {i:00}",
                Category = i % 2 == 1 ? "Food" : "Tools",
                UnitOfMeasure = "PCS",
                SalePrice = i
            })
            .ToList();
        context.AddRange(products);

        var wh1 = new Warehouse { Code = "WH1", Name = "Warehouse 1" };
        var wh2 = new Warehouse { Code = "WH2", Name = "Warehouse 2" };
        context.AddRange(wh1, wh2);
        await context.SaveChangesAsync();

        for (var i = 0; i < products.Count; i++)
        {
            ProductIds[i + 1] = products[i].Id;
        }

        Warehouse1Id = wh1.Id;
        Warehouse2Id = wh2.Id;

        var shelf1 = await CreateShelfAsync(context, wh1.Id, "X");
        var shelf2 = await CreateShelfAsync(context, wh2.Id, "Y");
        var b1 = Bin(wh1.Id, shelf1, "B1");
        var b2 = Bin(wh1.Id, shelf1, "B2");
        var c1 = Bin(wh2.Id, shelf2, "C1");
        context.AddRange(b1, b2, c1);
        await context.SaveChangesAsync();

        BinB1Id = b1.Id;
        BinB2Id = b2.Id;
        BinC1Id = c1.Id;

        for (var i = 1; i <= 10; i++)
        {
            context.Add(new Inventory
            {
                ProductId = ProductIds[i], WarehouseId = wh1.Id, LocationId = b1.Id,
                Quantity = i * 10, MinQuantity = 25
            });
        }

        for (var i = 1; i <= 5; i++)
        {
            context.Add(new Inventory
            {
                ProductId = ProductIds[i], WarehouseId = wh1.Id, LocationId = b2.Id, Quantity = 0
            });
            context.Add(new Inventory
            {
                ProductId = ProductIds[i], WarehouseId = wh2.Id, LocationId = c1.Id,
                Quantity = 100, ReservedQuantity = 100
            });
        }

        await context.SaveChangesAsync();
    }

    public Task DisposeAsync() => Database.DisposeAsync();

    private static async Task<WarehouseLocation> CreateShelfAsync(Data.WmsDbContext context, int warehouseId, string zone)
    {
        var zoneLocation = new WarehouseLocation
        {
            WarehouseId = warehouseId, Type = LocationType.Zone, SegmentCode = zone, Code = zone, Name = zone
        };
        context.Add(zoneLocation);
        await context.SaveChangesAsync();

        var rack = new WarehouseLocation
        {
            WarehouseId = warehouseId, ParentId = zoneLocation.Id, Type = LocationType.Rack,
            SegmentCode = "01", Code = $"{zone}-01", Name = "Rack"
        };
        context.Add(rack);
        await context.SaveChangesAsync();

        var shelf = new WarehouseLocation
        {
            WarehouseId = warehouseId, ParentId = rack.Id, Type = LocationType.Shelf,
            SegmentCode = "01", Code = $"{zone}-01-01", Name = "Shelf"
        };
        context.Add(shelf);
        await context.SaveChangesAsync();
        return shelf;
    }

    private static WarehouseLocation Bin(int warehouseId, WarehouseLocation shelf, string code) => new()
    {
        WarehouseId = warehouseId, ParentId = shelf.Id, Type = LocationType.Bin,
        SegmentCode = code, Code = $"{shelf.Code}-{code}", Name = code
    };
}
