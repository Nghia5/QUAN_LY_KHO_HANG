using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using WMS.Api.Constants;
using WMS.Api.Data;
using WMS.Api.Dtos.Stocktakes;
using WMS.Api.Exceptions;
using WMS.Api.Models;
using WMS.Api.Models.Documents;
using WMS.Api.Repositories;
using WMS.Api.Services.Audit;
using WMS.Api.Services.Documents;
using WMS.Api.Services.Inventories;
using WMS.Api.Services.Stocktakes;
using WMS.Api.Tests.Infrastructure;

namespace WMS.Api.Tests.Stocktakes;

public sealed class StocktakeFixture : IAsyncLifetime
{
    public SqlServerDatabaseFixture Database { get; } = new();

    public Task InitializeAsync() => Database.InitializeAsync();

    public Task DisposeAsync() => Database.DisposeAsync();
}

public sealed class StocktakeServiceTests(StocktakeFixture fixture) : IClassFixture<StocktakeFixture>
{
    private const int Counter = 10;
    private const int Manager = 20;

    private sealed class Scope(WmsDbContext context, StocktakeService service) : IAsyncDisposable
    {
        public StocktakeService Service { get; } = service;

        public ValueTask DisposeAsync() => context.DisposeAsync();
    }

    /// <summary>Isolated warehouse (3 bins) per test, so locks of one test never affect another.</summary>
    private sealed record Site(int WarehouseId, int ShelfId, int B1, int B2, int B3);

    private Scope As(int userId)
    {
        var context = fixture.Database.CreateContext();
        var user = new FixedCurrentUser(userId, Permissions.All.ToArray());
        var time = TimeProvider.System;
        return new Scope(context, new StocktakeService(
            context,
            new StocktakeRepository(context),
            new DocumentNumberGenerator(context, time),
            new DocumentWorkflow(user, time, Options.Create(new WarehouseOptions())),
            new StockLedger(context, user, time),
            new AuditLogger(context, user, time),
            user));
    }

    private async Task<StocktakeDetail> RunAsync(int userId, Func<StocktakeService, Task<StocktakeDetail>> action)
    {
        await using var scope = As(userId);
        return await action(scope.Service);
    }

    private async Task<Site> SiteAsync()
    {
        await using var context = fixture.Database.CreateContext();
        var (warehouse, shelf, bins) = await WarehouseSeed.WarehouseAsync(context, $"ST{Guid.NewGuid():N}"[..12], 3);
        return new Site(warehouse.Id, shelf.Id, bins[0].Id, bins[1].Id, bins[2].Id);
    }

    private async Task<int> ProductAsync()
    {
        await using var context = fixture.Database.CreateContext();
        return (await WarehouseSeed.ProductAsync(context, $"STP-{Guid.NewGuid():N}"[..20])).Id;
    }

    private async Task ReceiveAsync(Site site, int productId, int binId, decimal quantity)
    {
        await using var context = fixture.Database.CreateContext();
        await new StockLedger(context, new FixedCurrentUser(1), TimeProvider.System).ApplyAsync(
            [new StockChange(productId, site.WarehouseId, binId, quantity, MovementType.Receipt)],
            new StockReference("TEST_SEED", null, null),
            default);
    }

    private async Task<decimal?> StockAsync(int productId, int binId)
    {
        await using var context = fixture.Database.CreateContext();
        return await context.Set<Inventory>()
            .Where(i => i.ProductId == productId && i.LocationId == binId)
            .Select(i => (decimal?)i.Quantity)
            .SingleOrDefaultAsync();
    }

    private static StocktakeCountRequest Counts(params (int ProductId, int LocationId, decimal Counted)[] items) => new()
    {
        Items = items.Select(item => new StocktakeCountItem
        {
            ProductId = item.ProductId, LocationId = item.LocationId, CountedQuantity = item.Counted
        }).ToList()
    };

    private async Task<int> SnapshotTakenAsync(Site site, params int[] scope)
    {
        var created = await RunAsync(Counter, s => s.CreateAsync(new CreateStocktakeRequest { WarehouseId = site.WarehouseId, ScopeLocationIds = scope }, default));
        await RunAsync(Counter, s => s.SnapshotAsync(created.Id, default));
        return created.Id;
    }

