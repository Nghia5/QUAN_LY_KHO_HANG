using Microsoft.EntityFrameworkCore;
using WMS.Api.Data;
using WMS.Api.Models;

namespace WMS.Api.Repositories;

public interface IRoleRepository
{
    Task<List<Role>> ListAsync(CancellationToken cancellationToken);
    Task<Role?> FindAsync(int id, CancellationToken cancellationToken);
    Task<bool> CodeExistsAsync(string code, CancellationToken cancellationToken);
    Task<List<Role>> FindByIdsAsync(IReadOnlyCollection<int> ids, CancellationToken cancellationToken);
    void Add(Role role);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public sealed class RoleRepository(WmsDbContext dbContext) : IRoleRepository
{
    private IQueryable<Role> RolesWithPermissions =>
        dbContext.Roles.Include(r => r.RolePermissions).ThenInclude(rp => rp.Permission);

    public Task<List<Role>> ListAsync(CancellationToken cancellationToken) =>
        RolesWithPermissions.AsNoTracking().OrderBy(r => r.Name).ToListAsync(cancellationToken);

    public Task<Role?> FindAsync(int id, CancellationToken cancellationToken) =>
        RolesWithPermissions.SingleOrDefaultAsync(r => r.Id == id, cancellationToken);

    public Task<bool> CodeExistsAsync(string code, CancellationToken cancellationToken) =>
        dbContext.Roles.AnyAsync(r => r.Code == code, cancellationToken);

    public Task<List<Role>> FindByIdsAsync(IReadOnlyCollection<int> ids, CancellationToken cancellationToken) =>
        dbContext.Roles.AsNoTracking().Where(r => ids.Contains(r.Id)).ToListAsync(cancellationToken);

    public void Add(Role role) => dbContext.Roles.Add(role);

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
