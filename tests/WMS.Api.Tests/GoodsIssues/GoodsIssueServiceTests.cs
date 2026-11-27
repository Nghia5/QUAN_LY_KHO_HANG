using FluentValidation.TestHelper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using WMS.Api.Constants;
using WMS.Api.Data;
using WMS.Api.Dtos.GoodsIssues;
using WMS.Api.Exceptions;
using WMS.Api.Models;
using WMS.Api.Models.Documents;
using WMS.Api.Repositories;
using WMS.Api.Services.Audit;
using WMS.Api.Services.Documents;
using WMS.Api.Services.GoodsIssues;
using WMS.Api.Services.Inventories;
using WMS.Api.Tests.Infrastructure;
using WMS.Api.Validators;

namespace WMS.Api.Tests.GoodsIssues;

public sealed class GoodsIssueFixture : IAsyncLifetime
{
    public SqlServerDatabaseFixture Database { get; } = new();

    public int WarehouseId { get; private set; }

    public int Bin1Id { get; private set; }

    public int Bin2Id { get; private set; }

    public async Task InitializeAsync()
    {
        await Database.InitializeAsync();
        await using var context = Database.CreateContext();
        var (warehouse, _, bins) = await WarehouseSeed.WarehouseAsync(context, "GI-WH", 2);
        WarehouseId = warehouse.Id;
        Bin1Id = bins[0].Id;
        Bin2Id = bins[1].Id;
    }

    public Task DisposeAsync() => Database.DisposeAsync();
}

public sealed class GoodsIssueServiceTests(GoodsIssueFixture fixture) : IClassFixture<GoodsIssueFixture>
{
    private const int Clerk = 10;
    private const int Manager = 20;

    private sealed class Scope(WmsDbContext context, GoodsIssueService service) : IAsyncDisposable
    {
        public WmsDbContext Context { get; } = context;

        public GoodsIssueService Service { get; } = service;

        public ValueTask DisposeAsync() => Context.DisposeAsync();
    }

    private Scope As(int userId, params string[] permissions)
    {
        var context = fixture.Database.CreateContext();
        var user = new FixedCurrentUser(userId, permissions.Length > 0 ? permissions : Permissions.All.ToArray());
        var time = TimeProvider.System;
        var service = new GoodsIssueService(
            context,
            new GoodsIssueRepository(context),
            new DocumentNumberGenerator(context, time),
            new DocumentWorkflow(user, time, Options.Create(new WarehouseOptions())),
            new StockLedger(context, user, time),
            new StockReservationService(context),
            new AuditLogger(context, user, time),
            user,
            time);
        return new Scope(context, service);
    }

    /// <summary>New product with opening stock posted through the ledger (keeps the ledger invariant).</summary>
    private async Task<int> ProductWithStockAsync(decimal quantity, int? locationId = null)
    {
        await using var context = fixture.Database.CreateContext();
        var product = await WarehouseSeed.ProductAsync(context, $"GI-{Guid.NewGuid():N}"[..20]);
        await new StockLedger(context, new FixedCurrentUser(1), TimeProvider.System).ApplyAsync(
            [new StockChange(product.Id, fixture.WarehouseId, locationId ?? fixture.Bin1Id, quantity, MovementType.Receipt)],
            new StockReference("TEST_SEED", null, null),
            default);
        return product.Id;
    }

    private CreateGoodsIssueRequest Request(params GoodsIssueLineRequest[] lines) => new()
    {
        WarehouseId = fixture.WarehouseId,
        CustomerName = "Customer A",
        ReferenceNo = "SO-1",
        Lines = lines
    };

    private GoodsIssueLineRequest Line(int productId, decimal quantity, int? locationId = null) => new()
    {
        ProductId = productId,
        LocationId = locationId ?? fixture.Bin1Id,
        Quantity = quantity
    };

    private async Task<int> SubmittedIssueAsync(params GoodsIssueLineRequest[] lines)
    {
        await using var clerk = As(Clerk);
        var id = (await clerk.Service.CreateAsync(Request(lines), default)).Id;
        await clerk.Service.SubmitAsync(id, default);
        return id;
    }

    private async Task<int> ApprovedIssueAsync(params GoodsIssueLineRequest[] lines)
    {
        var id = await SubmittedIssueAsync(lines);
        await using var manager = As(Manager);
        await manager.Service.ApproveAsync(id, default);
        return id;
    }

    private async Task<(decimal Quantity, decimal Reserved, decimal Available)> StockAsync(int productId, int? locationId = null)
    {
        await using var context = fixture.Database.CreateContext();
        var location = locationId ?? fixture.Bin1Id;
        var row = await context.Set<Inventory>()
            .Where(i => i.ProductId == productId && i.LocationId == location)
            .Select(i => new { i.Quantity, i.ReservedQuantity, i.AvailableQuantity })
            .SingleAsync();
        return (row.Quantity, row.ReservedQuantity, row.AvailableQuantity);
    }

