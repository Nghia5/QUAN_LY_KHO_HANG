using WMS.Api.Dtos.Roles;

namespace WMS.Api.Dtos.Auth;

/// <summary>Request payload for authenticating a user.</summary>
public sealed record LoginRequest
{
    public required string UsernameOrEmail { get; init; }

    public required string Password { get; init; }
}

/// <summary>Request payload for refreshing an expired access token using a refresh token.</summary>
public sealed record RefreshTokenRequest
{
    public required string RefreshToken { get; init; }
}

/// <summary>Request payload for revoking a refresh token (logout).</summary>
public sealed record RevokeTokenRequest
{
    public string? RefreshToken { get; init; }
}

/// <summary>Response containing tokens returned upon successful login or refresh.</summary>
public sealed record AuthResponse(
    string AccessToken,
    string RefreshToken,
    DateTime ExpiresAt,
    AuthUserSummary User);

/// <summary>Compact user details included in the authentication response.</summary>
public sealed record AuthUserSummary(
    int Id,
    string Username,
    string Email,
    string FullName,
    string? AvatarUrl,
    IReadOnlyList<string> Roles);

/// <summary>Full user profile response for the currently authenticated user (/api/auth/me).</summary>
public sealed record CurrentUserProfileResponse(
    int Id,
    string Username,
    string Email,
    string FullName,
    string? PhoneNumber,
    string? AvatarUrl,
    bool IsActive,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Permissions);
