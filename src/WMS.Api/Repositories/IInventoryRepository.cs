using WMS.Api.Dtos.Common;
using WMS.Api.Dtos.Inventories;

namespace WMS.Api.Repositories;

public interface IInventoryRepository
{
    /// <summary>
    /// Server-side filtered, sorted and paged inventory list. Executes one COUNT query and one
    /// OFFSET/FETCH query projected to <see cref="InventoryResponse"/>; never loads the whole table.
    /// </summary>
    Task<PagedResult<InventoryResponse>> SearchAsync(InventoryQuery query, CancellationToken cancellationToken);

    Task<InventoryResponse?> GetByIdAsync(int id, CancellationToken cancellationToken);
}
