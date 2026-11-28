using WMS.Api.Common;
using WMS.Api.Repositories;
using WMS.Api.Dtos.Auth;
using WMS.Api.Exceptions;

namespace WMS.Api.Services.Auth;

public sealed class AuthService(
    IUserRepository userRepository,
    IPasswordHasher passwordHasher,
    ITokenService tokenService,
    ICurrentUser currentUser) : IAuthService
{
    public async Task<AuthResponse> LoginAsync(LoginRequest request, string? ipAddress, CancellationToken cancellationToken)
    {
        var identifier = request.UsernameOrEmail.Trim();

        var user = await userRepository.FindByIdentifierAsync(identifier, cancellationToken);

        if (user is null || !passwordHasher.VerifyPassword(request.Password, user.PasswordHash))
        {
            throw new UnauthorizedException("Invalid username, email or password.");
        }

        if (!user.IsActive)
        {
            throw new UnauthorizedException("Your account has been deactivated. Please contact an administrator.");
        }

        return await tokenService.CreateTokenResponseAsync(user, ipAddress, cancellationToken);
    }

    public Task<AuthResponse> RefreshTokenAsync(RefreshTokenRequest request, string? ipAddress, CancellationToken cancellationToken) =>
        tokenService.RefreshTokenAsync(request.RefreshToken, ipAddress, cancellationToken);

    public async Task RevokeTokenAsync(RevokeTokenRequest request, string? ipAddress, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedException();

        if (!string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            await tokenService.RevokeTokenAsync(request.RefreshToken, userId, ipAddress, cancellationToken);
        }
        else
        {
            await tokenService.RevokeAllUserTokensAsync(userId, ipAddress, cancellationToken);
        }
    }

    public async Task<CurrentUserProfileResponse> GetCurrentUserProfileAsync(CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId
            ?? throw new UnauthorizedException("You must be authenticated to view your profile.");

        var user = await userRepository.GetWithPermissionsAsync(userId, cancellationToken)
            ?? throw new NotFoundException("User", userId);

        var activeRoles = user.UserRoles
            .Where(ur => ur.Role.IsActive)
            .Select(ur => ur.Role)
            .ToList();

        var roles = activeRoles.Select(r => r.Name).Distinct().ToList();
        var permissions = activeRoles
            .SelectMany(r => r.RolePermissions)
            .Select(rp => rp.Permission.Code)
            .Distinct()
            .ToList();

        return new CurrentUserProfileResponse(
            user.Id,
            user.Username,
            user.Email,
            user.FullName,
            user.PhoneNumber,
            user.AvatarUrl,
            user.IsActive,
            roles,
            permissions);
    }
}
