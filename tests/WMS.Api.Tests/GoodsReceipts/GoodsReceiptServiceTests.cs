using FluentValidation.TestHelper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using WMS.Api.Constants;
using WMS.Api.Data;
using WMS.Api.Dtos.GoodsReceipts;
using WMS.Api.Exceptions;
using WMS.Api.Models;
using WMS.Api.Models.Documents;
using WMS.Api.Repositories;
using WMS.Api.Services.Audit;
using WMS.Api.Services.Documents;
using WMS.Api.Services.GoodsReceipts;
using WMS.Api.Services.Inventories;
using WMS.Api.Tests.Infrastructure;
using WMS.Api.Validators;

namespace WMS.Api.Tests.GoodsReceipts;

public sealed class GoodsReceiptFixture : IAsyncLifetime
{
    public SqlServerDatabaseFixture Database { get; } = new();

    public int WarehouseId { get; private set; }

    public int ShelfId { get; private set; }

    public int Bin1Id { get; private set; }

    public int Bin2Id { get; private set; }

    public int OtherWarehouseBinId { get; private set; }

    public int SupplierId { get; private set; }

    public async Task InitializeAsync()
    {
        await Database.InitializeAsync();
        await using var context = Database.CreateContext();
        var (warehouse, shelf, bins) = await WarehouseSeed.WarehouseAsync(context, "GR-WH", 2);
        var (_, _, otherBins) = await WarehouseSeed.WarehouseAsync(context, "GR-OTHER", 1);
        WarehouseId = warehouse.Id;
        ShelfId = shelf.Id;
        Bin1Id = bins[0].Id;
        Bin2Id = bins[1].Id;
        OtherWarehouseBinId = otherBins[0].Id;

        var supplier = new SupplierModel { Code = "ACME", Name = "ACME Co." };
        context.Add(supplier);
        await context.SaveChangesAsync();
        SupplierId = supplier.Id;
    }

    public Task DisposeAsync() => Database.DisposeAsync();
}