    [Fact]
    public async Task Full_workflow_snapshot_count_compare_approve_adjust_complete()
    {
        var site = await SiteAsync();
        var p1 = await ProductAsync();
        var p2 = await ProductAsync();
        var found = await ProductAsync();
        await ReceiveAsync(site, p1, site.B1, 10);
        await ReceiveAsync(site, p2, site.B2, 5);

        var created = await RunAsync(Counter, s => s.CreateAsync(new CreateStocktakeRequest { WarehouseId = site.WarehouseId }, default));
        Assert.Matches(@"^ST-\d{4}-\d{6}$", created.DocumentNo);
        Assert.Empty(created.Lines);

        var snapshot = await RunAsync(Counter, s => s.SnapshotAsync(created.Id, default));
        Assert.Equal(DocumentStatus.InProgress, snapshot.Status);
        Assert.Equal([10m, 5m], snapshot.Lines.OrderBy(l => l.LocationId).Select(l => l.SystemQuantity));
        Assert.All(snapshot.Lines, line => Assert.Null(line.CountedQuantity));

        await RunAsync(Counter, s => s.CountAsync(created.Id, Counts((p1, site.B1, 8), (p2, site.B2, 5), (found, site.B3, 2)), default));

        var compare = await RunAsync(Counter, s => s.GetDifferencesAsync(created.Id, default));
        Assert.Equal(2, compare.Lines.Count);                     // p2 matches, so it is not listed
        Assert.Equal(-2m, compare.Lines.Single(l => l.ProductId == p1).Difference);
        var foundLine = compare.Lines.Single(l => l.ProductId == found);
        Assert.Equal((0m, 2m, 2m, true), (foundLine.SystemQuantity, foundLine.CountedQuantity!.Value, foundLine.Difference!.Value, foundLine.IsAddedDuringCount));
        Assert.Equal((3, 3, 0, 2, 2m, 2m), (compare.Totals.LineCount, compare.Totals.CountedLines, compare.Totals.UncountedLines,
            compare.Totals.LinesWithDifference, compare.Totals.TotalSurplus, compare.Totals.TotalShortage));

        await RunAsync(Counter, s => s.SubmitAsync(created.Id, default));
        await RunAsync(Manager, s => s.ApproveAsync(created.Id, default));
        Assert.Equal(10m, await StockAsync(p1, site.B1)); // nothing posted before ADJUSTMENT

        var adjusted = await RunAsync(Manager, s => s.AdjustAsync(created.Id, default));
        Assert.Equal(DocumentStatus.Adjusted, adjusted.Status);
        Assert.Equal(8m, await StockAsync(p1, site.B1));
        Assert.Equal(5m, await StockAsync(p2, site.B2));
        Assert.Equal(2m, await StockAsync(found, site.B3));

        var completed = await RunAsync(Manager, s => s.CompleteAsync(created.Id, default));
        Assert.Equal(DocumentStatus.Completed, completed.Status);

        await using var check = fixture.Database.CreateContext();
        var movements = await check.Set<StockMovement>()
            .Where(m => m.ReferenceType == "STOCKTAKE" && m.ReferenceId == created.Id)
            .OrderBy(m => m.ProductId)
            .Select(m => new { m.ProductId, m.MovementType, m.Quantity, m.QuantityBefore, m.QuantityAfter })
            .ToListAsync();
        Assert.Equal(2, movements.Count);
        Assert.Contains(movements, m => m.ProductId == p1 && m.MovementType == MovementType.StocktakeOut && m.Quantity == -2 && m.QuantityBefore == 10 && m.QuantityAfter == 8);
        Assert.Contains(movements, m => m.ProductId == found && m.MovementType == MovementType.StocktakeIn && m.Quantity == 2 && m.QuantityAfter == 2);

        var actions = await check.Set<AuditLog>()
            .Where(log => log.EntityType == "Stocktake" && log.EntityId == created.Id.ToString())
            .OrderBy(log => log.Id).Select(log => log.Action).ToListAsync();
        Assert.Equal(["CREATED", "SNAPSHOT", "COUNTED", "SUBMITTED", "APPROVED", "ADJUSTED", "COMPLETED"], actions);
    }

    [Fact]
    public async Task Difference_is_exact_with_decimals()
    {
        var site = await SiteAsync();
        var product = await ProductAsync();
        await ReceiveAsync(site, product, site.B1, 10);
        var id = await SnapshotTakenAsync(site);

        var counted = await RunAsync(Counter, s => s.CountAsync(id, Counts((product, site.B1, 9.875m)), default));

        Assert.Equal(-0.125m, counted.Lines.Single().Difference);
    }

    [Fact]
    public async Task Snapshot_locks_counted_locations_until_adjusted()
    {
        var site = await SiteAsync();
        var product = await ProductAsync();
        await ReceiveAsync(site, product, site.B1, 10);
        await ReceiveAsync(site, product, site.B2, 10);
        var id = await SnapshotTakenAsync(site, site.B1); // only B1 in scope

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => ReceiveAsync(site, product, site.B1, 1));
        Assert.Contains("is being counted", error.Message);
        await ReceiveAsync(site, product, site.B2, 1); // outside the scope: allowed
        Assert.Equal(10m, await StockAsync(product, site.B1));

