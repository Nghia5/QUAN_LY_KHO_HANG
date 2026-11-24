using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using WMS.Api.Data;
using WMS.Api.Dtos.Files;
using WMS.Api.Exceptions;
using WMS.Api.Models;

namespace WMS.Api.Services.Files;

public sealed class FileStorageService(
    WmsDbContext dbContext,
    IWebHostEnvironment environment,
    IConfiguration configuration,
    TimeProvider timeProvider) : IFileStorageService
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

        dbContext.UploadedFiles.Add(uploadedFile);
        await dbContext.SaveChangesAsync(cancellationToken);

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
        var documentsDir = Path.Combine(BaseStorageDirectory, "documents");
        Directory.CreateDirectory(documentsDir);

        var extension = Path.GetExtension(request.File.FileName).ToLowerInvariant();
        var storedName = $"{Guid.NewGuid():N}{extension}";
        var physicalPath = Path.Combine(documentsDir, storedName);

        await using (var fileStream = new FileStream(physicalPath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            await request.File.CopyToAsync(fileStream, cancellationToken);
        }

        var relativePath = Path.Combine("documents", storedName).Replace('\\', '/');
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

        dbContext.UploadedFiles.Add(uploadedFile);
        await dbContext.SaveChangesAsync(cancellationToken);

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

    public async Task<FileDownloadInfo> GetFileDownloadAsync(Guid fileId, CancellationToken cancellationToken)
    {
        var fileRecord = await dbContext.UploadedFiles
            .SingleOrDefaultAsync(f => f.Id == fileId, cancellationToken)
            ?? throw new NotFoundException("File", fileId);

        var physicalPath = Path.Combine(BaseStorageDirectory, fileRecord.FilePath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(physicalPath))
        {
            throw new NotFoundException("File content on disk", fileId);
        }

        var stream = new FileStream(physicalPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return new FileDownloadInfo(stream, fileRecord.ContentType, fileRecord.OriginalFileName);
    }

    public async Task DeleteFileAsync(Guid fileId, CancellationToken cancellationToken)
    {
        var fileRecord = await dbContext.UploadedFiles
            .SingleOrDefaultAsync(f => f.Id == fileId, cancellationToken)
            ?? throw new NotFoundException("File", fileId);

        var physicalPath = Path.Combine(BaseStorageDirectory, fileRecord.FilePath.Replace('/', Path.DirectorySeparatorChar));
        if (File.Exists(physicalPath))
        {
            File.Delete(physicalPath);
        }

        dbContext.UploadedFiles.Remove(fileRecord);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