    private async Task<List<StockMovement>> IssueMovementsAsync(int productId)
    {
        await using var context = fixture.Database.CreateContext();
        return await context.Set<StockMovement>()
            .Where(m => m.ProductId == productId && m.MovementType == MovementType.Issue)
            .OrderBy(m => m.Id)
            .ToListAsync();
    }

    private async Task<bool> TryAsync(Func<GoodsIssueService, Task> action)
    {
        await using var scope = As(Manager);
        try
        {
            await action(scope.Service);
            return true;
        }
        catch (Exception exception) when (exception is BusinessRuleException or ConflictException or DbUpdateException)
        {
            return false;
        }
    }

    [Fact]
    public async Task Approve_reserves_and_complete_decreases_stock_with_movement_and_audit()
    {
        var product = await ProductWithStockAsync(100);
        var id = await SubmittedIssueAsync(Line(product, 30));
        Assert.Equal((100m, 0m, 100m), await StockAsync(product)); // nothing held before approval

        await using (var manager = As(Manager))
        {
            var approved = await manager.Service.ApproveAsync(id, default);
            Assert.Equal(DocumentStatus.Approved, approved.Status);
            Assert.Equal(70m, approved.Lines[0].AvailableQuantity);
        }

        Assert.Equal((100m, 30m, 70m), await StockAsync(product));

        await using (var manager = As(Manager))
        {
            var completed = await manager.Service.CompleteAsync(id, default);
            Assert.Equal(DocumentStatus.Completed, completed.Status);
            Assert.Matches(@"^GI-\d{4}-\d{6}$", completed.DocumentNo);
        }

        Assert.Equal((70m, 0m, 70m), await StockAsync(product));
        var movement = Assert.Single(await IssueMovementsAsync(product));
        Assert.Equal((-30m, 100m, 70m), (movement.Quantity, movement.QuantityBefore, movement.QuantityAfter));
        Assert.Equal("GOODS_ISSUE", movement.ReferenceType);

        await using var check = fixture.Database.CreateContext();
        var actions = await check.Set<AuditLog>()
            .Where(log => log.EntityType == "GoodsIssue" && log.EntityId == id.ToString())
            .OrderBy(log => log.Id).Select(log => log.Action).ToListAsync();
        Assert.Equal(["CREATED", "SUBMITTED", "APPROVED", "COMPLETED"], actions);
    }

    [Fact]
    public async Task Cannot_create_issue_above_available_quantity()
    {
        var product = await ProductWithStockAsync(10);
        await using var clerk = As(Clerk);

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            clerk.Service.CreateAsync(Request(Line(product, 10.001m)), default));

