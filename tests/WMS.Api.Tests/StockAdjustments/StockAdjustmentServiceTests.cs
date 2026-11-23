using FluentValidation.TestHelper;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using WMS.Api.Constants;
using WMS.Api.Data;
using WMS.Api.Dtos.StockAdjustments;
using WMS.Api.Exceptions;
using WMS.Api.Models;
using WMS.Api.Models.Documents;
using WMS.Api.Repositories;
using WMS.Api.Services.Audit;
using WMS.Api.Services.Documents;
using WMS.Api.Services.Inventories;
using WMS.Api.Services.StockAdjustments;
using WMS.Api.Tests.Infrastructure;
using WMS.Api.Validators;

namespace WMS.Api.Tests.StockAdjustments;

public sealed class StockAdjustmentFixture : IAsyncLifetime
{
    public SqlServerDatabaseFixture Database { get; } = new();

    public int WarehouseId { get; private set; }

    public int Bin1 { get; private set; }

    public int Bin2 { get; private set; }

    public async Task InitializeAsync()
    {
        await Database.InitializeAsync();
        await using var context = Database.CreateContext();
        var (warehouse, _, bins) = await WarehouseSeed.WarehouseAsync(context, "ADJ-WH", 2);
        WarehouseId = warehouse.Id;
        Bin1 = bins[0].Id;
        Bin2 = bins[1].Id;
    }

    public Task DisposeAsync() => Database.DisposeAsync();
}

public sealed class StockAdjustmentServiceTests(StockAdjustmentFixture fixture) : IClassFixture<StockAdjustmentFixture>
{
    private const int Clerk = 10;
    private const int Manager = 20;

    private sealed class Scope(WmsDbContext context, StockAdjustmentService service) : IAsyncDisposable
    {
        public StockAdjustmentService Service { get; } = service;

        public ValueTask DisposeAsync() => context.DisposeAsync();
    }

    private Scope As(int userId, bool requireApproval = true)
    {
        var context = fixture.Database.CreateContext();
        var user = new FixedCurrentUser(userId, Permissions.All.ToArray());
        var time = TimeProvider.System;
        var options = Options.Create(new WarehouseOptions { RequireAdjustmentApproval = requireApproval });
        return new Scope(context, new StockAdjustmentService(
            context,
            new StockAdjustmentRepository(context),
            new DocumentNumberGenerator(context, time),
            new DocumentWorkflow(user, time, options),
            new StockLedger(context, user, time),
            new AuditLogger(context, user, time),
            user));
    }

    private async Task<StockAdjustmentDetail> RunAsync(int userId, Func<StockAdjustmentService, Task<StockAdjustmentDetail>> action, bool requireApproval = true)
    {
        await using var scope = As(userId, requireApproval);
        return await action(scope.Service);
    }

    private async Task<int> ProductWithStockAsync(decimal quantity, decimal reserved = 0)
    {
        await using var context = fixture.Database.CreateContext();
        var product = await WarehouseSeed.ProductAsync(context, $"ADJ-{Guid.NewGuid():N}"[..20]);
        await new StockLedger(context, new FixedCurrentUser(1), TimeProvider.System).ApplyAsync(
            [new StockChange(product.Id, fixture.WarehouseId, fixture.Bin1, quantity, MovementType.Receipt)],
            new StockReference("TEST_SEED", null, null),
            default);
        if (reserved > 0)
        {
            await context.Database.ExecuteSqlAsync($"UPDATE Inventories SET ReservedQuantity = {reserved} WHERE ProductId = {product.Id}");
        }

        return product.Id;
    }

    private CreateStockAdjustmentRequest Request(params (int ProductId, int LocationId, decimal Quantity)[] lines) => new()
    {
        WarehouseId = fixture.WarehouseId,
        ReasonCode = AdjustmentReason.Damaged,
        Reason = "Broken during handling",
        Lines = lines.Select(line => new StockAdjustmentLineRequest
        {
            ProductId = line.ProductId, LocationId = line.LocationId, AdjustmentQuantity = line.Quantity
        }).ToList()
    };

    private async Task<int> SubmittedAsync(CreateStockAdjustmentRequest request)
    {
        var created = await RunAsync(Clerk, s => s.CreateAsync(request, default));
        await RunAsync(Clerk, s => s.SubmitAsync(created.Id, default));
        return created.Id;
    }

    private async Task<decimal?> StockAsync(int productId, int binId)
    {
        await using var context = fixture.Database.CreateContext();
        return await context.Set<Inventory>()
            .Where(i => i.ProductId == productId && i.LocationId == binId)
            .Select(i => (decimal?)i.Quantity)
            .SingleOrDefaultAsync();
    }

