using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WMS.Api.Common;
using WMS.Api.Dtos.Files;
using WMS.Api.Exceptions;
using WMS.Api.Models.Documents;
using WMS.Api.Services.Files;

namespace WMS.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/files")]
public sealed class FilesController(IFileStorageService fileStorageService, ICurrentUser currentUser) : ControllerBase
{
    [HttpPost("avatar")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<UploadFileResponse>> UploadAvatar(
        [FromForm] UploadAvatarRequest request, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedException();
        var file = await fileStorageService.UploadAvatarAsync(request, userId, cancellationToken);
        return CreatedAtAction(nameof(Download), new { id = file.Id }, file);
    }

    [HttpPost("document")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<UploadFileResponse>> UploadDocument(
        [FromForm] UploadDocumentRequest request, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedException();
        var file = await fileStorageService.UploadDocumentAsync(request, userId, cancellationToken);
        return CreatedAtAction(nameof(Download), new { id = file.Id }, file);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Download(Guid id, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedException();
        var file = await fileStorageService.GetFileDownloadAsync(id, userId, cancellationToken);
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return File(file.Stream, "application/octet-stream", file.FileName);
    }

    [HttpGet("document/{documentType}/{documentId:int:min(1)}")]
    public async Task<ActionResult<IReadOnlyList<DocumentAttachmentResponse>>> ListDocumentAttachments(
        WarehouseDocumentType documentType, int documentId, CancellationToken cancellationToken) =>
        Ok(await fileStorageService.ListDocumentAttachmentsAsync(documentType, documentId, cancellationToken));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedException();
        await fileStorageService.DeleteFileAsync(id, userId, cancellationToken);
        return NoContent();
    }
}
