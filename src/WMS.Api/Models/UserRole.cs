using System.ComponentModel.DataAnnotations;

namespace WMS.Api.Models;

/// <summary>
/// Mapping entity between User and Role with full model validation.
/// </summary>
public sealed class UserRole : IValidatableObject
{
    [Required]
    [Range(1, int.MaxValue, ErrorMessage = "UserId must be a positive integer.")]
    public int UserId { get; set; }
    public User User { get; set; } = null!;

    [Required]
    [Range(1, int.MaxValue, ErrorMessage = "RoleId must be a positive integer.")]
    public int RoleId { get; set; }
    public Role Role { get; set; } = null!;

    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (UserId <= 0)
        {
            yield return new ValidationResult("UserId must be greater than 0.", [nameof(UserId)]);
        }

        if (RoleId <= 0)
        {
            yield return new ValidationResult("RoleId must be greater than 0.", [nameof(RoleId)]);
        }
    }
}
