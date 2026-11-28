using WMS.Api.Dtos.Common;
using WMS.Api.Dtos.Warehouses;
using WMS.Api.Models;

namespace WMS.Api.Repositories;

public interface IWarehouseRepository
{
    Task<PagedResult<WarehouseResponse>> SearchAsync(WarehouseQuery query, CancellationToken cancellationToken);

    Task<WarehouseResponse?> GetResponseByIdAsync(int id, CancellationToken cancellationToken);

    /// <summary>Tracked entity for updates.</summary>
    Task<Warehouse?> FindAsync(int id, CancellationToken cancellationToken);

    Task<bool> CodeExistsAsync(string code, CancellationToken cancellationToken);

    void Add(Warehouse warehouse);

    void Remove(Warehouse warehouse);

    /// <summary>Saves changes; throws <see cref="Exceptions.ConflictException"/> on unique-key or FK violation.</summary>
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
