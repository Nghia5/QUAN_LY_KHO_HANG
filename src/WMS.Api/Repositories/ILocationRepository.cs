using WMS.Api.Dtos.Common;
using WMS.Api.Dtos.Locations;
using WMS.Api.Models;

namespace WMS.Api.Repositories;

public interface ILocationRepository
{
    Task<PagedResult<LocationResponse>> SearchAsync(
        int warehouseId,
        LocationQuery query,
        CancellationToken cancellationToken);

    /// <summary>All locations of a warehouse (lightweight projection) for building the tree.</summary>
    Task<IReadOnlyList<LocationResponse>> ListAllAsync(int warehouseId, CancellationToken cancellationToken);

    Task<LocationResponse?> GetResponseAsync(int warehouseId, int id, CancellationToken cancellationToken);

    /// <summary>Tracked entity, scoped to the warehouse (returns null if it belongs to another one).</summary>
    Task<WarehouseLocation?> FindAsync(int warehouseId, int id, CancellationToken cancellationToken);

    Task<bool> CodeExistsAsync(int warehouseId, string code, CancellationToken cancellationToken);

    Task<int> CountChildrenAsync(int locationId, bool activeOnly, CancellationToken cancellationToken);

    Task<int> CountByWarehouseAsync(int warehouseId, CancellationToken cancellationToken);

    void Add(WarehouseLocation location);

    void Remove(WarehouseLocation location);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
