using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using WMS.Api.Constants;
using WMS.Api.Data;
using WMS.Api.Dtos.Auth;
using WMS.Api.Exceptions;
using WMS.Api.Models;

namespace WMS.Api.Services.Auth;

public sealed class TokenService(
    WmsDbContext dbContext,
    IConfiguration configuration,
    IOptionsMonitor<JwtBearerOptions> jwtOptions,
    TimeProvider timeProvider) : ITokenService
{
    private const int RefreshTokenDays = 7;
    private const int AccessTokenMinutes = 60;

    public async Task<AuthResponse> CreateTokenResponseAsync(User user, string? ipAddress, CancellationToken cancellationToken)
    {
        var permissions = await GetUserPermissionsAsync(user.Id, cancellationToken);
        var roleNames = await GetUserRoleNamesAsync(user.Id, cancellationToken);

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

        dbContext.RefreshTokens.Add(refreshTokenEntity);
        await dbContext.SaveChangesAsync(cancellationToken);

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
        var existingToken = await dbContext.RefreshTokens
            .Include(t => t.User)
            .SingleOrDefaultAsync(t => t.Token == refreshToken, cancellationToken)
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

        dbContext.RefreshTokens.Add(newRefreshToken);

        var permissions = await GetUserPermissionsAsync(existingToken.UserId, cancellationToken);
        var roleNames = await GetUserRoleNamesAsync(existingToken.UserId, cancellationToken);
        var (accessToken, expiresAt) = GenerateJwtAccessToken(existingToken.User, permissions);

        await dbContext.SaveChangesAsync(cancellationToken);

        var summary = new AuthUserSummary(
            existingToken.User.Id,
            existingToken.User.Username,
            existingToken.User.Email,
            existingToken.User.FullName,
            existingToken.User.AvatarUrl,
            roleNames);

        return new AuthResponse(accessToken, newRefreshTokenString, expiresAt, summary);
    }

    public async Task RevokeTokenAsync(string refreshToken, string? ipAddress, CancellationToken cancellationToken)
    {
        var token = await dbContext.RefreshTokens
            .SingleOrDefaultAsync(t => t.Token == refreshToken, cancellationToken);

        if (token is not null && token.IsActive)
        {
            token.Revoke(ipAddress);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task RevokeAllUserTokensAsync(int userId, string? ipAddress, CancellationToken cancellationToken)
    {
        var activeTokens = await dbContext.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null)
            .ToListAsync(cancellationToken);

        foreach (var token in activeTokens)
        {
            token.Revoke(ipAddress);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
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
        // 1. Try explicit configuration key
        var configuredKey = configuration["Jwt:Key"];
        var issuer = configuration["Jwt:Issuer"];
        var audience = configuration["Jwt:Audience"];

        if (!string.IsNullOrWhiteSpace(configuredKey))
        {
            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configuredKey));
            return (new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256), issuer, audience);
        }

        // 2. Try JwtBearerOptions (e.g. configured via dotnet user-jwts)
        var parameters = jwtOptions.Get(JwtBearerDefaults.AuthenticationScheme).TokenValidationParameters;
        var key = parameters.IssuerSigningKeys?.FirstOrDefault() ?? parameters.IssuerSigningKey;

        if (key is not null)
        {
            return (new SigningCredentials(key, SecurityAlgorithms.HmacSha256),
                parameters.ValidIssuer ?? issuer,
                parameters.ValidAudiences?.FirstOrDefault() ?? parameters.ValidAudience ?? audience);
        }

        // 3. Fallback dev key so the system always runs safely even before secrets are set
        const string FallbackSecret = "WMS_Secret_Key_For_Jwt_Token_Generation_Default_2026_Must_Be_Long_Enough";
        var fallbackKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(FallbackSecret));
        return (new SigningCredentials(fallbackKey, SecurityAlgorithms.HmacSha256), "WMS.Api", "WMS.Client");
    }

    private static string GenerateRefreshTokenString() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));

    private async Task<IReadOnlyList<string>> GetUserPermissionsAsync(int userId, CancellationToken cancellationToken) =>
        await dbContext.UserRoles
            .Where(ur => ur.UserId == userId && ur.Role.IsActive)
            .SelectMany(ur => ur.Role.RolePermissions)
            .Select(rp => rp.Permission.Code)
            .Distinct()
            .ToListAsync(cancellationToken);

    private async Task<IReadOnlyList<string>> GetUserRoleNamesAsync(int userId, CancellationToken cancellationToken) =>
        await dbContext.UserRoles
            .Where(ur => ur.UserId == userId && ur.Role.IsActive)
            .Select(ur => ur.Role.Name)
            .Distinct()
            .ToListAsync(cancellationToken);
}
