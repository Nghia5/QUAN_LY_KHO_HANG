using Microsoft.AspNetCore.Hosting;
using WMS.Api.Common;
using WMS.Api.Repositories;
using WMS.Api.Dtos.Files;
using WMS.Api.Exceptions;
using WMS.Api.Models;
using WMS.Api.Models.Documents;

namespace WMS.Api.Services.Files;

public sealed class FileStorageService(
    IUploadedFileRepository uploadedFileRepository,
    IWebHostEnvironment environment,
    IConfiguration configuration,
    TimeProvider timeProvider,
    DocumentAttachmentAccess attachmentAccess,
    ICurrentUser currentUser,
    ILogger<FileStorageService> logger) : IFileStorageService
{
    private string BaseStorageDirectory =>
        configuration["Storage:UploadDirectory"]
        ?? Path.Combine(environment.ContentRootPath, "storage", "uploads");

    public async Task<UploadFileResponse> UploadAvatarAsync(UploadAvatarRequest request, int? userId, CancellationToken cancellationToken)
    {
        var avatarsDir = Path.Combine(BaseStorageDirectory, "avatars");
        Directory.CreateDirectory(avatarsDir);

        var extension = Path.GetExtension(request.File.FileName).ToLowerInvariant();
        var storedName = $"{Guid.NewGuid():N}{extension}";
        var physicalPath = Path.Combine(avatarsDir, storedName);

        await using (var fileStream = new FileStream(physicalPath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            await request.File.CopyToAsync(fileStream, cancellationToken);
        }

        var relativePath = Path.Combine("avatars", storedName).Replace('\\', '/');
        var uploadedFile = new UploadedFile
        {
            OriginalFileName = Path.GetFileName(request.File.FileName),
            StoredFileName = storedName,
            FilePath = relativePath,
            ContentType = request.File.ContentType,
            FileSize = request.File.Length,
            UploadedBy = userId,
            UploadedAt = timeProvider.GetUtcNow().UtcDateTime
        };

        uploadedFileRepository.Add(uploadedFile);
        await uploadedFileRepository.SaveChangesAsync(cancellationToken);

        var fileUrl = $"/api/files/{uploadedFile.Id}";
        return new UploadFileResponse(
            uploadedFile.Id,
            uploadedFile.OriginalFileName,
            uploadedFile.StoredFileName,
            fileUrl,
            uploadedFile.ContentType,
            uploadedFile.FileSize,
            uploadedFile.UploadedAt);
    }

    public async Task<UploadFileResponse> UploadDocumentAsync(UploadDocumentRequest request, int? userId, CancellationToken cancellationToken)
    {
        if (request.DocumentType is not { } documentType || request.DocumentId is not { } documentId)
        {
            throw new BusinessRuleException("A document type and ID are required.");
        }

        await using var transaction = await attachmentAccess.BeginWriteAsync(documentType, documentId, cancellationToken);
        await attachmentAccess.RequireAsync(documentType, documentId, write: true, cancellationToken);
        var documentsDir = Path.Combine(BaseStorageDirectory, "documents");
        Directory.CreateDirectory(documentsDir);

        var extension = Path.GetExtension(request.File.FileName).ToLowerInvariant();
        var storedName = $"{Guid.NewGuid():N}{extension}";
        var physicalPath = Path.Combine(documentsDir, storedName);

        var relativePath = Path.Combine("documents", storedName).Replace('\\', '/');
        var uploadedFile = new UploadedFile
        {
            OriginalFileName = Path.GetFileName(request.File.FileName),
            StoredFileName = storedName,
            FilePath = relativePath,
            ContentType = request.File.ContentType,
            FileSize = request.File.Length,
            UploadedBy = userId,
            DocumentType = documentType,
            DocumentId = documentId,
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            UploadedAt = timeProvider.GetUtcNow().UtcDateTime
        };

        try
        {
            await using (var fileStream = new FileStream(physicalPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                await request.File.CopyToAsync(fileStream, cancellationToken);
            }

            uploadedFileRepository.Add(uploadedFile);
            await uploadedFileRepository.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            try
            {
                File.Delete(physicalPath);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                logger.LogWarning(exception, "Could not remove an incomplete attachment {FileId}.", uploadedFile.Id);
            }

            throw;
        }

        await transaction.CommitAsync(cancellationToken);

        var fileUrl = $"/api/files/{uploadedFile.Id}";
        return new UploadFileResponse(
            uploadedFile.Id,
            uploadedFile.OriginalFileName,
            uploadedFile.StoredFileName,
            fileUrl,
            uploadedFile.ContentType,
            uploadedFile.FileSize,
            uploadedFile.UploadedAt)
        {
            Description = uploadedFile.Description
        };
    }

    public async Task<FileDownloadInfo> GetFileDownloadAsync(Guid fileId, int userId, CancellationToken cancellationToken)
    {
        var fileRecord = await FindAccessibleFileAsync(fileId, userId, write: false, cancellationToken);

        var physicalPath = Path.Combine(BaseStorageDirectory, fileRecord.FilePath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(physicalPath))
        {
            throw new NotFoundException("File content on disk", fileId);
        }

        var stream = new FileStream(physicalPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return new FileDownloadInfo(stream, fileRecord.ContentType, fileRecord.OriginalFileName);
    }

    public async Task DeleteFileAsync(Guid fileId, int userId, CancellationToken cancellationToken)
    {
        var fileRecord = await FindAccessibleFileAsync(fileId, userId, write: true, cancellationToken);

        var physicalPath = Path.Combine(BaseStorageDirectory, fileRecord.FilePath.Replace('/', Path.DirectorySeparatorChar));
        if (File.Exists(physicalPath))
        {
            File.Delete(physicalPath);
        }

        uploadedFileRepository.Remove(fileRecord);
        await uploadedFileRepository.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<DocumentAttachmentResponse>> ListDocumentAttachmentsAsync(
        WarehouseDocumentType type, int id, CancellationToken cancellationToken)
    {
        await attachmentAccess.RequireAsync(type, id, write: false, cancellationToken);
        var files = await uploadedFileRepository.ListForDocumentAsync(type, id, cancellationToken);
        return files.Select(file => new DocumentAttachmentResponse(
            file.Id, file.OriginalFileName, file.FileSize, file.UploadedAt, $"/api/files/{file.Id}")
        {
            Description = file.Description,
            DocumentType = file.DocumentType,
            DocumentId = file.DocumentId,
        }).ToList();
    }

    public async Task<IReadOnlyList<DocumentAttachmentResponse>> ListAllAsync(CancellationToken cancellationToken)
    {
        var files = await uploadedFileRepository.ListAllAsync(cancellationToken);
        return files.Select(file => new DocumentAttachmentResponse(
            file.Id, file.OriginalFileName, file.FileSize, file.UploadedAt, $"/api/files/{file.Id}")
        {
            Description = file.Description,
            DocumentType = file.DocumentType,
            DocumentId = file.DocumentId,
        }).ToList();
    }

    private async Task<UploadedFile> FindAccessibleFileAsync(
        Guid fileId, int userId, bool write, CancellationToken cancellationToken)
    {
        var file = await uploadedFileRepository.FindAsync(fileId, cancellationToken)
            ?? throw new NotFoundException("File", fileId);

        var isAttachment = file.DocumentType is not null && file.DocumentId is not null;
        if (!currentUser.IsAdmin && (write || !isAttachment) && file.UploadedBy != userId)
        {
            throw new NotFoundException("File", fileId);
        }

        if (file.DocumentType is { } type && file.DocumentId is { } documentId)
        {
            await attachmentAccess.RequireAsync(type, documentId, write, cancellationToken);
        }

        return file;
    }
}
