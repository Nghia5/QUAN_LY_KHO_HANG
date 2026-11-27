using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using WMS.Api.Common;
using WMS.Api.Data;
using WMS.Api.Exceptions;
using WMS.Api.Models;

namespace WMS.Api.Authorization;

public sealed class WarehouseAccess(WmsDbContext dbContext, ICurrentUser currentUser)
{
    private int[]? warehouseIds;

    public async Task LoadAsync(string permission, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedException();
        warehouseIds = await Grants(userId, permission)
            .Select(a => a.WarehouseId).Distinct().ToArrayAsync(cancellationToken);

        if (warehouseIds.Length == 0)
        {
            throw new ForbiddenException("No warehouse assignment grants this permission.");
        }
    }

    public Task<bool> HasAsync(string permission, int warehouseId, CancellationToken cancellationToken) =>
        currentUser.UserId is int userId
            ? Grants(userId, permission).AnyAsync(a => a.WarehouseId == warehouseId, cancellationToken)
            : Task.FromResult(false);

    public Task<bool> HasAnyAsync(string permission, CancellationToken cancellationToken) =>
        currentUser.UserId is int userId ? Grants(userId, permission).AnyAsync(cancellationToken) : Task.FromResult(false);

    private IQueryable<WarehouseRoleAssignment> Grants(int userId, string permission) =>
        dbContext.Set<WarehouseRoleAssignment>()
            .Where(a => a.UserId == userId && a.User.IsActive && a.Role.IsActive
                && a.Role.RolePermissions.Any(p => p.Permission.Code == permission));

    public void Require(int warehouseId)
    {
        if (warehouseIds is null || !warehouseIds.Contains(warehouseId))
        {
            throw new ForbiddenException("The operation is outside the assigned warehouse scope.");
        }
    }

    public void RequireEither(int sourceId, int destinationId)
    {
        if (warehouseIds is null || (!warehouseIds.Contains(sourceId) && !warehouseIds.Contains(destinationId)))
        {
            throw new ForbiddenException("The transfer is outside the assigned warehouse scope.");
        }
    }

    // Repositories also serve background tasks and domain tests without an HTTP scope.
    public int[]? WarehouseIds => warehouseIds;

    public IQueryable<T> Filter<T>(IQueryable<T> query, Expression<Func<T, int>> warehouseId)
    {
        if (warehouseIds is not { } ids)
        {
            return query;
        }

        // Capture the array so EF can parameterize it.
        Expression<Func<int, bool>> allowed = id => ids.Contains(id);
        var contains = ((MethodCallExpression)allowed.Body).Update(null,
            [((MethodCallExpression)allowed.Body).Arguments[0], warehouseId.Body]);
        return query.Where(Expression.Lambda<Func<T, bool>>(contains, warehouseId.Parameters));
    }
}
