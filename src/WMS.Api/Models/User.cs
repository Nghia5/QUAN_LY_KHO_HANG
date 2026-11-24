using System.ComponentModel.DataAnnotations;

namespace WMS.Api.Models;

/// <summary>
/// User entity for authentication, authorization, and warehouse operations.
/// </summary>
public sealed class User
{
    public int Id { get; set; }

    [Required]
    [MaxLength(50)]
    public required string Username { get; set; }

    [Required]
    [MaxLength(100)]
    [EmailAddress]
    public required string Email { get; set; }

    [Required]
    [MaxLength(500)]
    public required string PasswordHash { get; set; }

    [Required]
    [MaxLength(150)]
    public required string FullName { get; set; }

    [MaxLength(20)]
    [Phone]
    public string? PhoneNumber { get; set; }

    [MaxLength(500)]
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
}
