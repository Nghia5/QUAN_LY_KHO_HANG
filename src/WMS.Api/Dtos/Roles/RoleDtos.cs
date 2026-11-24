using System.ComponentModel.DataAnnotations;

namespace WMS.Api.Dtos.Roles;

/// <summary>Summary representation of a role.</summary>
public sealed record RoleSummaryResponse(
    int Id,
    string Code,
    string Name);

/// <summary>Granular permission information.</summary>
public sealed record PermissionResponse(
    int Id,
    string Code,
    string Group,
    string? Description);

/// <summary>Detailed representation of a role including its permissions.</summary>
public sealed record RoleResponse(
    int Id,
    string Code,
    string Name,
    string? Description,
    bool IsActive,
    IReadOnlyList<PermissionResponse> Permissions);

/// <summary>Request payload for creating a new role with full built-in validation.</summary>
public sealed record CreateRoleRequest : IValidatableObject
{
    [Required(ErrorMessage = "Role code is required.")]
    [StringLength(50, MinimumLength = 2, ErrorMessage = "Role code must be between 2 and 50 characters.")]
    [RegularExpression(@"^[A-Z0-9_.-]+$", ErrorMessage = "Role code may only contain uppercase letters, numbers, '.', '_' and '-'.")]
    public required string Code { get; init; }

    [Required(ErrorMessage = "Role name is required.")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Role name must be between 2 and 100 characters.")]
    public required string Name { get; init; }

    [MaxLength(250, ErrorMessage = "Description cannot exceed 250 characters.")]
    public string? Description { get; init; }

    public IReadOnlyList<int> PermissionIds { get; init; } = [];

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.IsNullOrWhiteSpace(Code))
        {
            yield return new ValidationResult("Role code cannot be whitespace.", [nameof(Code)]);
        }

        if (string.IsNullOrWhiteSpace(Name))
        {
            yield return new ValidationResult("Role name cannot be whitespace.", [nameof(Name)]);
        }

        if (PermissionIds.Any(id => id <= 0))
        {
            yield return new ValidationResult("All Permission IDs must be greater than 0.", [nameof(PermissionIds)]);
        }

        if (PermissionIds.Distinct().Count() != PermissionIds.Count)
        {
            yield return new ValidationResult("Permission IDs list cannot contain duplicates.", [nameof(PermissionIds)]);
        }
    }
}

/// <summary>Request payload for updating an existing role with full built-in validation.</summary>
public sealed record UpdateRoleRequest : IValidatableObject
{
    [Required(ErrorMessage = "Role name is required.")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Role name must be between 2 and 100 characters.")]
    public required string Name { get; init; }

    [MaxLength(250, ErrorMessage = "Description cannot exceed 250 characters.")]
    public string? Description { get; init; }

    public bool IsActive { get; init; } = true;

    public IReadOnlyList<int> PermissionIds { get; init; } = [];

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            yield return new ValidationResult("Role name cannot be whitespace.", [nameof(Name)]);
        }

        if (PermissionIds.Any(id => id <= 0))
        {
            yield return new ValidationResult("All Permission IDs must be greater than 0.", [nameof(PermissionIds)]);
        }

        if (PermissionIds.Distinct().Count() != PermissionIds.Count)
        {
            yield return new ValidationResult("Permission IDs list cannot contain duplicates.", [nameof(PermissionIds)]);
        }
    }
}
