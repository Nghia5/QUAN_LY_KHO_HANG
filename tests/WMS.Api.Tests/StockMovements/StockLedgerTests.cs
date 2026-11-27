using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using WMS.Api.Data;
using WMS.Api.Dtos.StockMovements;
using WMS.Api.Exceptions;
using WMS.Api.Models;
using WMS.Api.Repositories;
using WMS.Api.Services.Inventories;
using WMS.Api.Tests.Infrastructure;

namespace WMS.Api.Tests.StockMovements;

public sealed class StockLedgerFixture : IAsyncLifetime
{
    public SqlServerDatabaseFixture Database { get; } = new();

    public int WarehouseId { get; private set; }

    public int ShelfId { get; private set; }

    public int Bin1Id { get; private set; }

    public int Bin2Id { get; private set; }

    public async Task InitializeAsync()
    {
        await Database.InitializeAsync();
        await using var context = Database.CreateContext();
        var (warehouse, shelf, bins) = await WarehouseSeed.WarehouseAsync(context, "LEDGER", 2);
        WarehouseId = warehouse.Id;
        ShelfId = shelf.Id;
        Bin1Id = bins[0].Id;
        Bin2Id = bins[1].Id;
    }

    public Task DisposeAsync() => Database.DisposeAsync();
}

public sealed class StockLedgerTests(StockLedgerFixture fixture) : IClassFixture<StockLedgerFixture>
{
    private static readonly StockReference TestReference = new("TEST", 1, "T-0001");

    private StockLedger Ledger(WmsDbContext context, TimeProvider? time = null) =>
        new(context, new FixedCurrentUser(42), time ?? TimeProvider.System);

    private async Task<int> NewProductAsync()
    {
        await using var context = fixture.Database.CreateContext();
        return (await WarehouseSeed.ProductAsync(context, $"L-{Guid.NewGuid():N}"[..20])).Id;
    }

    private StockChange Change(int productId, decimal quantity, MovementType type, int? locationId = null) =>
        new(productId, fixture.WarehouseId, locationId ?? fixture.Bin1Id, quantity, type);

    private async Task<(decimal Quantity, decimal Reserved)?> InventoryAsync(int productId, int? locationId = null)
    {
        await using var context = fixture.Database.CreateContext();
        var location = locationId ?? fixture.Bin1Id;
        var row = await context.Set<Inventory>()
            .Where(i => i.ProductId == productId && i.LocationId == location)
            .Select(i => new { i.Quantity, i.ReservedQuantity })
            .SingleOrDefaultAsync();
        return row is null ? null : (row.Quantity, row.ReservedQuantity);
    }

    private async Task<List<StockMovement>> MovementsAsync(int productId)
    {
        await using var context = fixture.Database.CreateContext();
        return await context.Set<StockMovement>().Where(m => m.ProductId == productId).OrderBy(m => m.Id).ToListAsync();
    }

    [Fact]
    public async Task First_receipt_creates_inventory_and_movement_with_before_zero()
    {
        var productId = await NewProductAsync();
        await using var context = fixture.Database.CreateContext();

        var movements = await Ledger(context).ApplyAsync([Change(productId, 50, MovementType.Receipt)], TestReference, default);

        var movement = Assert.Single(movements);
        Assert.Equal(0m, movement.QuantityBefore);
        Assert.Equal(50m, movement.QuantityAfter);
        Assert.Equal(42, movement.CreatedBy);
        Assert.Equal("T-0001", movement.ReferenceNo);
        Assert.Equal((50m, 0m), await InventoryAsync(productId));
    }

    [Fact]
    public async Task Each_change_records_before_and_after_of_the_inventory_row()
    {
        var productId = await NewProductAsync();
        await using var context = fixture.Database.CreateContext();
        var ledger = Ledger(context);

        await ledger.ApplyAsync([Change(productId, 50, MovementType.Receipt)], TestReference, default);
        await ledger.ApplyAsync([Change(productId, -20, MovementType.Issue)], TestReference, default);
        await ledger.ApplyAsync([Change(productId, 5.5m, MovementType.AdjustmentIn)], TestReference, default);

        var movements = await MovementsAsync(productId);
        Assert.Equal([(0m, 50m), (50m, 30m), (30m, 35.5m)], movements.Select(m => (m.QuantityBefore, m.QuantityAfter)));
        Assert.Equal((35.5m, 0m), await InventoryAsync(productId));
    }

