using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using WMS.Api.Authorization;
using WMS.Api.Constants;
using WMS.Api.Dtos.Files;
using WMS.Api.Dtos.Warehouses;
using WMS.Api.Exceptions;
using WMS.Api.Models;
using WMS.Api.Models.Documents;
using WMS.Api.Repositories;
using WMS.Api.Services.Files;
using WMS.Api.Tests.Infrastructure;

namespace WMS.Api.Tests.Authorization;

public sealed class WarehouseAccessTests(SqlServerDatabaseFixture fixture) : IClassFixture<SqlServerDatabaseFixture>
{
    [Fact]
    public async Task Warehouse_list_is_filtered_before_count_and_pagination()
    {
        await using var db = fixture.CreateContext();
        var first = new Warehouse { Code = "SCOPE-A", Name = "Assigned warehouse" };
        var second = new Warehouse { Code = "SCOPE-B", Name = "Unassigned warehouse" };
        var user = NewUser("scope-list");
        var role = NewRole("SCOPE_VIEW", Permissions.WarehouseView);
        db.AddRange(first, second, user, role);
        db.Add(new WarehouseRoleAssignment { Warehouse = first, User = user, Role = role });
        await db.SaveChangesAsync();

        var access = new WarehouseAccess(db, new FixedCurrentUser(user.Id, Permissions.WarehouseUpdate));
        await access.LoadAsync(Permissions.WarehouseView, default);

        var result = await new WarehouseRepository(db, access).SearchAsync(new WarehouseQuery(), default);
        Assert.Equal(1, result.TotalCount);
        Assert.Equal(first.Id, Assert.Single(result.Items).Id);
        Assert.Throws<ForbiddenException>(() => access.Require(second.Id));
        Assert.False(await access.HasAsync(Permissions.WarehouseUpdate, first.Id, default));
    }

    [Fact]
    public async Task Transfer_attachment_access_follows_view_scope_and_destination_execution_permissions()
    {
        await using var db = fixture.CreateContext();
        var source = new Warehouse { Code = "FILES-A", Name = "Source warehouse" };
        var destination = new Warehouse { Code = "FILES-B", Name = "Destination warehouse" };
        var user = NewUser("scope-files");
        var role = NewRole("FILES_VIEW", Permissions.StockTransferView);
        db.AddRange(source, destination, user, role);
        db.Add(new WarehouseRoleAssignment { Warehouse = destination, User = user, Role = role });
        await db.SaveChangesAsync();

        var transfer = new StockTransfer
        {
            DocumentNo = "FILES-TRANSFER",
            WarehouseId = source.Id,
            DestinationWarehouseId = destination.Id,
            Status = DocumentStatus.InTransit
        };
        db.Add(transfer);
        await db.SaveChangesAsync();
        var attachmentAccess = new DocumentAttachmentAccess(db, new WarehouseAccess(db, new FixedCurrentUser(user.Id)));

        await attachmentAccess.RequireAsync(WarehouseDocumentType.Transfer, transfer.Id, write: false, default);
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            attachmentAccess.RequireAsync(WarehouseDocumentType.Transfer, transfer.Id, write: true, default));

        role.RolePermissions.Add(new RolePermission { Permission = new Permission
        {
            Code = Permissions.StockTransferReceive, Group = "Test"
        } });
        await db.SaveChangesAsync();
        await attachmentAccess.RequireAsync(WarehouseDocumentType.Transfer, transfer.Id, write: true, default);

        var storageDirectory = Directory.CreateTempSubdirectory("wms-attachment-");
        try
        {
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Storage:UploadDirectory"] = storageDirectory.FullName
            }).Build();
            var storage = new FileStorageService(new UploadedFileRepository(db), null!, config, TimeProvider.System,
                attachmentAccess, new FixedCurrentUser(user.Id), NullLogger<FileStorageService>.Instance);
            using var content = new MemoryStream([1]);
            var response = await storage.UploadDocumentAsync(new UploadDocumentRequest
            {
                File = new FormFile(content, 0, 1, "File", "test.pdf")
                    { Headers = new HeaderDictionary(), ContentType = "application/pdf" },
                DocumentType = WarehouseDocumentType.Transfer,
                DocumentId = transfer.Id,
                Description = "  Transfer manifest  "
            }, user.Id, default);
            Assert.Equal("Transfer manifest", response.Description);
            var attachments = await storage.ListDocumentAttachmentsAsync(WarehouseDocumentType.Transfer, transfer.Id, default);
            Assert.Equal(response.Description, Assert.Single(attachments).Description);
        }
        finally
        {
            storageDirectory.Delete(recursive: true);
        }

        transfer.Status = DocumentStatus.Completed;
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            attachmentAccess.RequireAsync(WarehouseDocumentType.Transfer, transfer.Id, write: true, default));

        user.IsActive = false;
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            attachmentAccess.RequireAsync(WarehouseDocumentType.Transfer, transfer.Id, write: false, default));
    }

    private static User NewUser(string username) =>
        new() { Username = username, FullName = username, Email = $"{username}@example.com", PasswordHash = "unused" };

    private static Role NewRole(string code, string permission) =>
        new() { Code = code, Name = code, RolePermissions =
            [new RolePermission { Permission = new Permission { Code = permission, Group = "Test" } }] };
}
