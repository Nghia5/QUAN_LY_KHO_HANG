using System.Security.Claims;
using Microsoft.Extensions.DependencyInjection;

namespace WMS.Api.Common;

public interface ICurrentUser
{
    /// <summary>Id of the authenticated user, or null when it cannot be resolved as an integer.</summary>
    int? UserId { get; }

    /// <summary>True when the user has the given permission claim.</summary>
    bool HasPermission(string permission);

    /// <summary>True when the user has the system ADMIN role.</summary>
    bool IsAdmin => false;

    /// <summary>True when the user has the given role.</summary>
    bool IsInRole(string role) => false;

    Task<bool> HasWarehousePermissionAsync(string permission, int warehouseId, CancellationToken cancellationToken) =>
        Task.FromResult(HasPermission(permission));
}

public sealed class HttpContextCurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    public Task<bool> HasWarehousePermissionAsync(string permission, int warehouseId, CancellationToken cancellationToken) =>
        httpContextAccessor.HttpContext?.RequestServices
            .GetRequiredService<Authorization.WarehouseAccess>().HasAsync(permission, warehouseId, cancellationToken)
        ?? Task.FromResult(false);

    public bool HasPermission(string permission) =>
        httpContextAccessor.HttpContext?.User.HasClaim(Constants.Permissions.ClaimType, permission) == true;

    public bool IsInRole(string role) =>
        httpContextAccessor.HttpContext?.User.IsInRole(role) == true
        || httpContextAccessor.HttpContext?.User.HasClaim(ClaimTypes.Role, role) == true;

    public bool IsAdmin
    {
        get
        {
            if (IsInRole("ADMIN")) return true;
            if (UserId is int uid)
            {
                var db = httpContextAccessor.HttpContext?.RequestServices.GetService<Data.WmsDbContext>();
                if (db is not null)
                {
                    return db.Users.Any(u => u.Id == uid && u.IsActive && u.UserRoles.Any(r => r.Role.IsActive && r.Role.Code == "ADMIN"));
                }
            }
            return false;
        }
    }

    public int? UserId
    {
        get
        {
            var user = httpContextAccessor.HttpContext?.User;
            var value = user?.FindFirstValue(ClaimTypes.NameIdentifier) ?? user?.FindFirstValue("sub");
            return int.TryParse(value, out var id) ? id : null;
        }
    }
}
