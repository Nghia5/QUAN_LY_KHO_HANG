using WMS.Api.Dtos.Common;
using WMS.Api.Dtos.Roles;

namespace WMS.Api.Dtos.Users;

/// <summary>Server-side query for user list with keyword filtering, role filtering and sorting.</summary>
public sealed record UserQuery : PagedRequest
{
    public static readonly string[] SortableFields = ["id", "username", "fullName", "email", "createdAt", "isActive"];

    public int? RoleId { get; init; }

    public bool? IsActive { get; init; }
}

/// <summary>Request payload for creating a new user.</summary>
public sealed record CreateUserRequest
{
    public required string Username { get; init; }

    public required string Email { get; init; }

    public required string Password { get; init; }

    public required string FullName { get; init; }

    public string? PhoneNumber { get; init; }

    public string? AvatarUrl { get; init; }

    public IReadOnlyList<int> RoleIds { get; init; } = [];
}

/// <summary>Request payload for updating user details.</summary>
public sealed record UpdateUserRequest
{
    public required string FullName { get; init; }

    public required string Email { get; init; }

    public string? PhoneNumber { get; init; }

    public string? AvatarUrl { get; init; }

    public IReadOnlyList<int> RoleIds { get; init; } = [];
}

/// <summary>Request payload for changing user password.</summary>
public sealed record ChangePasswordRequest
{
    public required string CurrentPassword { get; init; }

    public required string NewPassword { get; init; }

    public required string ConfirmPassword { get; init; }
}

/// <summary>Request payload for administrative password reset.</summary>
public sealed record AdminResetPasswordRequest
{
    public required string NewPassword { get; init; }
}

/// <summary>User representation in list queries.</summary>
public sealed record UserResponse(
    int Id,
    string Username,
    string Email,
    string FullName,
    string? PhoneNumber,
    string? AvatarUrl,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    DateTime? DeletedAt,
    IReadOnlyList<string> Roles);

/// <summary>Comprehensive user details for single entity view.</summary>
public sealed record UserDetailResponse(
    int Id,
    string Username,
    string Email,
    string FullName,
    string? PhoneNumber,
    string? AvatarUrl,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    DateTime? DeletedAt,
    int? DeletedBy,
    IReadOnlyList<RoleSummaryResponse> Roles,
    IReadOnlyList<string> Permissions);

/// <summary>Compact user summary for dropdowns or minimal references.</summary>
public sealed record UserSummaryResponse(
    int Id,
    string Username,
    string FullName,
    string Email,
    string? AvatarUrl);