    [Fact]
    public async Task Issue_more_than_available_fails_and_writes_nothing()
    {
        var productId = await NewProductAsync();
        await using var context = fixture.Database.CreateContext();
        await Ledger(context).ApplyAsync([Change(productId, 10, MovementType.Receipt)], TestReference, default);

        await using var context2 = fixture.Database.CreateContext();
        var error = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            Ledger(context2).ApplyAsync([Change(productId, -11, MovementType.Issue)], TestReference, default));

        Assert.Contains("Insufficient stock", error.Message);
        Assert.Equal((10m, 0m), await InventoryAsync(productId));
        Assert.Single(await MovementsAsync(productId));
    }

    [Fact]
    public async Task Issue_cannot_take_reserved_quantity()
    {
        var productId = await NewProductAsync();
        await using (var context = fixture.Database.CreateContext())
        {
            await Ledger(context).ApplyAsync([Change(productId, 30, MovementType.Receipt)], TestReference, default);
            await context.Database.ExecuteSqlAsync($"UPDATE Inventories SET ReservedQuantity = 20 WHERE ProductId = {productId}");
        }

        await using var context2 = fixture.Database.CreateContext();
        var error = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            Ledger(context2).ApplyAsync([Change(productId, -15, MovementType.Issue)], TestReference, default));

        Assert.Contains("available 10", error.Message);
        Assert.Equal((30m, 20m), await InventoryAsync(productId));
    }

    [Fact]
    public async Task Multi_line_change_is_atomic_and_rolls_back_entirely()
    {
        var productId = await NewProductAsync();
        await using (var context = fixture.Database.CreateContext())
        {
            await Ledger(context).ApplyAsync([Change(productId, 10, MovementType.Receipt)], TestReference, default);
        }

        await using var context2 = fixture.Database.CreateContext();
        await Assert.ThrowsAsync<BusinessRuleException>(() => Ledger(context2).ApplyAsync(
            [
                Change(productId, 100, MovementType.Receipt, fixture.Bin2Id), // valid
                Change(productId, -999, MovementType.Issue)                  // invalid
            ],
            TestReference,
            default));

        Assert.Equal((10m, 0m), await InventoryAsync(productId));
        Assert.Null(await InventoryAsync(productId, fixture.Bin2Id));
        Assert.Single(await MovementsAsync(productId));
    }

    [Fact]
    public async Task Joins_caller_transaction_and_is_rolled_back_with_it()
    {
        var productId = await NewProductAsync();
        await using (var context = fixture.Database.CreateContext())
        {
            await using var transaction = await context.Database.BeginTransactionAsync();
            await Ledger(context).ApplyAsync([Change(productId, 10, MovementType.Receipt)], TestReference, default);
            await transaction.RollbackAsync();
        }

        Assert.Null(await InventoryAsync(productId));
        Assert.Empty(await MovementsAsync(productId));
    }

    [Theory]
    [InlineData(5, MovementType.Issue)]       // outbound must be negative
    [InlineData(-5, MovementType.Receipt)]    // inbound must be positive
    [InlineData(0, MovementType.Receipt)]
    [InlineData(1.2345, MovementType.Receipt)] // more than 3 decimals
    public async Task Invalid_quantity_or_direction_is_rejected(decimal quantity, MovementType type)
    {
        var productId = await NewProductAsync();
        await using var context = fixture.Database.CreateContext();

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            Ledger(context).ApplyAsync([Change(productId, quantity, type)], TestReference, default));
    }

    [Fact]
    public async Task Stock_can_only_be_placed_in_a_bin()
    {
        var productId = await NewProductAsync();
        await using var context = fixture.Database.CreateContext();

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            Ledger(context).ApplyAsync([Change(productId, 5, MovementType.Receipt, fixture.ShelfId)], TestReference, default));

        Assert.Contains("only be stored in a Bin", error.Message);
    }

    [Fact]
    public async Task Concurrent_issues_never_go_negative_and_keep_an_unbroken_before_after_chain()
    {
        var productId = await NewProductAsync();
        await using (var context = fixture.Database.CreateContext())
        {
            await Ledger(context).ApplyAsync([Change(productId, 100, MovementType.Receipt)], TestReference, default);
        }

        // 40 parallel issues of 5 against 100 in stock: exactly 20 can succeed.
        var results = await Task.WhenAll(Enumerable.Range(0, 40).Select(async _ =>
        {
            await using var context = fixture.Database.CreateContext();
            try
            {
                await Ledger(context).ApplyAsync([Change(productId, -5, MovementType.Issue)], TestReference, default);
                return true;
            }
            catch (BusinessRuleException)
            {
                return false;
            }
        }));

        Assert.Equal(20, results.Count(success => success));
        Assert.Equal((0m, 0m), await InventoryAsync(productId));

        var movements = await MovementsAsync(productId);
        Assert.Equal(21, movements.Count);
        for (var i = 1; i < movements.Count; i++)
        {
            Assert.Equal(movements[i - 1].QuantityAfter, movements[i].QuantityBefore);
        }

        Assert.Equal(0m, movements[^1].QuantityAfter);
    }

    [Fact]
    public async Task Movements_cannot_be_updated_or_deleted_through_ef()
    {
        var productId = await NewProductAsync();
        await using var context = fixture.Database.CreateContext();
        await Ledger(context).ApplyAsync([Change(productId, 10, MovementType.Receipt)], TestReference, default);

        var movement = await context.Set<StockMovement>().SingleAsync(m => m.ProductId == productId);
        movement.Quantity = 999;
        await Assert.ThrowsAsync<InvalidOperationException>(() => context.SaveChangesAsync());

        context.ChangeTracker.Clear();
        context.Remove(await context.Set<StockMovement>().SingleAsync(m => m.ProductId == productId));
        await Assert.ThrowsAsync<InvalidOperationException>(() => context.SaveChangesAsync());
    }

    [Theory]
    [InlineData("UPDATE StockMovements SET Note = 'tampered' WHERE ProductId = {0}")]
    [InlineData("DELETE FROM StockMovements WHERE ProductId = {0}")]
    public async Task Database_trigger_blocks_update_and_delete_from_raw_sql(string sql)
    {
        var productId = await NewProductAsync();
        await using var context = fixture.Database.CreateContext();
        await Ledger(context).ApplyAsync([Change(productId, 10, MovementType.Receipt)], TestReference, default);

        var error = await Assert.ThrowsAsync<SqlException>(() => context.Database.ExecuteSqlRawAsync(sql, productId));

        Assert.Equal(51000, error.Number);
        Assert.Single(await MovementsAsync(productId));
    }

    [Fact]
    public async Task Report_returns_opening_in_out_and_closing_for_the_period()
    {
        var productId = await NewProductAsync();
        var clock = new ManualTimeProvider(new DateTimeOffset(2026, 1, 10, 0, 0, 0, TimeSpan.Zero));
        await using var context = fixture.Database.CreateContext();
        var ledger = Ledger(context, clock);

        await ledger.ApplyAsync([Change(productId, 100, MovementType.Receipt)], TestReference, default); // before period
        await ledger.ApplyAsync([Change(productId, -30, MovementType.Issue)], TestReference, default);   // before period
        clock.Set(new DateTimeOffset(2026, 2, 5, 0, 0, 0, TimeSpan.Zero));
        await ledger.ApplyAsync([Change(productId, 40, MovementType.Receipt, fixture.Bin2Id)], TestReference, default);
        await ledger.ApplyAsync([Change(productId, -25, MovementType.Issue)], TestReference, default);
        clock.Set(new DateTimeOffset(2026, 3, 5, 0, 0, 0, TimeSpan.Zero));
        await ledger.ApplyAsync([Change(productId, -5, MovementType.Issue)], TestReference, default);     // after period

        var report = await new StockMovementRepository(context).GetStockReportAsync(new StockReportQuery
        {
            ProductId = productId,
            From = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc),
            To = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc)
        }, default);

        var row = Assert.Single(report.Items);
        Assert.Equal(70m, row.OpeningQuantity);   // 100 - 30
        Assert.Equal(40m, row.InQuantity);
        Assert.Equal(25m, row.OutQuantity);
        Assert.Equal(85m, row.ClosingQuantity);   // 70 + 40 - 25
    }

    [Fact]
    public async Task Ledger_sum_always_equals_inventory_quantity()
    {
        await using var context = fixture.Database.CreateContext();

        var mismatches = await context.Set<Inventory>()
            .Select(i => new
            {
                i.Id,
                i.Quantity,
                Ledger = context.Set<StockMovement>().Where(m => m.InventoryId == i.Id).Sum(m => (decimal?)m.Quantity) ?? 0m
            })
            .Where(x => x.Quantity != x.Ledger)
            .ToListAsync();

        Assert.Empty(mismatches);
    }
}