        Assert.Contains("exceeds available stock 10", error.Message);
    }

    [Fact]
    public async Task Reserved_stock_is_not_available_to_other_issues()
    {
        var product = await ProductWithStockAsync(50);
        await ApprovedIssueAsync(Line(product, 40));

        await using var clerk = As(Clerk);
        var error = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            clerk.Service.CreateAsync(Request(Line(product, 20)), default));

        Assert.Contains("exceeds available stock 10", error.Message);
    }

    [Fact]
    public async Task Approval_fails_atomically_when_stock_was_taken_in_between()
    {
        var p1 = await ProductWithStockAsync(20);
        var p2 = await ProductWithStockAsync(20);
        var late = await SubmittedIssueAsync(Line(p1, 5), Line(p2, 15)); // valid when submitted
        await ApprovedIssueAsync(Line(p2, 10));                          // takes p2 stock first

        await using (var manager = As(Manager))
        {
            var error = await Assert.ThrowsAsync<BusinessRuleException>(() => manager.Service.ApproveAsync(late, default));
            Assert.Contains("available 10, requested 15", error.Message);
        }

        // All or nothing: the line that could be reserved (p1) was rolled back too.
        Assert.Equal((20m, 0m, 20m), await StockAsync(p1));
        Assert.Equal((20m, 10m, 10m), await StockAsync(p2));
        await using var check = As(Manager);
        Assert.Equal(DocumentStatus.PendingApproval, (await check.Service.GetAsync(late, default)).Status);
    }

    [Fact]
    public async Task Concurrent_approvals_never_reserve_more_than_stock()
    {
        var product = await ProductWithStockAsync(100);
        var ids = new List<int>();
        for (var i = 0; i < 10; i++)
        {
            ids.Add(await SubmittedIssueAsync(Line(product, 15))); // 10 x 15 = 150 > 100
        }

        var approved = await Task.WhenAll(ids.Select(id => TryAsync(service => service.ApproveAsync(id, default))));

        Assert.Equal(6, approved.Count(ok => ok)); // 6 x 15 = 90 <= 100 < 105
        Assert.Equal((100m, 90m, 10m), await StockAsync(product));

        // Then complete every approved issue in parallel: stock never goes negative.
        var approvedIds = ids.Where((_, index) => approved[index]).ToList();
        var completed = await Task.WhenAll(approvedIds.Select(id => TryAsync(service => service.CompleteAsync(id, default))));

        Assert.All(completed, Assert.True);
        Assert.Equal((10m, 0m, 10m), await StockAsync(product));

        var movements = await IssueMovementsAsync(product);
        Assert.Equal(6, movements.Count);
        Assert.All(movements, movement => Assert.True(movement.QuantityAfter >= 0));
        for (var i = 1; i < movements.Count; i++)
        {
            Assert.Equal(movements[i - 1].QuantityAfter, movements[i].QuantityBefore);
        }
    }

    [Fact]
    public async Task Same_issue_approved_twice_concurrently_reserves_once()
    {
        var product = await ProductWithStockAsync(100);
        var id = await SubmittedIssueAsync(Line(product, 25));

        var results = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => TryAsync(service => service.ApproveAsync(id, default))));

        Assert.Equal(1, results.Count(ok => ok));
        Assert.Equal((100m, 25m, 75m), await StockAsync(product));
    }

    [Fact]
    public async Task Same_issue_completed_twice_concurrently_issues_once()
    {
        var product = await ProductWithStockAsync(100);
        var id = await ApprovedIssueAsync(Line(product, 25));

        var results = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => TryAsync(service => service.CompleteAsync(id, default))));

        Assert.Equal(1, results.Count(ok => ok));
        Assert.Equal((75m, 0m, 75m), await StockAsync(product));
        Assert.Single(await IssueMovementsAsync(product));
    }

    [Fact]
    public async Task Cancelling_an_approved_issue_releases_the_reservation()
    {
        var product = await ProductWithStockAsync(40);
        var id = await ApprovedIssueAsync(Line(product, 15));
        Assert.Equal((40m, 15m, 25m), await StockAsync(product));

        await using var manager = As(Manager);
        var cancelled = await manager.Service.CancelAsync(id, "Customer cancelled", default);

        Assert.Equal(DocumentStatus.Cancelled, cancelled.Status);
        Assert.Equal((40m, 0m, 40m), await StockAsync(product));
        Assert.Empty(await IssueMovementsAsync(product));
    }

    [Fact]
    public async Task Cancelling_an_approved_issue_requires_approve_permission()
    {
        var product = await ProductWithStockAsync(10);
        var id = await ApprovedIssueAsync(Line(product, 5));

        await using var clerk = As(Clerk, Permissions.GoodsIssueCreate);
        await Assert.ThrowsAsync<ForbiddenException>(() => clerk.Service.CancelAsync(id, "x", default));
        Assert.Equal((10m, 5m, 5m), await StockAsync(product));
    }

    [Fact]
    public async Task Creator_cannot_approve_own_issue_and_invalid_transitions_fail()
    {
        var product = await ProductWithStockAsync(10);
        await using var clerk = As(Clerk);
        var issue = await clerk.Service.CreateAsync(Request(Line(product, 1)), default);

        await Assert.ThrowsAsync<BusinessRuleException>(() => clerk.Service.CompleteAsync(issue.Id, default));
        await clerk.Service.SubmitAsync(issue.Id, default);
        await Assert.ThrowsAsync<BusinessRuleException>(() => clerk.Service.ApproveAsync(issue.Id, default));
        Assert.Equal((10m, 0m, 10m), await StockAsync(product));
    }

    [Fact]
    public async Task Issue_from_a_location_without_stock_is_rejected()
    {
        var product = await ProductWithStockAsync(10, fixture.Bin1Id);
        await using var clerk = As(Clerk);

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            clerk.Service.CreateAsync(Request(Line(product, 1, fixture.Bin2Id)), default));

        Assert.Contains("exceeds available stock 0", error.Message);
    }

    [Fact]
    public async Task Duplicate_lines_are_rejected()
    {
        var product = await ProductWithStockAsync(10);
        await using var clerk = As(Clerk);

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            clerk.Service.CreateAsync(Request(Line(product, 1), Line(product, 2)), default));

        Assert.Contains("Lines 1, 2 repeat", error.Message);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void Quantity_must_be_positive(decimal quantity)
    {
        var result = new GoodsIssueLineRequestValidator().TestValidate(new GoodsIssueLineRequest
        {
            ProductId = 1, LocationId = 1, Quantity = quantity
        });

        result.ShouldHaveValidationErrorFor(line => line.Quantity);
    }

    [Fact]
    public async Task Reserved_quantity_always_matches_open_approved_issues()
    {
        await using var context = fixture.Database.CreateContext();

        var reservedByIssues = await context.Set<GoodsIssue>()
            .Where(issue => issue.Status == DocumentStatus.Approved)
            .SelectMany(issue => issue.Lines)
            .GroupBy(line => new { line.ProductId, line.LocationId })
            .Select(group => new { group.Key.ProductId, group.Key.LocationId, Quantity = group.Sum(line => line.Quantity) })
            .ToListAsync();

        foreach (var reserved in reservedByIssues)
        {
            var inventory = await context.Set<Inventory>()
                .SingleAsync(i => i.ProductId == reserved.ProductId && i.LocationId == reserved.LocationId);
            Assert.Equal(reserved.Quantity, inventory.ReservedQuantity);
        }
    }
}
