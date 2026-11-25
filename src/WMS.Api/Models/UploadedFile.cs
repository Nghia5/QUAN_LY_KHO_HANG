using System.ComponentModel.DataAnnotations;
using WMS.Api.Models.Documents;

namespace WMS.Api.Models;

/// <summary>
/// Metadata for uploaded files (avatars, documents, attachments) with full model validation.
/// </summary>
public sealed class UploadedFile : IValidatableObject
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required(ErrorMessage = "Original file name is required.")]
    [StringLength(255, MinimumLength = 1, ErrorMessage = "Original file name cannot exceed 255 characters.")]
    public required string OriginalFileName { get; set; }

    [Required(ErrorMessage = "Stored file name is required.")]
    [StringLength(255, MinimumLength = 1, ErrorMessage = "Stored file name cannot exceed 255 characters.")]
    public required string StoredFileName { get; set; }

    [Required(ErrorMessage = "File path is required.")]
    [StringLength(500, MinimumLength = 1, ErrorMessage = "File path cannot exceed 500 characters.")]
    public required string FilePath { get; set; }

    [Required(ErrorMessage = "Content type is required.")]
    [StringLength(100, MinimumLength = 1, ErrorMessage = "Content type cannot exceed 100 characters.")]
    public required string ContentType { get; set; }

    [Range(1, 104857600, ErrorMessage = "File size must be between 1 byte and 100 MB.")]
    public long FileSize { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "UploadedBy must be a positive integer.")]
    public int? UploadedBy { get; set; }
    public User? Uploader { get; set; }
    public WarehouseDocumentType? DocumentType { get; set; }
    public int? DocumentId { get; set; }

    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.IsNullOrWhiteSpace(OriginalFileName))
        {
            yield return new ValidationResult("Original file name cannot be blank.", [nameof(OriginalFileName)]);
        }

        if (string.IsNullOrWhiteSpace(StoredFileName))
        {
            yield return new ValidationResult("Stored file name cannot be blank.", [nameof(StoredFileName)]);
        }

        if (string.IsNullOrWhiteSpace(FilePath))
        {
            yield return new ValidationResult("File path cannot be blank.", [nameof(FilePath)]);
        }
    }
}
