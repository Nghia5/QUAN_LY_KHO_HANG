using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using WMS.Api.Constants;
using WMS.Api.Data;
using WMS.Api.Dtos.Batches;
using WMS.Api.Dtos.GoodsIssues;
using WMS.Api.Dtos.GoodsReceipts;
using WMS.Api.Dtos.StockTransfers;
using WMS.Api.Exceptions;
using WMS.Api.Models;
using WMS.Api.Repositories;
using WMS.Api.Services.Audit;
using WMS.Api.Services.Documents;
using WMS.Api.Services.GoodsIssues;
using WMS.Api.Services.GoodsReceipts;
using WMS.Api.Services.Inventories;
using WMS.Api.Services.StockTransfers;
using WMS.Api.Tests.Infrastructure;

namespace WMS.Api.Tests.Batches;

public sealed class BatchFixture : IAsyncLifetime
{
    public SqlServerDatabaseFixture Database { get; } = new();

    public Task InitializeAsync() => Database.InitializeAsync();

    public Task DisposeAsync() => Database.DisposeAsync();
}

public sealed class BatchTests(BatchFixture fixture) : IClassFixture<BatchFixture>
{
    private const int Clerk = 10;
    private const int Manager = 20;
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);

    private sealed record Site(int WarehouseId, int B1, int B2);

    private async Task<Site> SiteAsync()
    {
        await using var context = fixture.Database.CreateContext();
        var (warehouse, _, bins) = await WarehouseSeed.WarehouseAsync(context, $"BT{Guid.NewGuid():N}"[..12], 2);
        return new Site(warehouse.Id, bins[0].Id, bins[1].Id);
    }

    private async Task<int> ProductAsync()
    {
        await using var context = fixture.Database.CreateContext();
        return (await WarehouseSeed.ProductAsync(context, $"BP-{Guid.NewGuid():N}"[..20])).Id;
    }

    private (WmsDbContext Context, FixedCurrentUser User, DocumentWorkflow Workflow, StockLedger Ledger) Parts(int userId)
    {
        var context = fixture.Database.CreateContext();
        var user = new FixedCurrentUser(userId, Permissions.All.ToArray());
        return (context, user, new DocumentWorkflow(user, TimeProvider.System, Options.Create(new WarehouseOptions())),
            new StockLedger(context, user, TimeProvider.System));
    }

    private GoodsReceiptService Receipts(int userId)
    {
        var (context, user, workflow, ledger) = Parts(userId);
        return new GoodsReceiptService(context, new GoodsReceiptRepository(context), new DocumentNumberGenerator(context, TimeProvider.System),
            workflow, ledger, TestServices.Batches(context, user), new AuditLogger(context, user, TimeProvider.System), user, TimeProvider.System);
    }

    private GoodsIssueService Issues(int userId)
    {
        var (context, user, workflow, ledger) = Parts(userId);
        return new GoodsIssueService(context, new GoodsIssueRepository(context), new DocumentNumberGenerator(context, TimeProvider.System),
            workflow, ledger, new StockReservationService(context), new AuditLogger(context, user, TimeProvider.System), user, TimeProvider.System);
    }

    private StockTransferService Transfers(int userId)
    {
        var (context, user, workflow, ledger) = Parts(userId);
        return new StockTransferService(context, new StockTransferRepository(context), new DocumentNumberGenerator(context, TimeProvider.System),
            workflow, ledger, new StockReservationService(context), new AuditLogger(context, user, TimeProvider.System), user);
    }

    private Services.Batches.BatchService Batches(int nearExpiryDays = 30) =>
        TestServices.Batches(fixture.Database.CreateContext(), new FixedCurrentUser(Manager), nearExpiryDays: nearExpiryDays);

    /// <summary>Full receipt flow (create → submit → approve → complete).</summary>
    private async Task ReceiveAsync(Site site, params GoodsReceiptLineRequest[] lines)
    {
        var id = (await Receipts(Clerk).CreateAsync(new CreateGoodsReceiptRequest { WarehouseId = site.WarehouseId, Lines = lines }, default)).Id;
        await Receipts(Clerk).SubmitAsync(id, default);
        await Receipts(Manager).ApproveAsync(id, default);
        await Receipts(Manager).CompleteAsync(id, default);
    }

    private static GoodsReceiptLineRequest Lot(int productId, int binId, decimal quantity, string? batch = null, int? expiresInDays = null) => new()
    {
        ProductId = productId,
        LocationId = binId,
        Quantity = quantity,
        BatchCode = batch,
        ExpiryDate = expiresInDays is { } days ? Today.AddDays(days) : null
    };

    private async Task<int> IssueAsync(Site site, int productId, int binId, decimal quantity, int? batchId = null)
    {
        var id = (await Issues(Clerk).CreateAsync(new CreateGoodsIssueRequest
        {
            WarehouseId = site.WarehouseId,
            Lines = [new GoodsIssueLineRequest { ProductId = productId, LocationId = binId, Quantity = quantity, BatchId = batchId }]
        }, default)).Id;
        await Issues(Clerk).SubmitAsync(id, default);
        await Issues(Manager).ApproveAsync(id, default);
        await Issues(Manager).CompleteAsync(id, default);
        return id;
    }

    private async Task<Dictionary<string, decimal>> LotsAsync(int productId, int binId)
    {
        await using var context = fixture.Database.CreateContext();
        return await context.Set<InventoryLot>()
            .Where(lot => lot.ProductId == productId && lot.Inventory!.LocationId == binId)
            .Select(lot => new { lot.Batch!.BatchCode, lot.Quantity })
            .ToDictionaryAsync(lot => lot.BatchCode, lot => lot.Quantity);
    }

    private async Task<int> BatchIdAsync(int productId, string code)
    {
        await using var context = fixture.Database.CreateContext();
        return await context.Set<Batch>().Where(b => b.ProductId == productId && b.BatchCode == code).Select(b => b.Id).SingleAsync();
    }

    private async Task<decimal> QuantityAsync(int productId, int binId)
    {
        await using var context = fixture.Database.CreateContext();
        return await context.Set<Inventory>().Where(i => i.ProductId == productId && i.LocationId == binId).Select(i => i.Quantity).SingleAsync();
    }

    [Fact]
    public async Task Receipt_creates_batch_and_lot_and_reuses_the_batch_next_time()
    {
        var site = await SiteAsync();
        var product = await ProductAsync();

        await ReceiveAsync(site, Lot(product, site.B1, 10, "l-001", 90));
        await ReceiveAsync(site, Lot(product, site.B1, 5, "L-001", 90), Lot(product, site.B2, 4, "L-001", 90));

        await using var context = fixture.Database.CreateContext();
        Assert.Equal(1, await context.Set<Batch>().CountAsync(b => b.ProductId == product));
        Assert.Equal(new Dictionary<string, decimal> { ["L-001"] = 15 }, await LotsAsync(product, site.B1));

        var detail = await Batches().GetAsync(await BatchIdAsync(product, "L-001"), default);
        Assert.Equal(19m, detail.Batch.Quantity); // batch quantity = sum of its lots
        Assert.Equal(2, detail.Lots.Count);
        Assert.Equal(Today.AddDays(90), detail.Batch.ExpiryDate);
    }

    [Fact]
    public async Task Receipt_with_existing_code_but_other_dates_is_rejected()
    {
        var site = await SiteAsync();
        var product = await ProductAsync();
        await ReceiveAsync(site, Lot(product, site.B1, 1, "L-X", 90));

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => Receipts(Clerk).CreateAsync(new CreateGoodsReceiptRequest
        {
            WarehouseId = site.WarehouseId,
            Lines = [Lot(product, site.B1, 1, "L-X", 120)]
        }, default));

        Assert.Contains("already exists with", error.Message);
    }

    [Fact]
    public async Task Issue_without_batch_uses_fefo_then_stock_without_lot()
    {
        var site = await SiteAsync();
        var product = await ProductAsync();
        await ReceiveAsync(site,
            Lot(product, site.B1, 5, "LATE", 60),
            Lot(product, site.B1, 5, "EARLY", 10),
            Lot(product, site.B1, 3));                  // without lot

        var issueId = await IssueAsync(site, product, site.B1, 12);

        Assert.Equal(new Dictionary<string, decimal> { ["EARLY"] = 0, ["LATE"] = 0 }, await LotsAsync(product, site.B1));
        Assert.Equal(1m, await QuantityAsync(product, site.B1)); // 13 - 12, the remaining unit has no lot

        await using var context = fixture.Database.CreateContext();
        var movements = await context.Set<StockMovement>()
            .Where(m => m.ReferenceType == "GOODS_ISSUE" && m.ReferenceId == issueId)
            .OrderBy(m => m.Id)
            .Select(m => new { Batch = m.Batch != null ? m.Batch.BatchCode : null, m.Quantity, m.QuantityBefore, m.QuantityAfter })
            .ToListAsync();
        Assert.Equal(["EARLY", "LATE", null], movements.Select(m => m.Batch));          // FEFO order, lot-less last
        Assert.Equal([-5m, -5m, -2m], movements.Select(m => m.Quantity));
        Assert.Equal([(13m, 8m), (8m, 3m), (3m, 1m)], movements.Select(m => (m.QuantityBefore, m.QuantityAfter))); // unbroken chain
    }

    [Fact]
    public async Task Issue_of_a_specific_batch_takes_only_that_lot()
    {
        var site = await SiteAsync();
        var product = await ProductAsync();
        await ReceiveAsync(site, Lot(product, site.B1, 5, "A", 10), Lot(product, site.B1, 5, "B", 60));

        await IssueAsync(site, product, site.B1, 4, await BatchIdAsync(product, "B"));

        Assert.Equal(new Dictionary<string, decimal> { ["A"] = 5, ["B"] = 1 }, await LotsAsync(product, site.B1));
    }

    [Fact]
    public async Task Expired_lots_are_never_issued()
    {
        var site = await SiteAsync();
        var product = await ProductAsync();
        var expired = await Batches().CreateAsync(new CreateBatchRequest
        {
            ProductId = product, BatchCode = "OLD", ManufactureDate = Today.AddDays(-400), ExpiryDate = Today.AddDays(-1)
        }, default);
        await using (var context = fixture.Database.CreateContext())
        {
            await new StockLedger(context, new FixedCurrentUser(1), TimeProvider.System).ApplyAsync(
                [new StockChange(product, site.WarehouseId, site.B1, 5, MovementType.Receipt, BatchId: expired.Batch.Id)],
                new StockReference("TEST_SEED", null, null), default);
        }

        // Explicit expired batch: rejected when the issue is created.
        var explicitError = await Assert.ThrowsAsync<BusinessRuleException>(() => Issues(Clerk).CreateAsync(new CreateGoodsIssueRequest
        {
            WarehouseId = site.WarehouseId,
            Lines = [new GoodsIssueLineRequest { ProductId = product, LocationId = site.B1, Quantity = 1, BatchId = expired.Batch.Id }]
        }, default));
        Assert.Contains("expired", explicitError.Message);

        // Automatic FEFO: the expired lot is skipped, so completion fails and everything rolls back.
        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => IssueAsync(site, product, site.B1, 2));
        Assert.Contains("expired", error.Message);
        Assert.Equal(5m, await QuantityAsync(product, site.B1));
        Assert.Equal(new Dictionary<string, decimal> { ["OLD"] = 5 }, await LotsAsync(product, site.B1));
    }

    [Fact]
    public async Task Transfer_moves_the_same_lots_to_the_destination()
    {
        var site = await SiteAsync();
        var product = await ProductAsync();
        await ReceiveAsync(site, Lot(product, site.B1, 4, "T1", 20), Lot(product, site.B1, 6, "T2", 50));

        var id = (await Transfers(Clerk).CreateAsync(new CreateStockTransferRequest
        {
            WarehouseId = site.WarehouseId,
            DestinationWarehouseId = site.WarehouseId,
            Lines = [new StockTransferLineRequest { ProductId = product, FromLocationId = site.B1, ToLocationId = site.B2, Quantity = 7 }]
        }, default)).Id;
        await Transfers(Clerk).SubmitAsync(id, default);
        await Transfers(Manager).ApproveAsync(id, default);
        await Transfers(Clerk).PickAsync(id, default);
        await Transfers(Clerk).ShipAsync(id, default);
        await Transfers(Clerk).ReceiveAsync(id, default);

        Assert.Equal(new Dictionary<string, decimal> { ["T1"] = 0, ["T2"] = 3 }, await LotsAsync(product, site.B1));
        Assert.Equal(new Dictionary<string, decimal> { ["T1"] = 4, ["T2"] = 3 }, await LotsAsync(product, site.B2));
    }

    [Fact]
    public async Task Lot_of_another_product_is_rejected_by_the_database()
    {
        var site = await SiteAsync();
        var product = await ProductAsync();
        var other = await ProductAsync();
        await ReceiveAsync(site, Lot(product, site.B1, 1, "MINE", 30));
        var otherBatch = await Batches().CreateAsync(new CreateBatchRequest { ProductId = other, BatchCode = "THEIRS" }, default);

        await using var context = fixture.Database.CreateContext();
        var inventoryId = await context.Set<Inventory>().Where(i => i.ProductId == product && i.LocationId == site.B1).Select(i => i.Id).SingleAsync();

        // Lot claims product = inventory's product, but the batch belongs to another product.
        var error = await Assert.ThrowsAsync<SqlException>(() => context.Database.ExecuteSqlAsync(
            $"INSERT INTO InventoryLots (InventoryId, BatchId, ProductId, Quantity) VALUES ({inventoryId}, {otherBatch.Batch.Id}, {product}, 1)"));
        Assert.Equal(547, error.Number);
    }

    [Fact]
    public async Task Expiry_status_queries_and_alerts_are_accurate()
    {
        var site = await SiteAsync();
        var product = await ProductAsync();
        await ReceiveAsync(site,
            Lot(product, site.B1, 1, "NEAR", 5),
            Lot(product, site.B1, 2, "VALID", 200),
            Lot(product, site.B2, 3, "NOEXP"));
        var expired = await Batches().CreateAsync(new CreateBatchRequest { ProductId = product, BatchCode = "EXPIRED", ExpiryDate = Today.AddDays(-3) }, default);
        await using (var context = fixture.Database.CreateContext())
        {
            await new StockLedger(context, new FixedCurrentUser(1), TimeProvider.System).ApplyAsync(
                [new StockChange(product, site.WarehouseId, site.B2, 4, MovementType.AdjustmentIn, BatchId: expired.Batch.Id)],
                new StockReference("TEST_SEED", null, null), default);
        }

        var all = await Batches().SearchAsync(new BatchQuery { ProductId = product, PageSize = 100 }, default);
        var status = all.Items.ToDictionary(batch => batch.BatchCode, batch => (batch.ExpiryStatus, batch.DaysToExpiry));
        Assert.Equal((ExpiryStatus.NearExpiry, 5), status["NEAR"]);
        Assert.Equal((ExpiryStatus.Valid, 200), status["VALID"]);
        Assert.Equal((ExpiryStatus.NoExpiry, (int?)null), status["NOEXP"]);
        Assert.Equal((ExpiryStatus.Expired, -3), status["EXPIRED"]);

        var near = await Batches().SearchAsync(new BatchQuery { ProductId = product, ExpiryStatus = ExpiryStatus.NearExpiry }, default);
        Assert.Equal(["NEAR"], near.Items.Select(batch => batch.BatchCode));
        var within = await Batches().SearchAsync(new BatchQuery { ProductId = product, ExpiringWithinDays = 300 }, default);
        Assert.Equal(["NEAR", "VALID"], within.Items.Select(batch => batch.BatchCode)); // sorted by expiry

        var alerts = await Batches().GetExpiryAlertsAsync(new ExpiryAlertQuery { ProductId = product }, default);
        Assert.Equal([("EXPIRED", ExpiryStatus.Expired, 4m), ("NEAR", ExpiryStatus.NearExpiry, 1m)],
            alerts.Items.Select(alert => (alert.BatchCode, alert.Status, alert.Quantity)));
        Assert.All(alerts.Items, alert => Assert.Equal(site.WarehouseId, alert.WarehouseId));
    }

    [Fact]
    public async Task Pick_suggestion_follows_fefo_or_fifo_and_respects_reservations()
    {
        var site = await SiteAsync();
        var product = await ProductAsync();
        await using (var setup = fixture.Database.CreateContext())
        {
            // Older lot (manufactured first) expires later; newer lot expires first.
            var batches = TestServices.Batches(setup, new FixedCurrentUser(1));
            var older = await batches.CreateAsync(new CreateBatchRequest { ProductId = product, BatchCode = "OLDER", ManufactureDate = Today.AddDays(-100), ExpiryDate = Today.AddDays(300) }, default);
            var newer = await batches.CreateAsync(new CreateBatchRequest { ProductId = product, BatchCode = "NEWER", ManufactureDate = Today.AddDays(-10), ExpiryDate = Today.AddDays(30) }, default);
            await new StockLedger(setup, new FixedCurrentUser(1), TimeProvider.System).ApplyAsync(
            [
                new StockChange(product, site.WarehouseId, site.B1, 5, MovementType.Receipt, BatchId: older.Batch.Id),
                new StockChange(product, site.WarehouseId, site.B2, 5, MovementType.Receipt, BatchId: newer.Batch.Id)
            ], new StockReference("TEST_SEED", null, null), default);
        }

        var fefo = await Batches().SuggestPickAsync(new PickSuggestionQuery { ProductId = product, WarehouseId = site.WarehouseId, Quantity = 6 }, default);
        Assert.Equal([("NEWER", 5m), ("OLDER", 1m)], fefo.Lines.Select(line => (line.BatchCode!, line.PickQuantity)));

        var fifo = await Batches().SuggestPickAsync(new PickSuggestionQuery { ProductId = product, WarehouseId = site.WarehouseId, Quantity = 6, Strategy = PickStrategy.Fifo }, default);
        Assert.Equal([("OLDER", 5m), ("NEWER", 1m)], fifo.Lines.Select(line => (line.BatchCode!, line.PickQuantity)));

        await using (var context = fixture.Database.CreateContext())
        {
            await context.Database.ExecuteSqlAsync($"UPDATE Inventories SET ReservedQuantity = 4 WHERE ProductId = {product} AND LocationId = {site.B2}");
        }

        var limited = await Batches().SuggestPickAsync(new PickSuggestionQuery { ProductId = product, WarehouseId = site.WarehouseId, Quantity = 12 }, default);
        Assert.Equal([("NEWER", 1m), ("OLDER", 5m)], limited.Lines.Select(line => (line.BatchCode!, line.PickQuantity)));
        Assert.Equal(6m, limited.Shortage);
    }

    [Fact]
    public async Task Lots_never_exceed_stock_and_match_their_movements()
    {
        await using var context = fixture.Database.CreateContext();

        var overflow = await context.Set<Inventory>()
            .Where(i => context.Set<InventoryLot>().Where(lot => lot.InventoryId == i.Id).Sum(lot => (decimal?)lot.Quantity) > i.Quantity)
            .CountAsync();
        Assert.Equal(0, overflow);

        var lots = await context.Set<InventoryLot>()
            .Select(lot => new
            {
                lot.Quantity,
                Ledger = context.Set<StockMovement>()
                    .Where(m => m.InventoryId == lot.InventoryId && m.BatchId == lot.BatchId)
                    .Sum(m => (decimal?)m.Quantity) ?? 0m
            })
            .ToListAsync();
        Assert.All(lots, lot => Assert.Equal(lot.Ledger, lot.Quantity));
    }

    [Fact]
    public async Task Batch_management_rules()
    {
        var product = await ProductAsync();
        var batches = Batches();
        var created = await batches.CreateAsync(new CreateBatchRequest { ProductId = product, BatchCode = "m-1" }, default);
        Assert.Equal("M-1", created.Batch.BatchCode);

        await Assert.ThrowsAsync<ConflictException>(() => Batches().CreateAsync(new CreateBatchRequest { ProductId = product, BatchCode = "M-1" }, default));
        await Assert.ThrowsAsync<BusinessRuleException>(() => Batches().CreateAsync(new CreateBatchRequest
        {
            ProductId = product, BatchCode = "FUTURE", ManufactureDate = Today.AddDays(1)
        }, default));

        var site = await SiteAsync();
        await ReceiveAsync(site, Lot(product, site.B1, 1, "USED", 30));
        await Assert.ThrowsAsync<ConflictException>(async () => await Batches().DeleteAsync(await BatchIdAsync(product, "USED"), default));
        await Batches().DeleteAsync(created.Batch.Id, default); // never used: can be deleted
    }
}
