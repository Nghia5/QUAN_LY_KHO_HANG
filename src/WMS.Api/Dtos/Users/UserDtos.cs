using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using WMS.Api.Dtos.Common;
using WMS.Api.Dtos.Roles;

namespace WMS.Api.Dtos.Users;

/// <summary>Server-side query for user list with keyword filtering, role filtering, sorting and self-validation.</summary>
public sealed record UserQuery : PagedRequest, IValidatableObject
{
    public static readonly string[] SortableFields = ["id", "username", "fullName", "email", "createdAt", "isActive"];

    public int? RoleId { get; init; }
    public int? WarehouseId { get; init; }

    public bool? IsActive { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Page < 1)
        {
            yield return new ValidationResult("Page must be greater than or equal to 1.", [nameof(Page)]);
        }
        else if (Offset > int.MaxValue)
        {
            yield return new ValidationResult("The requested page offset is too large.", [nameof(Page)]);
        }

        if (PageSize is < 1 or > MaxPageSize)
        {
            yield return new ValidationResult("PageSize must be between 1 and 100.", [nameof(PageSize)]);
        }

        if (Keyword?.Length > 200)
        {
            yield return new ValidationResult("Keyword cannot exceed 200 characters.", [nameof(Keyword)]);
        }

        if (!string.IsNullOrEmpty(SortDir) &&
            !SortDir.Equals("asc", StringComparison.OrdinalIgnoreCase) &&
            !SortDir.Equals("desc", StringComparison.OrdinalIgnoreCase))
        {
            yield return new ValidationResult("SortDir must be 'asc' or 'desc'.", [nameof(SortDir)]);
        }

        if (SortBy is not null && !SortableFields.Contains(SortBy, StringComparer.OrdinalIgnoreCase))
        {
            yield return new ValidationResult(
                $"SortBy must be one of: {string.Join(", ", SortableFields)}.",
                [nameof(SortBy)]);
        }

        if (RoleId.HasValue && RoleId.Value <= 0)
        {
            yield return new ValidationResult(
                "RoleId must be greater than 0.",
                [nameof(RoleId)]);
        }

        if (WarehouseId is <= 0)
        {
            yield return new ValidationResult("WarehouseId must be greater than 0.", [nameof(WarehouseId)]);
        }
    }
}

/// <summary>Request payload for creating a new user with full property-level validation.</summary>
public sealed record CreateUserRequest : IValidatableObject
{
    [Required(ErrorMessage = "Username is required.")]
    [StringLength(50, MinimumLength = 3, ErrorMessage = "Username must be between 3 and 50 characters.")]
    [RegularExpression(@"^[A-Za-z0-9][A-Za-z0-9._-]*$",
        ErrorMessage = "Username must start with a letter/digit and contain only letters, digits, '.', '_' and '-'.")]
    public required string Username { get; init; }

    [Required(ErrorMessage = "Email is required.")]
    [MaxLength(100, ErrorMessage = "Email cannot exceed 100 characters.")]
    [EmailAddress(ErrorMessage = "Email is not in a valid format.")]
    public required string Email { get; init; }

    [Required(ErrorMessage = "Password is required.")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "Password must be at least 8 characters long.")]
    [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[\W_]).+$",
        ErrorMessage = "Password must contain at least one uppercase letter, one lowercase letter, one digit, and one special character.")]
    public required string Password { get; init; }

    [Required(ErrorMessage = "Full name is required.")]
    [StringLength(150, MinimumLength = 2, ErrorMessage = "Full name must be between 2 and 150 characters.")]
    public required string FullName { get; init; }

    [RegularExpression(@"^\+?[0-9]{9,15}$", ErrorMessage = "Phone number must contain between 9 and 15 digits with an optional leading '+'.")]
    public string? PhoneNumber { get; init; }

    [MaxLength(500, ErrorMessage = "Avatar URL cannot exceed 500 characters.")]
    public string? AvatarUrl { get; init; }

    [Required(ErrorMessage = "RoleIds is required.")]
    public IReadOnlyList<int> RoleIds { get; init; } = [];

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.IsNullOrWhiteSpace(Username))
        {
            yield return new ValidationResult("Username cannot be whitespace.", [nameof(Username)]);
        }

        if (string.IsNullOrWhiteSpace(FullName))
        {
            yield return new ValidationResult("Full name cannot be whitespace.", [nameof(FullName)]);
        }

        if (RoleIds.Any(id => id <= 0))
        {
            yield return new ValidationResult("All Role IDs must be greater than 0.", [nameof(RoleIds)]);
        }

        if (RoleIds.Distinct().Count() != RoleIds.Count)
        {
            yield return new ValidationResult("Role IDs list cannot contain duplicates.", [nameof(RoleIds)]);
        }
    }
}

