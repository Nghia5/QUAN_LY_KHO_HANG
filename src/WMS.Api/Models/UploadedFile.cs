using System.ComponentModel.DataAnnotations;

namespace WMS.Api.Models;

/// <summary>
/// Metadata for uploaded files (avatars, documents, attachments).
/// </summary>
public sealed class UploadedFile
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(255)]
    public required string OriginalFileName { get; set; }

    [Required]
    [MaxLength(255)]
    public required string StoredFileName { get; set; }

    [Required]
    [MaxLength(500)]
    public required string FilePath { get; set; }

    [Required]
    [MaxLength(100)]
    public required string ContentType { get; set; }

    public long FileSize { get; set; }

    public int? UploadedBy { get; set; }
    public User? Uploader { get; set; }

    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
}
