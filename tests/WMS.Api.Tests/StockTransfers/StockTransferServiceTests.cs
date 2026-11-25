using FluentValidation.TestHelper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using WMS.Api.Constants;
using WMS.Api.Data;
using WMS.Api.Dtos.StockTransfers;
using WMS.Api.Exceptions;
using WMS.Api.Models;
using WMS.Api.Models.Documents;
using WMS.Api.Repositories;
using WMS.Api.Services.Audit;
using WMS.Api.Services.Documents;
using WMS.Api.Services.Inventories;
using WMS.Api.Services.StockTransfers;
using WMS.Api.Tests.Infrastructure;
using WMS.Api.Validators;

namespace WMS.Api.Tests.StockTransfers;

public sealed class StockTransferFixture : IAsyncLifetime
{
    public SqlServerDatabaseFixture Database { get; } = new();

    public int SourceId { get; private set; }

    public int DestinationId { get; private set; }

    public int A1 { get; private set; }

    public int A2 { get; private set; }

    public int B1 { get; private set; }

    public async Task InitializeAsync()
    {
        await Database.InitializeAsync();
        await using var context = Database.CreateContext();
        var (source, _, sourceBins) = await WarehouseSeed.WarehouseAsync(context, "TR-SRC", 2);
        var (destination, _, destinationBins) = await WarehouseSeed.WarehouseAsync(context, "TR-DST", 1);
        SourceId = source.Id;
        DestinationId = destination.Id;
        A1 = sourceBins[0].Id;
        A2 = sourceBins[1].Id;
        B1 = destinationBins[0].Id;
    }

    public Task DisposeAsync() => Database.DisposeAsync();
}

public sealed class StockTransferServiceTests(StockTransferFixture fixture) : IClassFixture<StockTransferFixture>
{
    private const int Clerk = 10;
    private const int Manager = 20;

    private sealed class Scope(WmsDbContext context, StockTransferService service) : IAsyncDisposable
    {
        public StockTransferService Service { get; } = service;

        public ValueTask DisposeAsync() => context.DisposeAsync();
    }

    private Scope As(int userId)
    {
        var context = fixture.Database.CreateContext();
        var user = new FixedCurrentUser(userId, Permissions.All.ToArray());
        var time = TimeProvider.System;
        return new Scope(context, new StockTransferService(
            context,
            new StockTransferRepository(context),
            new DocumentNumberGenerator(context, time),
            new DocumentWorkflow(user, time, Options.Create(new WarehouseOptions())),
            new StockLedger(context, user, time),
            new StockReservationService(context),
            new AuditLogger(context, user, time),
            user));
    }

    private async Task<int> ProductWithStockAsync(decimal quantity)
    {
        await using var context = fixture.Database.CreateContext();
        var product = await WarehouseSeed.ProductAsync(context, $"TR-{Guid.NewGuid():N}"[..20]);
        await new StockLedger(context, new FixedCurrentUser(1), TimeProvider.System).ApplyAsync(
            [new StockChange(product.Id, fixture.SourceId, fixture.A1, quantity, MovementType.Receipt)],
            new StockReference("TEST_SEED", null, null),
            default);
        return product.Id;
    }

    private CreateStockTransferRequest Request(int productId, decimal quantity, int? destinationWarehouseId = null, int? toLocationId = null) => new()
    {
        WarehouseId = fixture.SourceId,
        DestinationWarehouseId = destinationWarehouseId ?? fixture.DestinationId,
        Reason = "Rebalance",
        Lines = [new StockTransferLineRequest
        {
            ProductId = productId,
            FromLocationId = fixture.A1,
            ToLocationId = toLocationId ?? fixture.B1,
            Quantity = quantity
        }]
    };

    private async Task<int> SubmittedAsync(CreateStockTransferRequest request)
    {
        await using var clerk = As(Clerk);
        var id = (await clerk.Service.CreateAsync(request, default)).Id;
        await clerk.Service.SubmitAsync(id, default);
        return id;
    }

    private async Task<int> ApprovedAsync(CreateStockTransferRequest request)
    {
        var id = await SubmittedAsync(request);
        await using var manager = As(Manager);
        await manager.Service.ApproveAsync(id, default);
        return id;
    }

    private async Task<StockTransferDetail> RunAsync(int userId, Func<StockTransferService, Task<StockTransferDetail>> action)
    {
        await using var scope = As(userId);
        return await action(scope.Service);
    }

    private async Task<(decimal Quantity, decimal Reserved)?> StockAsync(int productId, int locationId)
    {
        await using var context = fixture.Database.CreateContext();
        var row = await context.Set<Inventory>()
            .Where(i => i.ProductId == productId && i.LocationId == locationId)
            .Select(i => new { i.Quantity, i.ReservedQuantity })
            .SingleOrDefaultAsync();
        return row is null ? null : (row.Quantity, row.ReservedQuantity);
    }

