using System.ComponentModel.DataAnnotations;

namespace WMS.Api.Models;

/// <summary>
/// User entity for authentication, authorization, and warehouse operations with full model validation.
/// </summary>
public sealed class User : IValidatableObject
{
    [Key]
    public int Id { get; set; }

    [Required(ErrorMessage = "Username is required.")]
    [StringLength(50, MinimumLength = 3, ErrorMessage = "Username must be between 3 and 50 characters.")]
    [RegularExpression(@"^[A-Za-z0-9][A-Za-z0-9._-]*$",
        ErrorMessage = "Username must start with an alphanumeric character and contain only letters, numbers, '.', '_' and '-'.")]
    public required string Username { get; set; }

    [Required(ErrorMessage = "Email is required.")]
    [StringLength(100, ErrorMessage = "Email cannot exceed 100 characters.")]
    [EmailAddress(ErrorMessage = "Email is not in a valid format.")]
    public required string Email { get; set; }

    [Required(ErrorMessage = "Password hash is required.")]
    [MaxLength(500, ErrorMessage = "Password hash cannot exceed 500 characters.")]
    public required string PasswordHash { get; set; }

    [Required(ErrorMessage = "Full name is required.")]
    [StringLength(150, MinimumLength = 2, ErrorMessage = "Full name must be between 2 and 150 characters.")]
    public required string FullName { get; set; }

    [MaxLength(20, ErrorMessage = "Phone number cannot exceed 20 characters.")]
    [RegularExpression(@"^\+?[0-9]{9,15}$", ErrorMessage = "Phone number must contain between 9 and 15 digits with an optional leading '+'.")]
    public string? PhoneNumber { get; set; }

    [MaxLength(500, ErrorMessage = "Avatar URL cannot exceed 500 characters.")]
    public string? AvatarUrl { get; set; }

    /// <summary>
    /// Soft delete flag. Inactive users cannot log in or perform actions.
    /// Preserves FK references across documents and stock movements.
    /// </summary>
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    /// <summary>Timestamp when the user was soft-deleted.</summary>
    public DateTime? DeletedAt { get; set; }

    /// <summary>ID of the admin who soft-deleted this user.</summary>
    public int? DeletedBy { get; set; }

    // Navigation properties
    public ICollection<UserRole> UserRoles { get; set; } = [];
    public ICollection<WarehouseRoleAssignment> WarehouseRoleAssignments { get; set; } = [];
    public ICollection<RefreshToken> RefreshTokens { get; set; } = [];
    public ICollection<UploadedFile> UploadedFiles { get; set; } = [];

    // Domain methods for safe state transitions
    public void SoftDelete(int deletedByUserId)
    {
        IsActive = false;
        DeletedAt = DateTime.UtcNow;
        DeletedBy = deletedByUserId;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Restore()
    {
        IsActive = true;
        DeletedAt = null;
        DeletedBy = null;
        UpdatedAt = DateTime.UtcNow;
    }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (DeletedAt.HasValue && IsActive)
        {
            yield return new ValidationResult(
                "A soft-deleted user cannot have IsActive set to true.",
                [nameof(IsActive), nameof(DeletedAt)]);
        }

        if (DeletedAt.HasValue && (!DeletedBy.HasValue || DeletedBy.Value <= 0))
        {
            yield return new ValidationResult(
                "A soft-deleted user must record a valid DeletedBy user ID.",
                [nameof(DeletedBy)]);
        }
    }
}
