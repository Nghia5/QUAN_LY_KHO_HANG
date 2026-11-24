using WMS.Api.Services.Auth;
using WMS.Api.Services.Files;
using WMS.Api.Services.Roles;
using WMS.Api.Services.Users;

namespace WMS.Api.Extensions;

/// <summary>
/// Service collection extensions for registering User & Platform module components into DI.
/// </summary>
public static class UserPlatformModuleExtensions
{
    public static IServiceCollection AddUserPlatformModule(this IServiceCollection services)
    {
        services.AddScoped<IPasswordHasher, PasswordHasher>();
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IRoleService, RoleService>();
        services.AddScoped<IFileStorageService, FileStorageService>();

        return services;
    }
}
