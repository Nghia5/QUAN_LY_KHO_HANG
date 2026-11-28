using Microsoft.AspNetCore.Authorization;
using WMS.Api.Common;
using WMS.Api.Repositories;

namespace WMS.Api.Authorization;

public sealed class ActiveAdminRequirement : IAuthorizationRequirement
{
    public const string PolicyName = "ActiveAdmin";
}

public sealed class ActiveAdminHandler(
    IHttpContextAccessor httpContextAccessor,
    ICurrentUser currentUser,
    IUserRepository userRepository) : AuthorizationHandler<ActiveAdminRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        ActiveAdminRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated == true &&
            currentUser.UserId is int userId &&
            await userRepository.IsActiveAdminAsync(
                userId, httpContextAccessor.HttpContext?.RequestAborted ?? CancellationToken.None))
        {
            context.Succeed(requirement);
        }
    }
}
