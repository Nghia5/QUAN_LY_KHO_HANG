using Microsoft.EntityFrameworkCore;
using WMS.Api.Data;
using WMS.Api.Models;
using WMS.Api.Services.Locations;
using WMS.Api.Services.Warehouses;

namespace WMS.Api.Services.Inventories;

/// <summary>
/// Stock on hand blocks deactivation and deletion; an empty inventory row (Quantity = 0)
/// still blocks hard deletion because it is referenced by stock history.
/// </summary>
public sealed class InventoryWarehouseDependencyChecker(WmsDbContext dbContext) : IWarehouseDependencyChecker
{
    public async Task<WarehouseDependency?> CheckAsync(int warehouseId, CancellationToken cancellationToken)
    {
        var summary = await dbContext.Set<Inventory>()
            .Where(inventory => inventory.WarehouseId == warehouseId)
            .GroupBy(_ => 1)
            .Select(group => new { Rows = group.Count(), WithStock = group.Count(inventory => inventory.Quantity > 0) })
            .SingleOrDefaultAsync(cancellationToken);

        if (summary is null)
        {
            return null;
        }

        return summary.WithStock > 0
            ? new WarehouseDependency($"{summary.WithStock} inventory record(s) with stock on hand", true, true)
            : new WarehouseDependency($"{summary.Rows} inventory record(s)", false, true);
    }
}

public sealed class InventoryLocationDependencyChecker(WmsDbContext dbContext) : ILocationDependencyChecker
{
    public async Task<LocationDependency?> CheckAsync(int locationId, CancellationToken cancellationToken)
    {
        var summary = await dbContext.Set<Inventory>()
            .Where(inventory => inventory.LocationId == locationId)
            .GroupBy(_ => 1)
            .Select(group => new { Rows = group.Count(), WithStock = group.Count(inventory => inventory.Quantity > 0) })
            .SingleOrDefaultAsync(cancellationToken);

        if (summary is null)
        {
            return null;
        }

        return summary.WithStock > 0
            ? new LocationDependency($"{summary.WithStock} product(s) still in stock at this location", true, true)
            : new LocationDependency($"{summary.Rows} inventory record(s)", false, true);
    }
}