/// <summary>Request payload for updating user details with full property-level validation.</summary>
public sealed record UpdateUserRequest : IValidatableObject
{
    [Required(ErrorMessage = "Full name is required.")]
    [StringLength(150, MinimumLength = 2, ErrorMessage = "Full name must be between 2 and 150 characters.")]
    public required string FullName { get; init; }

    [Required(ErrorMessage = "Email is required.")]
    [MaxLength(100, ErrorMessage = "Email cannot exceed 100 characters.")]
    [EmailAddress(ErrorMessage = "Email is not in a valid format.")]
    public required string Email { get; init; }

    [RegularExpression(@"^\+?[0-9]{9,15}$", ErrorMessage = "Phone number must contain between 9 and 15 digits with an optional leading '+'.")]
    public string? PhoneNumber { get; init; }

    [MaxLength(500, ErrorMessage = "Avatar URL cannot exceed 500 characters.")]
    public string? AvatarUrl { get; init; }

    [Required(ErrorMessage = "RoleIds is required.")]
    public IReadOnlyList<int> RoleIds { get; init; } = [];

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.IsNullOrWhiteSpace(FullName))
        {
            yield return new ValidationResult("Full name cannot be whitespace.", [nameof(FullName)]);
        }

        if (RoleIds.Any(id => id <= 0))
        {
            yield return new ValidationResult("All Role IDs must be greater than 0.", [nameof(RoleIds)]);
        }

        if (RoleIds.Distinct().Count() != RoleIds.Count)
        {
            yield return new ValidationResult("Role IDs list cannot contain duplicates.", [nameof(RoleIds)]);
        }
    }
}

/// <summary>Request payload for changing user password with cross-field validation.</summary>
public sealed record ChangePasswordRequest : IValidatableObject
{
    [Required(ErrorMessage = "Current password is required.")]
    public required string CurrentPassword { get; init; }

    [Required(ErrorMessage = "New password is required.")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "New password must be at least 8 characters long.")]
    [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[\W_]).+$",
        ErrorMessage = "New password must contain at least one uppercase letter, one lowercase letter, one digit, and one special character.")]
    public required string NewPassword { get; init; }

    [Required(ErrorMessage = "Please confirm your new password.")]
    [Compare(nameof(NewPassword), ErrorMessage = "Confirm password does not match new password.")]
    public required string ConfirmPassword { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.Equals(CurrentPassword, NewPassword, StringComparison.Ordinal))
        {
            yield return new ValidationResult(
                "New password cannot be the same as current password.",
                [nameof(NewPassword)]);
        }
    }
}

/// <summary>Request payload for administrative password reset.</summary>
public sealed record AdminResetPasswordRequest
{
    [Required(ErrorMessage = "New password is required.")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "New password must be at least 8 characters long.")]
    [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[\W_]).+$",
        ErrorMessage = "New password must contain at least one uppercase letter, one lowercase letter, one digit, and one special character.")]
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
    IReadOnlyList<string> Roles)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<UserWarehouseRoleResponse>? WarehouseRoles { get; init; }
}

public sealed record UserWarehouseRoleResponse(int WarehouseId, string WarehouseCode, int RoleId, string RoleCode, string RoleName);

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
    IReadOnlyList<string> Permissions)
{
    public IReadOnlyList<UserWarehouseRoleResponse> WarehouseRoles { get; init; } = [];
}

/// <summary>Compact user summary for dropdowns or minimal references.</summary>
public sealed record UserSummaryResponse(
    int Id,
    string Username,
    string FullName,
    string Email,
    string? AvatarUrl);
