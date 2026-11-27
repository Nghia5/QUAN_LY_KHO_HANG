using System.Security.Claims;

namespace WMS.Api.Common;

public interface ICurrentUser
{
    /// <summary>Id of the authenticated user, or null when it cannot be resolved as an integer.</summary>
    int? UserId { get; }

    /// <summary>True when the user has the given permission claim.</summary>
    bool HasPermission(string permission);

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
