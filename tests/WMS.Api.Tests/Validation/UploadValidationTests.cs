using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using WMS.Api.Dtos.Files;
using WMS.Api.Models.Documents;

namespace WMS.Api.Tests.Validation;

public sealed class UploadValidationTests
{
    [Fact]
    public void Uploads_reject_metadata_that_does_not_fit_storage()
    {
        var avatar = new UploadAvatarRequest { File = MakeFile(new string('a', 252) + ".png", "image/png") };
        var document = new UploadDocumentRequest
        {
            File = MakeFile("receipt.pdf", new string('a', 101)),
            DocumentType = WarehouseDocumentType.Receipt,
            DocumentId = 1
        };

        Assert.Contains(avatar.Validate(new ValidationContext(avatar)), error => error.ErrorMessage!.Contains("file name"));
        Assert.Contains(document.Validate(new ValidationContext(document)), error => error.ErrorMessage!.Contains("content type"));
        Assert.Empty((document with { File = MakeFile("receipt.pdf", "application/pdf") })
            .Validate(new ValidationContext(document)));
    }

    private static FormFile MakeFile(string name, string contentType) =>
        new(Stream.Null, 0, 1, "File", name) { Headers = new HeaderDictionary(), ContentType = contentType };
}