    private async Task<List<StockMovement>> TransferMovementsAsync(int transferId)
    {
        await using var context = fixture.Database.CreateContext();
        return await context.Set<StockMovement>()
            .Where(m => m.ReferenceType == "STOCK_TRANSFER" && m.ReferenceId == transferId)
            .OrderBy(m => m.Id)
            .ToListAsync();
    }

    [Fact]
    public async Task Full_flow_between_warehouses_moves_stock_step_by_step()
    {
        var product = await ProductWithStockAsync(50);
        var id = await ApprovedAsync(Request(product, 20));
        Assert.Equal((50m, 20m), await StockAsync(product, fixture.A1)); // reserved at source

        var picked = await RunAsync(Clerk, s => s.PickAsync(id, default));
        Assert.Equal(DocumentStatus.Picked, picked.Status);
        Assert.Equal(Clerk, picked.Progress.PickedBy);
        Assert.Equal((50m, 20m), await StockAsync(product, fixture.A1));

        var shipped = await RunAsync(Clerk, s => s.ShipAsync(id, default));
        Assert.Equal(DocumentStatus.InTransit, shipped.Status);
        Assert.Equal((30m, 0m), await StockAsync(product, fixture.A1)); // left the source
        Assert.Null(await StockAsync(product, fixture.B1));             // not arrived yet

        var received = await RunAsync(Clerk, s => s.ReceiveAsync(id, default));
        Assert.Equal(DocumentStatus.Received, received.Status);
        Assert.Equal((20m, 0m), await StockAsync(product, fixture.B1));

        var completed = await RunAsync(Manager, s => s.CompleteAsync(id, default));
        Assert.Equal(DocumentStatus.Completed, completed.Status);
        Assert.Matches(@"^TR-\d{4}-\d{6}$", completed.DocumentNo);
        Assert.False(completed.IsInternal);

        var movements = await TransferMovementsAsync(id);
        Assert.Collection(movements,
            m => Assert.Equal((MovementType.TransferOut, -20m, 50m, 30m, fixture.SourceId), (m.MovementType, m.Quantity, m.QuantityBefore, m.QuantityAfter, m.WarehouseId)),
            m => Assert.Equal((MovementType.TransferIn, 20m, 0m, 20m, fixture.DestinationId), (m.MovementType, m.Quantity, m.QuantityBefore, m.QuantityAfter, m.WarehouseId)));

        await using var check = fixture.Database.CreateContext();
        var actions = await check.Set<AuditLog>()
            .Where(log => log.EntityType == "StockTransfer" && log.EntityId == id.ToString())
            .OrderBy(log => log.Id).Select(log => log.Action).ToListAsync();
        Assert.Equal(["CREATED", "SUBMITTED", "APPROVED", "PICKED", "SHIPPED", "RECEIVED", "COMPLETED"], actions);
    }

    [Fact]
    public async Task Internal_bin_to_bin_transfer_keeps_warehouse_total()
    {
        var product = await ProductWithStockAsync(10);
        var id = await ApprovedAsync(Request(product, 4, fixture.SourceId, fixture.A2));

        await RunAsync(Clerk, s => s.PickAsync(id, default));
        await RunAsync(Clerk, s => s.ShipAsync(id, default));
        var received = await RunAsync(Clerk, s => s.ReceiveAsync(id, default));

        Assert.True(received.IsInternal);
        Assert.Equal((6m, 0m), await StockAsync(product, fixture.A1));
        Assert.Equal((4m, 0m), await StockAsync(product, fixture.A2));
    }

    [Fact]
    public async Task Only_valid_state_transitions_are_allowed()
    {
        var product = await ProductWithStockAsync(10);
        var id = await SubmittedAsync(Request(product, 2));

        await Assert.ThrowsAsync<BusinessRuleException>(() => RunAsync(Clerk, s => s.PickAsync(id, default)));    // not approved
        await RunAsync(Manager, s => s.ApproveAsync(id, default));
        await Assert.ThrowsAsync<BusinessRuleException>(() => RunAsync(Clerk, s => s.ShipAsync(id, default)));    // not picked
        await Assert.ThrowsAsync<BusinessRuleException>(() => RunAsync(Clerk, s => s.ReceiveAsync(id, default))); // not shipped
        await Assert.ThrowsAsync<BusinessRuleException>(() => RunAsync(Manager, s => s.CompleteAsync(id, default))); // not received

        await RunAsync(Clerk, s => s.PickAsync(id, default));
        await RunAsync(Clerk, s => s.ShipAsync(id, default));
        await Assert.ThrowsAsync<BusinessRuleException>(() => RunAsync(Manager, s => s.CancelAsync(id, "too late", default))); // in transit
        await Assert.ThrowsAsync<BusinessRuleException>(() => RunAsync(Clerk, s => s.ShipAsync(id, default)));    // twice

        Assert.Equal((8m, 0m), await StockAsync(product, fixture.A1));
    }

