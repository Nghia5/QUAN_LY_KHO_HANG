using System.ComponentModel.DataAnnotations;

namespace WMS.Api.Models;

/// <summary>
/// Mapping entity between Role and Permission with full model validation.
/// </summary>
public sealed class RolePermission : IValidatableObject
{
    [Required]
    [Range(1, int.MaxValue, ErrorMessage = "RoleId must be a positive integer.")]
    public int RoleId { get; set; }
    public Role Role { get; set; } = null!;

    [Required]
    [Range(1, int.MaxValue, ErrorMessage = "PermissionId must be a positive integer.")]
    public int PermissionId { get; set; }
    public Permission Permission { get; set; } = null!;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (RoleId <= 0)
        {
            yield return new ValidationResult("RoleId must be greater than 0.", [nameof(RoleId)]);
        }

        if (PermissionId <= 0)
        {
            yield return new ValidationResult("PermissionId must be greater than 0.", [nameof(PermissionId)]);
        }
    }
}
