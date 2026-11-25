using Microsoft.EntityFrameworkCore;
using WMS.Api.Data;
using WMS.Api.Exceptions;
using WMS.Api.Models;

namespace WMS.Api.Services.Inventories;

public sealed record StockReservationLine(int ProductId, int WarehouseId, int LocationId, decimal Quantity);

/// <summary>
/// Holds / releases stock for approved documents that are not completed yet (spec 4.1, 4.3).
/// Only ReservedQuantity changes, so no stock movement is written (the document is the audit).
/// Must run inside the caller's transaction so a failing line rolls back the whole document.
/// </summary>
public interface IStockReservationService
{
    /// <summary>Atomically reserves every line or throws 422 (nothing reserved).</summary>
    Task ReserveAsync(IReadOnlyCollection<StockReservationLine> lines, CancellationToken cancellationToken);

    Task ReleaseAsync(IReadOnlyCollection<StockReservationLine> lines, CancellationToken cancellationToken);
}

public sealed class StockReservationService(WmsDbContext dbContext) : IStockReservationService
{
    public async Task ReserveAsync(IReadOnlyCollection<StockReservationLine> lines, CancellationToken cancellationToken)
    {
        EnsureTransaction();
        var shortages = new List<string>();

        foreach (var line in Merge(lines))
        {
            // Conditional update: two concurrent reservations can never exceed the available quantity.
            var affected = await dbContext.Database.ExecuteSqlAsync($"""
                UPDATE [Inventories]
                SET [ReservedQuantity] = [ReservedQuantity] + {line.Quantity}, [UpdatedAt] = SYSUTCDATETIME()
                WHERE [ProductId] = {line.ProductId}
                  AND [WarehouseId] = {line.WarehouseId}
                  AND [LocationId] = {line.LocationId}
                  AND [Quantity] - [ReservedQuantity] >= {line.Quantity}
                """, cancellationToken);

            if (affected == 0)
            {
                var available = await dbContext.Set<Inventory>()
                    .Where(i => i.ProductId == line.ProductId && i.WarehouseId == line.WarehouseId && i.LocationId == line.LocationId)
                    .Select(i => (decimal?)i.AvailableQuantity)
                    .SingleOrDefaultAsync(cancellationToken) ?? 0m;
                shortages.Add(
                    $"product {line.ProductId} at location {line.LocationId}: available {available:0.###}, requested {line.Quantity:0.###}");
            }
        }

        if (shortages.Count > 0)
        {
            throw new BusinessRuleException($"Insufficient available stock for {string.Join("; ", shortages)}.");
        }
    }

    public async Task ReleaseAsync(IReadOnlyCollection<StockReservationLine> lines, CancellationToken cancellationToken)
    {
        EnsureTransaction();

        foreach (var line in Merge(lines))
        {
            var affected = await dbContext.Database.ExecuteSqlAsync($"""
                UPDATE [Inventories]
                SET [ReservedQuantity] = [ReservedQuantity] - {line.Quantity}, [UpdatedAt] = SYSUTCDATETIME()
                WHERE [ProductId] = {line.ProductId}
                  AND [WarehouseId] = {line.WarehouseId}
                  AND [LocationId] = {line.LocationId}
                  AND [ReservedQuantity] >= {line.Quantity}
                """, cancellationToken);

            if (affected == 0)
            {
                throw new ConflictException(
                    $"Reservation for product {line.ProductId} at location {line.LocationId} is missing; nothing was released.");
            }
        }
    }

    /// <summary>One statement per inventory row, in a fixed order (avoids deadlocks).</summary>
    private static IEnumerable<StockReservationLine> Merge(IEnumerable<StockReservationLine> lines) =>
        lines.GroupBy(line => (line.ProductId, line.WarehouseId, line.LocationId))
            .OrderBy(group => group.Key.ProductId).ThenBy(group => group.Key.WarehouseId).ThenBy(group => group.Key.LocationId)
            .Select(group => new StockReservationLine(
                group.Key.ProductId, group.Key.WarehouseId, group.Key.LocationId, group.Sum(line => line.Quantity)));

    private void EnsureTransaction()
    {
        if (dbContext.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("Stock reservations must run inside the document's transaction.");
        }
    }
}
