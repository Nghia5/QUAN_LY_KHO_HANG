using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using WMS.Api.Constants;
using WMS.Api.Repositories;
using WMS.Api.Dtos.Auth;
using WMS.Api.Exceptions;
using WMS.Api.Models;

namespace WMS.Api.Services.Auth;

public sealed class TokenService(
    IRefreshTokenRepository refreshTokenRepository,
    IUserRepository userRepository,
    IOptionsMonitor<JwtBearerOptions> jwtOptions,
    TimeProvider timeProvider) : ITokenService
{
    private const int RefreshTokenDays = 7;
    private const int AccessTokenMinutes = 60;

    public async Task<AuthResponse> CreateTokenResponseAsync(User user, string? ipAddress, CancellationToken cancellationToken)
    {
        var permissions = await userRepository.GetUserPermissionsAsync(user.Id, cancellationToken);
        var roleNames = await userRepository.GetUserRoleNamesAsync(user.Id, cancellationToken);

        var (accessToken, expiresAt) = GenerateJwtAccessToken(user, permissions);
        var refreshToken = GenerateRefreshTokenString();

        var refreshTokenEntity = new RefreshToken
        {
            UserId = user.Id,
            Token = refreshToken,
            ExpiresAt = timeProvider.GetUtcNow().UtcDateTime.AddDays(RefreshTokenDays),
            CreatedAt = timeProvider.GetUtcNow().UtcDateTime,
            CreatedByIp = ipAddress
        };

        refreshTokenRepository.Add(refreshTokenEntity);
        await refreshTokenRepository.SaveChangesAsync(cancellationToken);

        var summary = new AuthUserSummary(
            user.Id,
            user.Username,
            user.Email,
            user.FullName,
            user.AvatarUrl,
            roleNames);

        return new AuthResponse(accessToken, refreshToken, expiresAt, summary);
    }

    public async Task<AuthResponse> RefreshTokenAsync(string refreshToken, string? ipAddress, CancellationToken cancellationToken)
    {
        var existingToken = await refreshTokenRepository.FindByTokenAsync(refreshToken, cancellationToken)
            ?? throw new UnauthorizedException("Invalid refresh token.");

        if (existingToken.IsRevoked)
        {
            // Compromise detected: a revoked token is being reused! Invalidate all tokens for this user.
            await RevokeAllUserTokensAsync(existingToken.UserId, ipAddress, cancellationToken);
            throw new UnauthorizedException("Refresh token was previously revoked. All active sessions have been invalidated for security.");
        }

        if (existingToken.IsExpired)
        {
            throw new UnauthorizedException("Refresh token has expired. Please log in again.");
        }

        if (!existingToken.User.IsActive)
        {
            throw new UnauthorizedException("User account has been deactivated.");
        }

        var newRefreshTokenString = GenerateRefreshTokenString();
        var now = timeProvider.GetUtcNow().UtcDateTime;

        existingToken.Revoke(ipAddress, newRefreshTokenString);

        var newRefreshToken = new RefreshToken
        {
            UserId = existingToken.UserId,
            Token = newRefreshTokenString,
            ExpiresAt = now.AddDays(RefreshTokenDays),
            CreatedAt = now,
            CreatedByIp = ipAddress
        };

        refreshTokenRepository.Add(newRefreshToken);

        var permissions = await userRepository.GetUserPermissionsAsync(existingToken.UserId, cancellationToken);
        var roleNames = await userRepository.GetUserRoleNamesAsync(existingToken.UserId, cancellationToken);
        var (accessToken, expiresAt) = GenerateJwtAccessToken(existingToken.User, permissions);

        await refreshTokenRepository.SaveChangesAsync(cancellationToken);

        var summary = new AuthUserSummary(
            existingToken.User.Id,
            existingToken.User.Username,
            existingToken.User.Email,
            existingToken.User.FullName,
            existingToken.User.AvatarUrl,
            roleNames);

        return new AuthResponse(accessToken, newRefreshTokenString, expiresAt, summary);
    }

    public async Task RevokeTokenAsync(string refreshToken, int userId, string? ipAddress, CancellationToken cancellationToken)
    {
        var token = await refreshTokenRepository.FindByTokenAsync(refreshToken, cancellationToken);

        if (token is not null && token.UserId == userId && token.IsActive)
        {
            token.Revoke(ipAddress);
            await refreshTokenRepository.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task RevokeAllUserTokensAsync(int userId, string? ipAddress, CancellationToken cancellationToken)
    {
        await refreshTokenRepository.StageRevocationsByUserIdAsync(userId, ipAddress, cancellationToken);
        await refreshTokenRepository.SaveChangesAsync(cancellationToken);
    }

    private (string Token, DateTime ExpiresAt) GenerateJwtAccessToken(User user, IReadOnlyList<string> permissions)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var expiresAt = now.AddMinutes(AccessTokenMinutes);

        var (signingCredentials, issuer, audience) = ResolveSigningParameters();

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(JwtRegisteredClaimNames.Name, user.FullName),
            new(JwtRegisteredClaimNames.UniqueName, user.Username),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N"))
        };

        foreach (var permission in permissions)
        {
            claims.Add(new Claim(Permissions.ClaimType, permission));
        }

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Issuer = issuer,
            Audience = audience,
            Subject = new ClaimsIdentity(claims),
            NotBefore = now,
            IssuedAt = now,
            Expires = expiresAt,
            SigningCredentials = signingCredentials
        };

        var handler = new JsonWebTokenHandler();
        var token = handler.CreateToken(tokenDescriptor);

        return (token, expiresAt);
    }

    private (SigningCredentials Credentials, string? Issuer, string? Audience) ResolveSigningParameters()
    {
        var parameters = jwtOptions.Get(JwtBearerDefaults.AuthenticationScheme).TokenValidationParameters;
        var key = parameters.IssuerSigningKey ?? parameters.IssuerSigningKeys?.FirstOrDefault();
        var issuer = parameters.ValidIssuer ?? parameters.ValidIssuers?.FirstOrDefault();
        var audience = parameters.ValidAudience ?? parameters.ValidAudiences?.FirstOrDefault();

        if (key is not SymmetricSecurityKey || key.KeySize < 256 ||
            string.IsNullOrWhiteSpace(issuer) || string.IsNullOrWhiteSpace(audience))
        {
            throw new InvalidOperationException("JWT signing requires a symmetric key of at least 256 bits, an issuer and an audience.");
        }

        return (new SigningCredentials(key, SecurityAlgorithms.HmacSha256), issuer, audience);
    }

    private static string GenerateRefreshTokenString() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));

}
