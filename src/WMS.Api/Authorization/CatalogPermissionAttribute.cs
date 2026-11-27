using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using WMS.Api.Common;
using WMS.Api.Data;
using WMS.Api.Exceptions;

namespace WMS.Api.Authorization;

[AttributeUsage(AttributeTargets.Method)]
public sealed class CatalogPermissionAttribute(string permission, bool allowWarehouseGrant = false) : Attribute, IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var services = context.HttpContext.RequestServices;
        var userId = services.GetRequiredService<ICurrentUser>().UserId ?? throw new UnauthorizedException();
        var cancellationToken = context.HttpContext.RequestAborted;
        var db = services.GetRequiredService<WmsDbContext>();
        var hasGlobalGrant = await db.Users.AnyAsync(user => user.Id == userId && user.IsActive &&
            user.UserRoles.Any(assignment => assignment.Role.IsActive &&
                (assignment.Role.Code == "ADMIN" ||
                 assignment.Role.RolePermissions.Any(p => p.Permission.Code == permission))),
            cancellationToken);

        if (!hasGlobalGrant && !(allowWarehouseGrant &&
            await services.GetRequiredService<WarehouseAccess>().HasAnyAsync(permission, cancellationToken)))
        {
            throw new ForbiddenException("The account does not have the required catalog permission.");
        }

        await next();
    }
}
