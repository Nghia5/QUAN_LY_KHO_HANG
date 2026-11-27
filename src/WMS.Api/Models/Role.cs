using System.ComponentModel.DataAnnotations;

namespace WMS.Api.Models;

/// <summary>
/// System role (e.g. ADMIN, WAREHOUSE_MANAGER, WAREHOUSE_STAFF, VIEWER) with full model validation.
/// </summary>
public sealed class Role : IValidatableObject
{
    [Key]
    public int Id { get; set; }

    [Required(ErrorMessage = "Role code is required.")]
    [StringLength(50, MinimumLength = 2, ErrorMessage = "Role code must be between 2 and 50 characters.")]
    [RegularExpression(@"^[A-Za-z0-9_.-]+$", ErrorMessage = "Role code may only contain uppercase letters, numbers, '.', '_' and '-'.")]
    public required string Code { get; set; }

    [Required(ErrorMessage = "Role name is required.")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Role name must be between 2 and 100 characters.")]
    public required string Name { get; set; }

    [MaxLength(250, ErrorMessage = "Description cannot exceed 250 characters.")]
    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public ICollection<UserRole> UserRoles { get; set; } = [];
    public ICollection<RolePermission> RolePermissions { get; set; } = [];

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
    }
}
