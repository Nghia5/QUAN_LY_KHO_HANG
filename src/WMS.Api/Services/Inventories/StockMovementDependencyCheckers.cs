using WMS.Api.Repositories;
using WMS.Api.Services.Locations;
using WMS.Api.Services.Warehouses;

namespace WMS.Api.Services.Inventories;

/// <summary>Stock history must be kept: a warehouse/location with movements can never be hard-deleted.</summary>
public sealed class StockMovementWarehouseDependencyChecker(IStockMovementRepository repository) : IWarehouseDependencyChecker
{
    public async Task<WarehouseDependency?> CheckAsync(int warehouseId, CancellationToken cancellationToken)
    {
        var count = await repository.CountByWarehouseAsync(warehouseId, cancellationToken);
        return count == 0 ? null : new WarehouseDependency($"{count} stock movement(s) in history", false, true);
    }
}

public sealed class StockMovementLocationDependencyChecker(IStockMovementRepository repository) : ILocationDependencyChecker
{
    public async Task<LocationDependency?> CheckAsync(int locationId, CancellationToken cancellationToken)
    {
        var count = await repository.CountByLocationAsync(locationId, cancellationToken);
        return count == 0 ? null : new LocationDependency($"{count} stock movement(s) in history", false, true);
    }
}