public sealed class GoodsReceiptServiceTests(GoodsReceiptFixture fixture) : IClassFixture<GoodsReceiptFixture>
{
    private const int Clerk = 10;    // creates and submits
    private const int Manager = 20;  // approves and completes

    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);

    private sealed class Scope(WmsDbContext context, GoodsReceiptService service) : IAsyncDisposable
    {
        public WmsDbContext Context { get; } = context;

        public GoodsReceiptService Service { get; } = service;

        public ValueTask DisposeAsync() => Context.DisposeAsync();
    }

    private Scope As(int userId, IAuditLogger? auditOverride = null)
    {
        var context = fixture.Database.CreateContext();
        var user = new FixedCurrentUser(userId, Permissions.All.ToArray());
        var time = TimeProvider.System;
        var service = new GoodsReceiptService(
            context,
            new GoodsReceiptRepository(context),
            new DocumentNumberGenerator(context, time),
            new DocumentWorkflow(user, time, Options.Create(new WarehouseOptions())),
            new StockLedger(context, user, time),
            TestServices.Batches(context, user),
            auditOverride ?? new AuditLogger(context, user, time),
            user,
            time);
        return new Scope(context, service);
    }

    private async Task<int> NewProductAsync(bool active = true)
    {
        await using var context = fixture.Database.CreateContext();
        var product = await WarehouseSeed.ProductAsync(context, $"GR-{Guid.NewGuid():N}"[..20]);
        if (!active)
        {
            product.IsActive = false;
            await context.SaveChangesAsync();
        }

        return product.Id;
    }

    private CreateGoodsReceiptRequest Request(params GoodsReceiptLineRequest[] lines) => new()
    {
        WarehouseId = fixture.WarehouseId,
        SupplierId = fixture.SupplierId,
        SupplierName = "typed by user (ignored: the supplier master name wins)",
        ReferenceNo = "INV-001",
        Lines = lines
    };

    private GoodsReceiptLineRequest Line(int productId, decimal quantity, int? locationId = null) => new()
    {
        ProductId = productId,
        LocationId = locationId ?? fixture.Bin1Id,
        Quantity = quantity
    };

    private async Task<GoodsReceiptDetail> ApprovedReceiptAsync(params GoodsReceiptLineRequest[] lines)
    {
        int id;
        await using (var clerk = As(Clerk))
        {
            id = (await clerk.Service.CreateAsync(Request(lines), default)).Id;
            await clerk.Service.SubmitAsync(id, default);
        }

        await using var manager = As(Manager);
        return await manager.Service.ApproveAsync(id, default);
    }

    private async Task<decimal?> StockAsync(int productId, int? locationId = null)
    {
        await using var context = fixture.Database.CreateContext();
        var location = locationId ?? fixture.Bin1Id;
        return await context.Set<Inventory>()
            .Where(i => i.ProductId == productId && i.LocationId == location)
            .Select(i => (decimal?)i.Quantity)
            .SingleOrDefaultAsync();
    }

    private async Task<List<StockMovement>> MovementsAsync(int productId)
    {
        await using var context = fixture.Database.CreateContext();
        return await context.Set<StockMovement>().Where(m => m.ProductId == productId).OrderBy(m => m.Id).ToListAsync();
    }

    private async Task<List<string>> AuditActionsAsync(int receiptId)
    {
        await using var context = fixture.Database.CreateContext();
        return await context.Set<AuditLog>()
            .Where(log => log.EntityType == "GoodsReceipt" && log.EntityId == receiptId.ToString())
            .OrderBy(log => log.Id)
            .Select(log => log.Action)
            .ToListAsync();
    }

    [Fact]
    public async Task Full_flow_creates_receipt_updates_stock_and_writes_movements_and_audit()
    {
        var p1 = await NewProductAsync();
        var p2 = await NewProductAsync();

        var approved = await ApprovedReceiptAsync(Line(p1, 40), Line(p2, 12.5m, fixture.Bin2Id));
        Assert.Equal(DocumentStatus.Approved, approved.Status);
        Assert.Matches(@"^GR-\d{4}-\d{6}$", approved.DocumentNo);
        Assert.Null(await StockAsync(p1)); // nothing posted before completion

        await using var manager = As(Manager);
        var completed = await manager.Service.CompleteAsync(approved.Id, default);

        Assert.Equal(DocumentStatus.Completed, completed.Status);
        Assert.Equal(Manager, completed.Workflow.CompletedBy);
        Assert.Empty(completed.AvailableActions);
        Assert.Equal(40m, await StockAsync(p1));
        Assert.Equal(12.5m, await StockAsync(p2, fixture.Bin2Id));

        var movement = Assert.Single(await MovementsAsync(p1));
        Assert.Equal(MovementType.Receipt, movement.MovementType);
        Assert.Equal((0m, 40m), (movement.QuantityBefore, movement.QuantityAfter));
        Assert.Equal(("GOODS_RECEIPT", approved.Id, approved.DocumentNo), (movement.ReferenceType, movement.ReferenceId, movement.ReferenceNo));
        Assert.Equal(Manager, movement.CreatedBy);

        Assert.Equal(["CREATED", "SUBMITTED", "APPROVED", "COMPLETED"], await AuditActionsAsync(approved.Id));
    }

    [Fact]
    public async Task Second_receipt_adds_to_existing_stock()
    {
        var product = await NewProductAsync();
        foreach (var quantity in new[] { 10m, 15m })
        {
            var approved = await ApprovedReceiptAsync(Line(product, quantity));
            await using var manager = As(Manager);
            await manager.Service.CompleteAsync(approved.Id, default);
        }

        Assert.Equal(25m, await StockAsync(product));
        Assert.Equal([(0m, 10m), (10m, 25m)], (await MovementsAsync(product)).Select(m => (m.QuantityBefore, m.QuantityAfter)));
    }

    [Fact]
    public async Task Failure_after_stock_was_posted_rolls_back_everything()
    {
        var p1 = await NewProductAsync();
        var p2 = await NewProductAsync();
        var approved = await ApprovedReceiptAsync(Line(p1, 5), Line(p2, 7));

        // The audit write happens after the ledger has already updated inventory and movements.
        await using (var manager = As(Manager, new FailingAuditLogger()))
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => manager.Service.CompleteAsync(approved.Id, default));
        }

        Assert.Null(await StockAsync(p1));
        Assert.Null(await StockAsync(p2));
        Assert.Empty(await MovementsAsync(p1));
        await using var check = As(Manager);
        Assert.Equal(DocumentStatus.Approved, (await check.Service.GetAsync(approved.Id, default)).Status);

        // And it can still be completed normally afterwards.
        await check.Service.CompleteAsync(approved.Id, default);
        Assert.Equal(5m, await StockAsync(p1));
    }

    [Fact]
    public async Task Location_deactivated_after_approval_blocks_completion_without_side_effects()
    {
        await using var setup = fixture.Database.CreateContext();
        var (_, _, bins) = await WarehouseSeed.WarehouseAsync(setup, $"GRX{Random.Shared.Next(1000, 9999)}", 1);
        var product = await NewProductAsync();

        int id;
        await using (var clerk = As(Clerk))
        {
            id = (await clerk.Service.CreateAsync(new CreateGoodsReceiptRequest
            {
                WarehouseId = bins[0].WarehouseId,
                Lines = [new GoodsReceiptLineRequest { ProductId = product, LocationId = bins[0].Id, Quantity = 3 }]
            }, default)).Id;
            await clerk.Service.SubmitAsync(id, default);
        }

        await using (var manager = As(Manager))
        {
            await manager.Service.ApproveAsync(id, default);
        }

        await setup.Database.ExecuteSqlAsync($"UPDATE WarehouseLocations SET IsActive = 0 WHERE Id = {bins[0].Id}");

        await using var completer = As(Manager);
        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => completer.Service.CompleteAsync(id, default));
        Assert.Contains("inactive", error.Message);
        Assert.Empty(await MovementsAsync(product));
    }

    [Fact]
    public async Task Concurrent_completion_posts_stock_only_once()
    {
        var product = await NewProductAsync();
        var approved = await ApprovedReceiptAsync(Line(product, 9));

        var results = await Task.WhenAll(Enumerable.Range(0, 5).Select(async _ =>
        {
            await using var manager = As(Manager);
            try
            {
                await manager.Service.CompleteAsync(approved.Id, default);
                return true;
            }
            catch (Exception exception) when (exception is ConflictException or BusinessRuleException or DbUpdateException)
            {
                return false;
            }
        }));

        Assert.Equal(1, results.Count(success => success));
        Assert.Equal(9m, await StockAsync(product));
        Assert.Single(await MovementsAsync(product));
    }

    [Fact]
    public async Task Creator_cannot_approve_own_receipt()
    {
        var product = await NewProductAsync();
        await using var clerk = As(Clerk);
        var receipt = await clerk.Service.CreateAsync(Request(Line(product, 1)), default);
        await clerk.Service.SubmitAsync(receipt.Id, default);

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => clerk.Service.ApproveAsync(receipt.Id, default));
        Assert.Contains("someone other than its creator", error.Message);
    }

    [Fact]
    public async Task Invalid_transitions_are_rejected()
    {
        var product = await NewProductAsync();
        await using var clerk = As(Clerk);
        var receipt = await clerk.Service.CreateAsync(Request(Line(product, 1)), default);

        await Assert.ThrowsAsync<BusinessRuleException>(() => clerk.Service.CompleteAsync(receipt.Id, default)); // Draft
        await Assert.ThrowsAsync<BusinessRuleException>(() => clerk.Service.ApproveAsync(receipt.Id, default));  // Draft

        await clerk.Service.SubmitAsync(receipt.Id, default);
        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            clerk.Service.UpdateAsync(receipt.Id, new UpdateGoodsReceiptRequest { Lines = [Line(product, 2)] }, default));
        await Assert.ThrowsAsync<BusinessRuleException>(() => clerk.Service.DeleteAsync(receipt.Id, default));
    }

    [Fact]
    public async Task Completed_receipt_cannot_be_completed_or_cancelled_again()
    {
        var product = await NewProductAsync();
        var approved = await ApprovedReceiptAsync(Line(product, 4));
        await using var manager = As(Manager);
        await manager.Service.CompleteAsync(approved.Id, default);

        await Assert.ThrowsAsync<BusinessRuleException>(() => manager.Service.CompleteAsync(approved.Id, default));
        await Assert.ThrowsAsync<BusinessRuleException>(() => manager.Service.CancelAsync(approved.Id, "oops", default));
        Assert.Equal(4m, await StockAsync(product));
    }

    [Fact]
    public async Task Reject_reopen_edit_and_resubmit()
    {
        var product = await NewProductAsync();
        GoodsReceiptDetail receipt;
        await using (var firstRequest = As(Clerk))
        {
            receipt = await firstRequest.Service.CreateAsync(Request(Line(product, 1)), default);
            await firstRequest.Service.SubmitAsync(receipt.Id, default);
        }

        await using (var manager = As(Manager))
        {
            var rejected = await manager.Service.RejectAsync(receipt.Id, "Wrong quantity", default);
            Assert.Equal(DocumentStatus.Rejected, rejected.Status);
            Assert.Equal("Wrong quantity", rejected.Workflow.RejectionReason);
        }

        await using var clerk = As(Clerk); // new request scope, like a new HTTP request
        await clerk.Service.ReopenAsync(receipt.Id, default);
        var updated = await clerk.Service.UpdateAsync(receipt.Id, new UpdateGoodsReceiptRequest { Lines = [Line(product, 3)] }, default);
        Assert.Equal(3m, updated.TotalQuantity);
        Assert.Equal(DocumentStatus.PendingApproval, (await clerk.Service.SubmitAsync(receipt.Id, default)).Status);
    }

    [Fact]
    public async Task Cancelling_an_approved_receipt_requires_approve_permission()
    {
        var product = await NewProductAsync();
        var approved = await ApprovedReceiptAsync(Line(product, 1));

        await using var context = fixture.Database.CreateContext();
        var clerkWithoutApprove = new FixedCurrentUser(Clerk, Permissions.GoodsReceiptCreate);
        var service = new GoodsReceiptService(
            context, new GoodsReceiptRepository(context), new DocumentNumberGenerator(context, TimeProvider.System),
            new DocumentWorkflow(clerkWithoutApprove, TimeProvider.System, Options.Create(new WarehouseOptions())),
            new StockLedger(context, clerkWithoutApprove, TimeProvider.System),
            TestServices.Batches(context, clerkWithoutApprove),
            new AuditLogger(context, clerkWithoutApprove, TimeProvider.System), clerkWithoutApprove, TimeProvider.System);

        await Assert.ThrowsAsync<ForbiddenException>(() => service.CancelAsync(approved.Id, "no longer needed", default));
    }

    [Fact]
    public async Task Draft_can_be_deleted()
    {
        var product = await NewProductAsync();
        await using var clerk = As(Clerk);
        var receipt = await clerk.Service.CreateAsync(Request(Line(product, 1)), default);

        await clerk.Service.DeleteAsync(receipt.Id, default);

        await Assert.ThrowsAsync<NotFoundException>(() => clerk.Service.GetAsync(receipt.Id, default));
        Assert.Equal(["CREATED", "DELETED"], await AuditActionsAsync(receipt.Id));
    }

    [Fact]
    public async Task Master_data_rules_are_reported_per_line()
    {
        var active = await NewProductAsync();
        var inactive = await NewProductAsync(active: false);
        await using var clerk = As(Clerk);

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => clerk.Service.CreateAsync(Request(
            Line(999_999, 1),                                 // product does not exist
            Line(inactive, 1),                                // inactive product
            Line(active, 1, fixture.ShelfId),                 // not a bin
            Line(active, 1, fixture.OtherWarehouseBinId),     // other warehouse
            new GoodsReceiptLineRequest { ProductId = active, LocationId = fixture.Bin2Id, Quantity = 1, BatchCode = "L1", ExpiryDate = Today }, // expired
            Line(active, 2, fixture.Bin1Id)), default));      // duplicate of nothing yet

        Assert.Contains("Line 1: product 999999 does not exist", error.Message);
        Assert.Contains("Line 2: product", error.Message);
        Assert.Contains("Line 3: 'Z-01-01' is a Shelf", error.Message);
        Assert.Contains("Line 4: location", error.Message);
        Assert.Contains("Line 5: goods are already expired", error.Message);
    }

    [Fact]
    public async Task Duplicate_product_location_batch_lines_are_rejected()
    {
        var product = await NewProductAsync();
        await using var clerk = As(Clerk);

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            clerk.Service.CreateAsync(Request(Line(product, 1), Line(product, 2)), default));

        Assert.Contains("Lines 1, 2 repeat", error.Message);
    }

    [Fact]
    public async Task Same_product_and_bin_with_different_batches_is_allowed()
    {
        var product = await NewProductAsync();
        var approved = await ApprovedReceiptAsync(
            new GoodsReceiptLineRequest { ProductId = product, LocationId = fixture.Bin1Id, Quantity = 2, BatchCode = "a1", ExpiryDate = Today.AddDays(30) },
            new GoodsReceiptLineRequest { ProductId = product, LocationId = fixture.Bin1Id, Quantity = 3, BatchCode = "A2", ExpiryDate = Today.AddDays(60) });

        await using var manager = As(Manager);
        var completed = await manager.Service.CompleteAsync(approved.Id, default);

        Assert.Equal(["A1", "A2"], completed.Lines.Select(line => line.BatchCode));
        Assert.Equal(5m, await StockAsync(product));
        Assert.Equal(2, (await MovementsAsync(product)).Count);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(0.0001)]
    public void Quantity_must_be_positive_with_max_3_decimals(decimal quantity)
    {
        var result = new GoodsReceiptLineRequestValidator().TestValidate(new GoodsReceiptLineRequest
        {
            ProductId = 1, LocationId = 1, Quantity = quantity
        });

        result.ShouldHaveValidationErrorFor(line => line.Quantity);
    }

    [Fact]
    public void Batch_dates_require_batch_code_and_expiry_after_manufacture()
    {
        var result = new GoodsReceiptLineRequestValidator().TestValidate(new GoodsReceiptLineRequest
        {
            ProductId = 1, LocationId = 1, Quantity = 1,
            ManufactureDate = new DateOnly(2026, 5, 1), ExpiryDate = new DateOnly(2026, 4, 1)
        });

        result.ShouldHaveValidationErrorFor(line => line.BatchCode);
        result.ShouldHaveValidationErrorFor(line => line.ExpiryDate);
    }

    [Fact]
    public void Receipt_without_lines_is_invalid()
    {
        var result = new CreateGoodsReceiptRequestValidator().TestValidate(new CreateGoodsReceiptRequest { WarehouseId = 1 });

        result.ShouldHaveValidationErrorFor(request => request.Lines);
    }

    /// <summary>Simulates a failure at the very end of the completion transaction.</summary>
    private async Task<int> NewSupplierAsync(bool active = true)
    {
        await using var context = fixture.Database.CreateContext();
        var supplier = new SupplierModel
        {
            Code = $"S{Guid.NewGuid():N}"[..20],
            Name = $"Supplier {Guid.NewGuid():N}"[..15],
            IsActive = active
        };
        context.Add(supplier);
        await context.SaveChangesAsync();
        return supplier.Id;
    }

    [Fact]
    public async Task Supplier_name_is_copied_from_the_supplier_master()
    {
        var product = await NewProductAsync();
        await using var clerk = As(Clerk);
        var receipt = await clerk.Service.CreateAsync(Request(Line(product, 1)), default);

        Assert.Equal(fixture.SupplierId, receipt.SupplierId);
        Assert.Equal("ACME Co.", receipt.SupplierName);
    }

    [Fact]
    public async Task Free_text_supplier_name_is_allowed_without_supplier_id()
    {
        var product = await NewProductAsync();
        await using var clerk = As(Clerk);
        var receipt = await clerk.Service.CreateAsync(
            Request(Line(product, 1)) with { SupplierId = null, SupplierName = "  Walk-in seller  " }, default);

        Assert.Null(receipt.SupplierId);
        Assert.Equal("Walk-in seller", receipt.SupplierName);
    }

    [Fact]
    public async Task Unknown_or_inactive_supplier_is_rejected()
    {
        var product = await NewProductAsync();
        var inactive = await NewSupplierAsync(active: false);
        await using var clerk = As(Clerk);

        var unknown = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            clerk.Service.CreateAsync(Request(Line(product, 1)) with { SupplierId = 999_999 }, default));
        Assert.Contains("Supplier 999999 does not exist", unknown.Message);

        var deactivated = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            clerk.Service.CreateAsync(Request(Line(product, 1)) with { SupplierId = inactive }, default));
        Assert.Contains("is inactive", deactivated.Message);
    }

    [Fact]
    public async Task Supplier_deactivated_after_drafting_blocks_submit_but_not_completion_after_approval()
    {
        var product = await NewProductAsync();
        var supplier = await NewSupplierAsync();

        int draftId, approvedId;
        await using (var clerk = As(Clerk))
        {
            draftId = (await clerk.Service.CreateAsync(Request(Line(product, 1)) with { SupplierId = supplier }, default)).Id;
            approvedId = (await clerk.Service.CreateAsync(Request(Line(product, 2)) with { SupplierId = supplier }, default)).Id;
            await clerk.Service.SubmitAsync(approvedId, default);
        }

        await using (var manager = As(Manager))
        {
            await manager.Service.ApproveAsync(approvedId, default);
            await manager.Context.Set<SupplierModel>()
                .Where(s => s.Id == supplier)
                .ExecuteUpdateAsync(set => set.SetProperty(s => s.IsActive, false));
        }

        await using (var clerk = As(Clerk))
        {
            var error = await Assert.ThrowsAsync<BusinessRuleException>(() => clerk.Service.SubmitAsync(draftId, default));
            Assert.Contains("is inactive", error.Message);
        }

        await using (var manager = As(Manager))
        {
            var completed = await manager.Service.CompleteAsync(approvedId, default);
            Assert.Equal(DocumentStatus.Completed, completed.Status);
        }

        Assert.Equal(2m, await StockAsync(product));
    }

    [Fact]
    public async Task Database_rejects_a_supplier_id_that_does_not_exist()
    {
        var product = await NewProductAsync();
        await using var clerk = As(Clerk);
        var receipt = await clerk.Service.CreateAsync(Request(Line(product, 1)), default);

        var exception = await Assert.ThrowsAnyAsync<Exception>(() =>
            clerk.Context.Database.ExecuteSqlAsync($"UPDATE GoodsReceipts SET SupplierId = 999999 WHERE Id = {receipt.Id}"));
        Assert.Contains("FK_GoodsReceipts_Suppliers_SupplierId", exception.Message);
    }

    private sealed class FailingAuditLogger : IAuditLogger
    {
        public void Log(string entityType, object entityId, string action, object? data = null)
        {
            if (action == "COMPLETED")
            {
                throw new InvalidOperationException("Simulated failure while writing the audit log.");
            }
        }
    }
}