        await RunAsync(Counter, s => s.CountAsync(id, Counts((product, site.B1, 10)), default));
        await RunAsync(Counter, s => s.SubmitAsync(id, default));
        await RunAsync(Manager, s => s.ApproveAsync(id, default));
        await Assert.ThrowsAsync<BusinessRuleException>(() => ReceiveAsync(site, product, site.B1, 1)); // still locked
        await RunAsync(Manager, s => s.AdjustAsync(id, default));

        await ReceiveAsync(site, product, site.B1, 1); // unlocked after adjustment
        Assert.Equal(11m, await StockAsync(product, site.B1));
    }

    [Fact]
    public async Task Cancelling_unlocks_the_locations_without_changing_stock()
    {
        var site = await SiteAsync();
        var product = await ProductAsync();
        await ReceiveAsync(site, product, site.B1, 10);
        var id = await SnapshotTakenAsync(site);
        await RunAsync(Counter, s => s.CountAsync(id, Counts((product, site.B1, 3)), default));

        await RunAsync(Counter, s => s.CancelAsync(id, "Wrong scope", default));

        Assert.Equal(10m, await StockAsync(product, site.B1));
        await ReceiveAsync(site, product, site.B1, 1);
        Assert.Equal(11m, await StockAsync(product, site.B1));
    }

    [Fact]
    public async Task Concurrent_stock_changes_during_snapshot_never_make_it_stale()
    {
        var site = await SiteAsync();
        var product = await ProductAsync();
        await ReceiveAsync(site, product, site.B1, 100);
        var created = await RunAsync(Counter, s => s.CreateAsync(new CreateStocktakeRequest { WarehouseId = site.WarehouseId }, default));

        var receipts = Enumerable.Range(0, 10).Select(async _ =>
        {
            try
            {
                await ReceiveAsync(site, product, site.B1, 1);
            }
            catch (BusinessRuleException)
            {
                // rejected because the location got locked: expected for receipts after the snapshot
            }
        });
        await Task.WhenAll(receipts.Append(RunAsync(Counter, s => s.SnapshotAsync(created.Id, default))));

        var snapshot = await RunAsync(Counter, s => s.GetAsync(created.Id, default));
        Assert.Equal(await StockAsync(product, site.B1), snapshot.Lines.Single().SystemQuantity);
    }

    [Fact]
    public async Task System_quantity_is_never_overwritten_and_counts_are_audited()
    {
        var site = await SiteAsync();
        var product = await ProductAsync();
        await ReceiveAsync(site, product, site.B1, 10);
        var id = await SnapshotTakenAsync(site);

        await RunAsync(Counter, s => s.CountAsync(id, Counts((product, site.B1, 7)), default));
        await RunAsync(Counter, s => s.SubmitAsync(id, default));
        var rejected = await RunAsync(Manager, s => s.RejectAsync(id, "Recount bin B1", default));
        Assert.Equal(DocumentStatus.InProgress, rejected.Status); // back to counting
        Assert.Equal("Recount bin B1", rejected.Workflow.RejectionReason);

        var recounted = await RunAsync(Counter, s => s.CountAsync(id, Counts((product, site.B1, 9)), default));
        Assert.Equal((10m, 9m), (recounted.Lines.Single().SystemQuantity, recounted.Lines.Single().CountedQuantity!.Value));

        await using var check = fixture.Database.CreateContext();
        var countLogs = await check.Set<AuditLog>()
            .Where(log => log.EntityType == "Stocktake" && log.EntityId == id.ToString() && log.Action == "COUNTED")
            .OrderBy(log => log.Id).Select(log => log.Data!).ToListAsync();
        Assert.Equal(2, countLogs.Count);
        Assert.Contains("\"previous\":7", countLogs[1]); // history of the first count is kept
        Assert.Contains("\"counted\":9", countLogs[1]);
    }

    [Fact]
    public async Task Submit_requires_every_line_counted()
    {
        var site = await SiteAsync();
        var p1 = await ProductAsync();
        var p2 = await ProductAsync();
        await ReceiveAsync(site, p1, site.B1, 1);
        await ReceiveAsync(site, p2, site.B2, 1);
        var id = await SnapshotTakenAsync(site);
        await RunAsync(Counter, s => s.CountAsync(id, Counts((p1, site.B1, 1)), default));

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => RunAsync(Counter, s => s.SubmitAsync(id, default)));
        Assert.Contains("1 line(s) are not counted", error.Message);
    }

    [Fact]
    public async Task Counter_cannot_approve_own_stocktake_and_counts_are_frozen_after_approval()
    {
        var site = await SiteAsync();
        var product = await ProductAsync();
        await ReceiveAsync(site, product, site.B1, 4);
        var id = await SnapshotTakenAsync(site);
        await RunAsync(Counter, s => s.CountAsync(id, Counts((product, site.B1, 4)), default));
        await RunAsync(Counter, s => s.SubmitAsync(id, default));

        await Assert.ThrowsAsync<BusinessRuleException>(() => RunAsync(Counter, s => s.ApproveAsync(id, default)));
        await RunAsync(Manager, s => s.ApproveAsync(id, default));
        await Assert.ThrowsAsync<BusinessRuleException>(() => RunAsync(Counter, s => s.CountAsync(id, Counts((product, site.B1, 1)), default)));
    }

    [Fact]
    public async Task Overlapping_snapshots_are_rejected()
    {
        var site = await SiteAsync();
        var product = await ProductAsync();
        await ReceiveAsync(site, product, site.B1, 1);
        await SnapshotTakenAsync(site, site.B1);

        var second = await RunAsync(Counter, s => s.CreateAsync(new CreateStocktakeRequest { WarehouseId = site.WarehouseId }, default));
        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => RunAsync(Counter, s => s.SnapshotAsync(second.Id, default)));

        Assert.Contains("already being counted", error.Message);
    }

    [Fact]
    public async Task Counting_outside_the_scope_is_rejected()
    {
        var site = await SiteAsync();
        var product = await ProductAsync();
        var id = await SnapshotTakenAsync(site, site.B1);

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            RunAsync(Counter, s => s.CountAsync(id, Counts((product, site.B2, 1)), default)));

        Assert.Contains("outside the stocktake scope", error.Message);
    }

    [Fact]
    public async Task Scope_by_shelf_includes_all_its_bins()
    {
        var site = await SiteAsync();
        var product = await ProductAsync();
        await ReceiveAsync(site, product, site.B1, 1);
        await ReceiveAsync(site, product, site.B3, 1);

        var id = await SnapshotTakenAsync(site, site.ShelfId);

        var detail = await RunAsync(Counter, s => s.GetAsync(id, default));
        Assert.Equal([site.B1, site.B3], detail.Lines.Select(line => line.LocationId).Order());
    }

    [Fact]
    public async Task Adjusting_twice_concurrently_posts_once()
    {
        var site = await SiteAsync();
        var product = await ProductAsync();
        await ReceiveAsync(site, product, site.B1, 10);
        var id = await SnapshotTakenAsync(site);
        await RunAsync(Counter, s => s.CountAsync(id, Counts((product, site.B1, 6)), default));
        await RunAsync(Counter, s => s.SubmitAsync(id, default));
        await RunAsync(Manager, s => s.ApproveAsync(id, default));

        var results = await Task.WhenAll(Enumerable.Range(0, 4).Select(async _ =>
        {
            try
            {
                await RunAsync(Manager, s => s.AdjustAsync(id, default));
                return true;
            }
            catch (Exception exception) when (exception is BusinessRuleException or ConflictException or DbUpdateException)
            {
                return false;
            }
        }));

        Assert.Equal(1, results.Count(ok => ok));
        Assert.Equal(6m, await StockAsync(product, site.B1));
    }

    [Fact]
    public async Task Counted_below_reserved_quantity_cannot_be_adjusted_and_rolls_back()
    {
        var site = await SiteAsync();
        var product = await ProductAsync();
        await ReceiveAsync(site, product, site.B1, 10);
        await using (var context = fixture.Database.CreateContext())
        {
            await context.Database.ExecuteSqlAsync($"UPDATE Inventories SET ReservedQuantity = 8 WHERE ProductId = {product}");
        }

        var id = await SnapshotTakenAsync(site);
        await RunAsync(Counter, s => s.CountAsync(id, Counts((product, site.B1, 5)), default));
        await RunAsync(Counter, s => s.SubmitAsync(id, default));
        await RunAsync(Manager, s => s.ApproveAsync(id, default));

        await Assert.ThrowsAsync<BusinessRuleException>(() => RunAsync(Manager, s => s.AdjustAsync(id, default)));

        Assert.Equal(10m, await StockAsync(product, site.B1));
        Assert.Equal(DocumentStatus.Approved, (await RunAsync(Manager, s => s.GetAsync(id, default))).Status);
    }
}
