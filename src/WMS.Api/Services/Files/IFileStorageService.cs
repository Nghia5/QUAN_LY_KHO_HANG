using WMS.Api.Dtos.Files;

namespace WMS.Api.Services.Files;

public interface IFileStorageService
{
    Task<UploadFileResponse> UploadAvatarAsync(UploadAvatarRequest request, int? userId, CancellationToken cancellationToken);

    Task<UploadFileResponse> UploadDocumentAsync(UploadDocumentRequest request, int? userId, CancellationToken cancellationToken);

    Task<FileDownloadInfo> GetFileDownloadAsync(Guid fileId, CancellationToken cancellationToken);

    Task DeleteFileAsync(Guid fileId, CancellationToken cancellationToken);
}
