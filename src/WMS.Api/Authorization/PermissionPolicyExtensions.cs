using Microsoft.AspNetCore.Authorization;
using WMS.Api.Constants;

namespace WMS.Api.Authorization;

public static class PermissionPolicyExtensions
{
    /// <summary>Registers one policy per permission code (policy name == permission code).</summary>
    public static AuthorizationBuilder AddPermissionPolicies(this AuthorizationBuilder builder)
    {
        foreach (var permission in Permissions.All)
        {
            builder.AddPolicy(permission, policy => policy
                .RequireAuthenticatedUser()
                .RequireClaim(Permissions.ClaimType, permission));
        }

        return builder;
    }
}
