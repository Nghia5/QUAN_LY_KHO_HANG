using WMS.Api.Repositories;
using WMS.Api.Services.Warehouses;

namespace WMS.Api.Services.Locations;

/// <summary>A warehouse that still has locations cannot be hard-deleted (deactivation is fine).</summary>
public sealed class LocationWarehouseDependencyChecker(ILocationRepository repository) : IWarehouseDependencyChecker
{
    public async Task<WarehouseDependency?> CheckAsync(int warehouseId, CancellationToken cancellationToken)
    {
        var count = await repository.CountByWarehouseAsync(warehouseId, cancellationToken);
        return count == 0
            ? null
            : new WarehouseDependency($"{count} location(s) defined", BlocksDeactivation: false, BlocksDeletion: true);
    }
}
