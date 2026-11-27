using WMS.Api.Dtos.Auth;

namespace WMS.Api.Services.Auth;

public interface IAuthService
{
    Task<AuthResponse> LoginAsync(LoginRequest request, string? ipAddress, CancellationToken cancellationToken);

    Task<AuthResponse> RefreshTokenAsync(RefreshTokenRequest request, string? ipAddress, CancellationToken cancellationToken);

    Task RevokeTokenAsync(RevokeTokenRequest request, string? ipAddress, CancellationToken cancellationToken);

    Task<CurrentUserProfileResponse> GetCurrentUserProfileAsync(CancellationToken cancellationToken);
}
