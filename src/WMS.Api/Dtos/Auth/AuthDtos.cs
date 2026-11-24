using System.ComponentModel.DataAnnotations;
using WMS.Api.Dtos.Roles;

namespace WMS.Api.Dtos.Auth;

/// <summary>Request payload for authenticating a user with full built-in validation.</summary>
public sealed record LoginRequest : IValidatableObject
{
    [Required(ErrorMessage = "Username or email is required.")]
    [StringLength(100, MinimumLength = 3, ErrorMessage = "Username or email must be between 3 and 100 characters.")]
    public required string UsernameOrEmail { get; init; }

    [Required(ErrorMessage = "Password is required.")]
    [StringLength(100, MinimumLength = 6, ErrorMessage = "Password must be between 6 and 100 characters.")]
    public required string Password { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.IsNullOrWhiteSpace(UsernameOrEmail))
        {
            yield return new ValidationResult("Username or email cannot be empty or whitespace.", [nameof(UsernameOrEmail)]);
        }

        if (string.IsNullOrWhiteSpace(Password))
        {
            yield return new ValidationResult("Password cannot be empty or whitespace.", [nameof(Password)]);
        }
    }
}

/// <summary>Request payload for refreshing an access token with full built-in validation.</summary>
public sealed record RefreshTokenRequest : IValidatableObject
{
    [Required(ErrorMessage = "Refresh token is required.")]
    [StringLength(500, MinimumLength = 10, ErrorMessage = "Refresh token must be between 10 and 500 characters.")]
    public required string RefreshToken { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.IsNullOrWhiteSpace(RefreshToken))
        {
            yield return new ValidationResult("Refresh token cannot be whitespace.", [nameof(RefreshToken)]);
        }
    }
}

/// <summary>Request payload for revoking a refresh token (logout) with built-in validation.</summary>
public sealed record RevokeTokenRequest : IValidatableObject
{
    [StringLength(500, MinimumLength = 10, ErrorMessage = "Refresh token must be between 10 and 500 characters.")]
    public string? RefreshToken { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (RefreshToken is not null && string.IsNullOrWhiteSpace(RefreshToken))
        {
            yield return new ValidationResult("Refresh token cannot be only whitespace if provided.", [nameof(RefreshToken)]);
        }
    }
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