    [Fact]
    public async Task Approval_applies_stock_with_before_after_movements_and_audit()
    {
        var product = await ProductWithStockAsync(10);
        var id = await SubmittedAsync(Request((product, fixture.Bin1, -3), (product, fixture.Bin2, 5)));

        var pending = await RunAsync(Clerk, s => s.GetAsync(id, default));
        Assert.Equal(DocumentStatus.PendingApproval, pending.Status);
        Assert.True(pending.RequiresApproval);
        var preview = pending.Lines.Single(line => line.LocationId == fixture.Bin1);
        Assert.Equal((10m, 7m, (decimal?)null), (preview.CurrentQuantity, preview.ExpectedAfterQuantity, preview.BeforeQuantity));
        Assert.Equal(10m, await StockAsync(product, fixture.Bin1)); // nothing applied before approval

        var approved = await RunAsync(Manager, s => s.ApproveAsync(id, default));

        Assert.Equal(DocumentStatus.Completed, approved.Status);
        Assert.Matches(@"^ADJ-\d{4}-\d{6}$", approved.DocumentNo);
        var decrease = approved.Lines.Single(line => line.LocationId == fixture.Bin1);
        var increase = approved.Lines.Single(line => line.LocationId == fixture.Bin2);
        Assert.Equal((10m, -3m, 7m), (decrease.BeforeQuantity!.Value, decrease.AdjustmentQuantity, decrease.AfterQuantity!.Value));
        Assert.Equal((0m, 5m, 5m), (increase.BeforeQuantity!.Value, increase.AdjustmentQuantity, increase.AfterQuantity!.Value));
        Assert.Equal(7m, await StockAsync(product, fixture.Bin1));
        Assert.Equal(5m, await StockAsync(product, fixture.Bin2));

        await using var check = fixture.Database.CreateContext();
        var movements = await check.Set<StockMovement>()
            .Where(m => m.ReferenceType == "STOCK_ADJUSTMENT" && m.ReferenceId == id)
            .Select(m => new { m.MovementType, m.Quantity, m.CreatedBy, m.Note })
            .ToListAsync();
        Assert.Contains(movements, m => m.MovementType == MovementType.AdjustmentOut && m.Quantity == -3);
        Assert.Contains(movements, m => m.MovementType == MovementType.AdjustmentIn && m.Quantity == 5);
        Assert.All(movements, m => Assert.Equal(Manager, m.CreatedBy));
        Assert.All(movements, m => Assert.Contains("Broken during handling", m.Note));

        var audit = await check.Set<AuditLog>()
            .Where(log => log.EntityType == "StockAdjustment" && log.EntityId == id.ToString())
            .OrderBy(log => log.Id)
            .Select(log => new { log.Action, log.UserId, log.Data })
            .ToListAsync();
        Assert.Equal(["CREATED", "SUBMITTED", "APPROVED"], audit.Select(log => log.Action));
        Assert.Contains("\"status\":\"Completed\"", audit[2].Data);
        Assert.Contains("\"beforeQuantity\":10", audit[2].Data);
        Assert.Equal(Manager, audit[2].UserId);
    }

    [Fact]
    public async Task Without_approval_policy_submit_applies_immediately()
    {
        var product = await ProductWithStockAsync(10);
        var created = await RunAsync(Clerk, s => s.CreateAsync(Request((product, fixture.Bin1, -2)), default), requireApproval: false);

        var submitted = await RunAsync(Clerk, s => s.SubmitAsync(created.Id, default), requireApproval: false);

        Assert.Equal(DocumentStatus.Completed, submitted.Status);
        Assert.False(submitted.RequiresApproval);
        Assert.Equal(Clerk, submitted.Workflow.ApprovedBy);
        Assert.Equal(8m, await StockAsync(product, fixture.Bin1));
    }

    [Fact]
    public async Task Creator_cannot_approve_own_adjustment()
    {
        var product = await ProductWithStockAsync(10);
        var id = await SubmittedAsync(Request((product, fixture.Bin1, -1)));

        await Assert.ThrowsAsync<BusinessRuleException>(() => RunAsync(Clerk, s => s.ApproveAsync(id, default)));
        Assert.Equal(10m, await StockAsync(product, fixture.Bin1));
    }

    [Fact]
    public async Task Stock_can_never_become_negative()
    {
        var product = await ProductWithStockAsync(10);

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            RunAsync(Clerk, s => s.CreateAsync(Request((product, fixture.Bin1, -11)), default)));

