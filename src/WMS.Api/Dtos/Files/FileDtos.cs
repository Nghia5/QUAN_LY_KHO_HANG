namespace WMS.Api.Dtos.Files;

/// <summary>Response returned when a file is successfully uploaded.</summary>
public sealed record UploadFileResponse(
    Guid Id,
    string OriginalFileName,
    string StoredFileName,
    string FileUrl,
    string ContentType,
    long FileSize,
    DateTime UploadedAt);

/// <summary>Internal transport object for file download streaming.</summary>
public sealed record FileDownloadInfo(
    Stream Stream,
    string ContentType,
    string FileName);
