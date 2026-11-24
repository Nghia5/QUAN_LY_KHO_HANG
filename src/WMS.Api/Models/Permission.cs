using System.ComponentModel.DataAnnotations;

namespace WMS.Api.Models;

/// <summary>
/// Granular permission code with full model validation.
/// </summary>
public sealed class Permission : IValidatableObject
{
    [Key]
    public int Id { get; set; }

    [Required(ErrorMessage = "Permission code is required.")]
    [StringLength(100, MinimumLength = 3, ErrorMessage = "Permission code must be between 3 and 100 characters.")]
    [RegularExpression(@"^[A-Z0-9_]+$", ErrorMessage = "Permission code must contain only uppercase letters, numbers and underscores.")]
    public required string Code { get; set; }

    [Required(ErrorMessage = "Permission group is required.")]
    [StringLength(50, MinimumLength = 2, ErrorMessage = "Permission group must be between 2 and 50 characters.")]
    public required string Group { get; set; }

    [MaxLength(250, ErrorMessage = "Description cannot exceed 250 characters.")]
    public string? Description { get; set; }

    // Navigation properties
    public ICollection<RolePermission> RolePermissions { get; set; } = [];

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.IsNullOrWhiteSpace(Code))
        {
            yield return new ValidationResult("Permission code cannot be whitespace.", [nameof(Code)]);
        }

        if (string.IsNullOrWhiteSpace(Group))
        {
            yield return new ValidationResult("Permission group cannot be whitespace.", [nameof(Group)]);
        }
    }
}