    [Fact]
    public async Task Cannot_transfer_more_than_available_at_source()
    {
        var product = await ProductWithStockAsync(5);
        await using var clerk = As(Clerk);

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => clerk.Service.CreateAsync(Request(product, 6), default));

        Assert.Contains("exceeds available stock 5 at the source", error.Message);
    }

    [Fact]
    public async Task Concurrent_approvals_never_reserve_more_than_source_stock()
    {
        var product = await ProductWithStockAsync(30);
        var ids = new List<int>();
        for (var i = 0; i < 5; i++)
        {
            ids.Add(await SubmittedAsync(Request(product, 10)));
        }

        var results = await Task.WhenAll(ids.Select(async id =>
        {
            try
            {
                await RunAsync(Manager, s => s.ApproveAsync(id, default));
                return true;
            }
            catch (Exception exception) when (exception is BusinessRuleException or ConflictException)
            {
                return false;
            }
        }));

        Assert.Equal(3, results.Count(ok => ok));
        Assert.Equal((30m, 30m), await StockAsync(product, fixture.A1));
    }

    [Fact]
    public async Task Concurrent_shipping_of_the_same_transfer_posts_once()
    {
        var product = await ProductWithStockAsync(10);
        var id = await ApprovedAsync(Request(product, 3));
        await RunAsync(Clerk, s => s.PickAsync(id, default));

        var results = await Task.WhenAll(Enumerable.Range(0, 5).Select(async _ =>
        {
            try
            {
                await RunAsync(Clerk, s => s.ShipAsync(id, default));
                return true;
            }
            catch (Exception exception) when (exception is BusinessRuleException or ConflictException or DbUpdateException)
            {
                return false;
            }
        }));

        Assert.Equal(1, results.Count(ok => ok));
        Assert.Equal((7m, 0m), await StockAsync(product, fixture.A1));
        Assert.Single(await TransferMovementsAsync(id));
    }

    [Fact]
    public async Task Cancelling_a_picked_transfer_releases_the_reservation()
    {
        var product = await ProductWithStockAsync(10);
        var id = await ApprovedAsync(Request(product, 6));
        await RunAsync(Clerk, s => s.PickAsync(id, default));

        var cancelled = await RunAsync(Manager, s => s.CancelAsync(id, "Truck unavailable", default));

        Assert.Equal(DocumentStatus.Cancelled, cancelled.Status);
        Assert.Equal((10m, 0m), await StockAsync(product, fixture.A1));
        Assert.Empty(await TransferMovementsAsync(id));
    }

    [Fact]
    public async Task Destination_bin_deactivated_in_transit_blocks_receive_until_fixed()
    {
        await using var setup = fixture.Database.CreateContext();
        var (warehouse, _, bins) = await WarehouseSeed.WarehouseAsync(setup, $"TRD{Random.Shared.Next(1000, 9999)}", 1);
        var product = await ProductWithStockAsync(10);
        var id = await ApprovedAsync(Request(product, 5, warehouse.Id, bins[0].Id));
        await RunAsync(Clerk, s => s.PickAsync(id, default));
        await RunAsync(Clerk, s => s.ShipAsync(id, default));

        await setup.Database.ExecuteSqlAsync($"UPDATE WarehouseLocations SET IsActive = 0 WHERE Id = {bins[0].Id}");
        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => RunAsync(Clerk, s => s.ReceiveAsync(id, default)));
        Assert.Contains("inactive", error.Message);
        Assert.Null(await StockAsync(product, bins[0].Id));
        Assert.Equal(DocumentStatus.InTransit, (await RunAsync(Clerk, s => s.GetAsync(id, default))).Status);

        await setup.Database.ExecuteSqlAsync($"UPDATE WarehouseLocations SET IsActive = 1 WHERE Id = {bins[0].Id}");
        await RunAsync(Clerk, s => s.ReceiveAsync(id, default));
        Assert.Equal((5m, 0m), await StockAsync(product, bins[0].Id));
    }

    [Fact]
    public async Task Locations_must_belong_to_the_right_warehouse()
    {
        var product = await ProductWithStockAsync(10);
        await using var clerk = As(Clerk);

        // Destination bin of the source warehouse used for the destination warehouse.
        var error = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            clerk.Service.CreateAsync(Request(product, 1, fixture.DestinationId, fixture.A2), default));

        Assert.Contains("destination location", error.Message);
    }

    [Fact]
    public void Same_source_and_destination_bin_is_invalid()
    {
        var result = new StockTransferLineRequestValidator().TestValidate(new StockTransferLineRequest
        {
            ProductId = 1, FromLocationId = 5, ToLocationId = 5, Quantity = 1
        });

        result.ShouldHaveValidationErrorFor(line => line.ToLocationId);
    }
}
