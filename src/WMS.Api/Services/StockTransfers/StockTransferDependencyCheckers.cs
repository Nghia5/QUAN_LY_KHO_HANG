using WMS.Api.Repositories;
using WMS.Api.Services.Locations;
using WMS.Api.Services.Warehouses;

namespace WMS.Api.Services.StockTransfers;

/// <summary>Open transfers (as source or destination) block deactivation; any transfer blocks deletion.</summary>
public sealed class StockTransferWarehouseDependencyChecker(IStockTransferRepository repository) : IWarehouseDependencyChecker
{
    public async Task<WarehouseDependency?> CheckAsync(int warehouseId, CancellationToken cancellationToken)
    {
        var open = await repository.CountOpenByWarehouseAsync(warehouseId, cancellationToken);
        if (open > 0)
        {
            return new WarehouseDependency($"{open} open stock transfer(s)", true, true);
        }

        var all = await repository.CountByWarehouseAsync(warehouseId, cancellationToken);
        return all > 0 ? new WarehouseDependency($"{all} stock transfer(s)", false, true) : null;
    }
}

public sealed class StockTransferLocationDependencyChecker(IStockTransferRepository repository) : ILocationDependencyChecker
{
    public async Task<LocationDependency?> CheckAsync(int locationId, CancellationToken cancellationToken)
    {
        var open = await repository.CountOpenByLocationAsync(locationId, cancellationToken);
        return open > 0 ? new LocationDependency($"used by {open} open stock transfer(s)", true, true) : null;
    }
}