        Assert.Contains("stock would become negative (10 -11)", error.Message);
    }

    [Fact]
    public async Task Stock_cannot_drop_below_reserved_quantity()
    {
        var product = await ProductWithStockAsync(10, reserved: 8);

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            RunAsync(Clerk, s => s.CreateAsync(Request((product, fixture.Bin1, -3)), default)));

        Assert.Contains("8 is reserved", error.Message);
    }

    [Fact]
    public async Task Approval_rechecks_current_stock_and_rolls_back_when_it_no_longer_fits()
    {
        var product = await ProductWithStockAsync(10);
        var id = await SubmittedAsync(Request((product, fixture.Bin1, -8), (product, fixture.Bin2, 4)));

        // Stock drops after submission (e.g. an issue was completed).
        await using (var context = fixture.Database.CreateContext())
        {
            await new StockLedger(context, new FixedCurrentUser(1), TimeProvider.System).ApplyAsync(
                [new StockChange(product, fixture.WarehouseId, fixture.Bin1, -5, MovementType.Issue)],
                new StockReference("TEST", null, null),
                default);
        }

        await Assert.ThrowsAsync<BusinessRuleException>(() => RunAsync(Manager, s => s.ApproveAsync(id, default)));

        Assert.Equal(5m, await StockAsync(product, fixture.Bin1));
        Assert.Null(await StockAsync(product, fixture.Bin2)); // the valid line was not applied either
        Assert.Equal(DocumentStatus.PendingApproval, (await RunAsync(Manager, s => s.GetAsync(id, default))).Status);
    }

    [Fact]
    public async Task Concurrent_approvals_apply_once()
    {
        var product = await ProductWithStockAsync(10);
        var id = await SubmittedAsync(Request((product, fixture.Bin1, -4)));

        var results = await Task.WhenAll(Enumerable.Range(0, 5).Select(async _ =>
        {
            try
            {
                await RunAsync(Manager, s => s.ApproveAsync(id, default));
                return true;
            }
            catch (Exception exception) when (exception is BusinessRuleException or ConflictException or DbUpdateException)
            {
                return false;
            }
        }));

        Assert.Equal(1, results.Count(ok => ok));
        Assert.Equal(6m, await StockAsync(product, fixture.Bin1));
    }

    [Fact]
    public async Task Completed_adjustment_cannot_be_changed_or_cancelled()
    {
        var product = await ProductWithStockAsync(10);
        var id = await SubmittedAsync(Request((product, fixture.Bin1, 1)));
        await RunAsync(Manager, s => s.ApproveAsync(id, default));

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            RunAsync(Clerk, s => s.UpdateAsync(id, new UpdateStockAdjustmentRequest
            {
                ReasonCode = AdjustmentReason.Other, Reason = "change it", Lines = [new StockAdjustmentLineRequest { ProductId = product, LocationId = fixture.Bin1, AdjustmentQuantity = 50 }]
            }, default)));
        await Assert.ThrowsAsync<BusinessRuleException>(() => RunAsync(Manager, s => s.CancelAsync(id, "undo", default)));
        Assert.Equal(11m, await StockAsync(product, fixture.Bin1));
    }

    [Fact]
    public async Task Duplicate_lines_are_rejected()
    {
        var product = await ProductWithStockAsync(10);

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            RunAsync(Clerk, s => s.CreateAsync(Request((product, fixture.Bin1, 1), (product, fixture.Bin1, 2)), default)));

        Assert.Contains("Lines 1, 2 repeat", error.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("abc")]
    public void Reason_is_mandatory(string reason)
    {
        var result = new CreateStockAdjustmentRequestValidator().TestValidate(new CreateStockAdjustmentRequest
        {
            WarehouseId = 1,
            ReasonCode = AdjustmentReason.Lost,
            Reason = reason,
            Lines = [new StockAdjustmentLineRequest { ProductId = 1, LocationId = 1, AdjustmentQuantity = 1 }]
        });

        result.ShouldHaveValidationErrorFor(request => request.Reason);
    }

    [Fact]
    public void Zero_adjustment_is_invalid()
    {
        var result = new StockAdjustmentLineRequestValidator().TestValidate(new StockAdjustmentLineRequest
        {
            ProductId = 1, LocationId = 1, AdjustmentQuantity = 0
        });

        result.ShouldHaveValidationErrorFor(line => line.AdjustmentQuantity);
    }

    [Fact]
    public async Task Database_rejects_inconsistent_before_after()
    {
        var product = await ProductWithStockAsync(10);
        var id = await SubmittedAsync(Request((product, fixture.Bin1, 1)));
        await RunAsync(Manager, s => s.ApproveAsync(id, default));

        await using var context = fixture.Database.CreateContext();
        var error = await Assert.ThrowsAsync<SqlException>(() => context.Database.ExecuteSqlAsync(
            $"UPDATE StockAdjustmentLines SET AfterQuantity = 999 WHERE StockAdjustmentId = {id}"));

        Assert.Contains("CK_StockAdjustmentLines_Balance", error.Message);
    }
}
