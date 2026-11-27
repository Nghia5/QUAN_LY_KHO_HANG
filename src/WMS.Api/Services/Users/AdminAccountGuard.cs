using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using WMS.Api.Data;
using WMS.Api.Exceptions;
using WMS.Api.Models;

namespace WMS.Api.Services.Users;

public sealed class AdminAccountGuard(WmsDbContext db)
{
    public Task<IDbContextTransaction> BeginAsync(CancellationToken cancellationToken) =>
        DatabaseWriteLock.BeginAsync(db, "wms:active-admin", cancellationToken);

    public async Task EnsureCanRemoveAsync(User user, IReadOnlySet<int>? retainedRoles, CancellationToken cancellationToken)
    {
        if (user.IsActive &&
            user.UserRoles.Any(a => a.Role.IsActive && IsAdminRole(a.Role) &&
                (retainedRoles is null || !retainedRoles.Contains(a.RoleId))) &&
            !await db.Users.AnyAsync(other => other.Id != user.Id && other.IsActive &&
                other.UserRoles.Any(a => a.Role.IsActive && a.Role.Code == "ADMIN"), cancellationToken))
        {
            throw new ConflictException("At least one active administrator must remain.");
        }
    }

    public static void EnsureCanDeactivate(Role role, bool isActive)
    {
        if (IsAdminRole(role) && !isActive)
        {
            throw new ConflictException("The ADMIN role must remain active.");
        }
    }

    private static bool IsAdminRole(Role role) =>
        string.Equals(role.Code, "ADMIN", StringComparison.OrdinalIgnoreCase);
}
