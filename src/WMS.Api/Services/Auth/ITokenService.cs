using WMS.Api.Dtos.Auth;
using WMS.Api.Models;

namespace WMS.Api.Services.Auth;

public interface ITokenService
{
    Task<AuthResponse> CreateTokenResponseAsync(User user, string? ipAddress, CancellationToken cancellationToken);

    Task<AuthResponse> RefreshTokenAsync(string refreshToken, string? ipAddress, CancellationToken cancellationToken);

    Task RevokeTokenAsync(string refreshToken, string? ipAddress, CancellationToken cancellationToken);

    Task RevokeAllUserTokensAsync(int userId, string? ipAddress, CancellationToken cancellationToken);
}
