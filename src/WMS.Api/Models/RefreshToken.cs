using System.ComponentModel.DataAnnotations;

namespace WMS.Api.Models;

/// <summary>
/// Refresh token entity for JWT authentication renewal and rotation with full model validation.
/// </summary>
public sealed class RefreshToken : IValidatableObject
{
    [Key]
    public int Id { get; set; }

    [Required]
    [Range(1, int.MaxValue, ErrorMessage = "UserId must be a positive integer.")]
    public int UserId { get; set; }
    public User User { get; set; } = null!;

    [Required(ErrorMessage = "Token value is required.")]
    [StringLength(256, MinimumLength = 10, ErrorMessage = "Token must be between 10 and 256 characters.")]
    public required string Token { get; set; }

    [Required]
    public DateTime ExpiresAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [MaxLength(50, ErrorMessage = "IP address cannot exceed 50 characters.")]
    public string? CreatedByIp { get; set; }

    public DateTime? RevokedAt { get; set; }

    [MaxLength(50, ErrorMessage = "IP address cannot exceed 50 characters.")]
    public string? RevokedByIp { get; set; }

    [MaxLength(256, ErrorMessage = "ReplacedByToken cannot exceed 256 characters.")]
    public string? ReplacedByToken { get; set; }

    public bool IsExpired => DateTime.UtcNow >= ExpiresAt;

    public bool IsRevoked => RevokedAt != null;

    public bool IsActive => !IsRevoked && !IsExpired;

    public void Revoke(string? ipAddress = null, string? replacedBy = null)
    {
        var now = DateTime.UtcNow;
        RevokedAt = now < CreatedAt ? CreatedAt : now;
        RevokedByIp = ipAddress;
        ReplacedByToken = replacedBy;
    }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (ExpiresAt <= CreatedAt)
        {
            yield return new ValidationResult(
                "ExpiresAt must be later than CreatedAt.",
                [nameof(ExpiresAt)]);
        }

        if (RevokedAt.HasValue && RevokedAt.Value < CreatedAt)
        {
            yield return new ValidationResult(
                "RevokedAt cannot be earlier than CreatedAt.",
                [nameof(RevokedAt)]);
        }
    }
}
