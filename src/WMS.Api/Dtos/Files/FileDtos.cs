using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using WMS.Api.Models;
using WMS.Api.Models.Documents;

namespace WMS.Api.Dtos.Files;

/// <summary>Request payload for uploading an avatar image with built-in validation.</summary>
public sealed record UploadAvatarRequest : IValidatableObject
{
    private static readonly string[] AllowedExtensions = [".jpg", ".jpeg", ".png", ".webp"];
    private static readonly string[] AllowedMimeTypes = ["image/jpeg", "image/png", "image/webp"];
    private const long MaxFileSizeInBytes = 5 * 1024 * 1024; // 5 MB

    [Required(ErrorMessage = "Avatar image file is required.")]
    public required IFormFile File { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (File is null)
        {
            yield break;
        }

        foreach (var error in UploadFileMetadataValidation.Validate(File))
        {
            yield return error;
        }

        if (File.Length == 0)
        {
            yield return new ValidationResult("The uploaded file is empty.", [nameof(File)]);
            yield break;
        }

        if (File.Length > MaxFileSizeInBytes)
        {
            yield return new ValidationResult("Avatar image cannot exceed 5 MB.", [nameof(File)]);
        }

        var extension = Path.GetExtension(File.FileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(extension))
        {
            yield return new ValidationResult(
                $"Only image formats ({string.Join(", ", AllowedExtensions)}) are allowed for avatars.",
                [nameof(File)]);
        }

        if (!AllowedMimeTypes.Contains(File.ContentType, StringComparer.OrdinalIgnoreCase))
        {
            yield return new ValidationResult("File MIME type is not a recognized valid image format.", [nameof(File)]);
        }
    }
}

/// <summary>Request payload for uploading general documents/attachments with built-in validation.</summary>
public sealed record UploadDocumentRequest : IValidatableObject
{
    private static readonly string[] AllowedExtensions = [".pdf", ".xlsx", ".xls", ".docx", ".doc", ".png", ".jpg", ".jpeg"];
    private const long MaxFileSizeInBytes = 20 * 1024 * 1024; // 20 MB

    [Required(ErrorMessage = "Document file is required.")]
    public required IFormFile File { get; init; }

    [Required]
    [EnumDataType(typeof(WarehouseDocumentType))]
    public WarehouseDocumentType? DocumentType { get; init; }

    [Required]
    [Range(1, int.MaxValue)]
    public int? DocumentId { get; init; }

    [MaxLength(UploadedFile.MaxDescriptionLength, ErrorMessage = "Description cannot exceed 255 characters.")]
    public string? Description { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (File is null)
        {
            yield break;
        }

        foreach (var error in UploadFileMetadataValidation.Validate(File))
        {
            yield return error;
        }

        if (File.Length == 0)
        {
            yield return new ValidationResult("The uploaded file is empty.", [nameof(File)]);
            yield break;
        }

        if (File.Length > MaxFileSizeInBytes)
        {
            yield return new ValidationResult("Document cannot exceed 20 MB.", [nameof(File)]);
        }

        var extension = Path.GetExtension(File.FileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(extension))
        {
            yield return new ValidationResult(
                $"File extension '{extension}' is not allowed. Supported formats: {string.Join(", ", AllowedExtensions)}.",
                [nameof(File)]);
        }
    }
}

internal static class UploadFileMetadataValidation
{
    public static IEnumerable<ValidationResult> Validate(IFormFile file)
    {
        var name = Path.GetFileName(file.FileName);
        if (string.IsNullOrWhiteSpace(name) || name.Length > UploadedFile.MaxOriginalFileNameLength)
        {
            yield return new ValidationResult(
                $"The file name must have 1 to {UploadedFile.MaxOriginalFileNameLength} characters.",
                [nameof(UploadDocumentRequest.File)]);
        }

        var contentType = file.ContentType;
        if (string.IsNullOrWhiteSpace(contentType) || contentType.Length > UploadedFile.MaxContentTypeLength)
        {
            yield return new ValidationResult(
                $"The file content type must have 1 to {UploadedFile.MaxContentTypeLength} characters.",
                [nameof(UploadDocumentRequest.File)]);
        }
    }
}

/// <summary>Response returned when a file is successfully uploaded.</summary>
public sealed record UploadFileResponse(
    Guid Id,
    string OriginalFileName,
    string StoredFileName,
    string FileUrl,
    string ContentType,
    long FileSize,
    DateTime UploadedAt)
{
    public string? Description { get; init; }
}

/// <summary>Internal transport object for file download streaming.</summary>
public sealed record FileDownloadInfo(
    Stream Stream,
    string ContentType,
    string FileName);

public sealed record DocumentAttachmentResponse(Guid Id, string FileName, long Size, DateTime UploadedAt, string FileUrl)
{
    public string? Description { get; init; }
    public WarehouseDocumentType? DocumentType { get; init; }
    public int? DocumentId { get; init; }
}
