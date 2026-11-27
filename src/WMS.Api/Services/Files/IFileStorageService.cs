using WMS.Api.Dtos.Files;
using WMS.Api.Models.Documents;

namespace WMS.Api.Services.Files;

public interface IFileStorageService
{
    Task<UploadFileResponse> UploadAvatarAsync(UploadAvatarRequest request, int? userId, CancellationToken cancellationToken);

    Task<UploadFileResponse> UploadDocumentAsync(UploadDocumentRequest request, int? userId, CancellationToken cancellationToken);

    Task<IReadOnlyList<DocumentAttachmentResponse>> ListDocumentAttachmentsAsync(
        WarehouseDocumentType type, int id, CancellationToken cancellationToken);

    Task<FileDownloadInfo> GetFileDownloadAsync(Guid fileId, int userId, CancellationToken cancellationToken);

    Task DeleteFileAsync(Guid fileId, int userId, CancellationToken cancellationToken);
}
