using Microsoft.EntityFrameworkCore;
using WMS.Api.Data;
using WMS.Api.Models;

namespace WMS.Api.Repositories;

public interface IPermissionRepository
{
    Task<List<Permission>> ListAsync(CancellationToken cancellationToken);
    Task<List<Permission>> FindByIdsAsync(IReadOnlyCollection<int> ids, CancellationToken cancellationToken);
}

public sealed class PermissionRepository(WmsDbContext dbContext) : IPermissionRepository
{
    public Task<List<Permission>> ListAsync(CancellationToken cancellationToken) =>
        dbContext.Permissions.AsNoTracking()
            .OrderBy(p => p.Group).ThenBy(p => p.Code)
            .ToListAsync(cancellationToken);

    public Task<List<Permission>> FindByIdsAsync(IReadOnlyCollection<int> ids, CancellationToken cancellationToken) =>
        dbContext.Permissions.Where(p => ids.Contains(p.Id)).ToListAsync(cancellationToken);
}
