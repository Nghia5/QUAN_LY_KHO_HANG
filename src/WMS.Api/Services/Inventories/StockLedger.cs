using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using WMS.Api.Common;
using WMS.Api.Data;
using WMS.Api.Exceptions;
using WMS.Api.Models;
using WMS.Api.Models.Documents;
using WMS.Api.Services.Documents;

namespace WMS.Api.Services.Inventories;

/// <summary>One quantity change of one inventory row (Product + Warehouse + Location).</summary>
/// <param name="Quantity">Signed: positive for inbound types, negative for outbound types.</param>
/// <param name="ReservedChange">
/// Signed change of ReservedQuantity applied in the same statement, e.g. completing an approved
/// issue consumes its reservation: Quantity = -5, ReservedChange = -5.
/// </param>
/// <param name="BatchId">
/// Lot affected. Inbound: the quantity is added to this lot. Outbound: taken from this lot.
/// Null outbound = automatic FEFO allocation over the row's lots, then stock without lot
/// (expired lots are skipped for <see cref="MovementType.Issue"/>). Null inbound = stock without lot.
/// </param>
public sealed record StockChange(
    int ProductId,
    int WarehouseId,
    int LocationId,
    decimal Quantity,
    MovementType MovementType,
    string? Note = null,
    decimal ReservedChange = 0,
    int? BatchId = null);

/// <summary>A change and the movements it produced (one per lot touched).</summary>
public sealed record AppliedStockChange(StockChange Change, IReadOnlyList<StockMovement> Movements);

/// <summary>Source document of a set of changes, e.g. ("GOODS_RECEIPT", 12, "GR-2026-0001").</summary>
public sealed record StockReference(string ReferenceType, int? ReferenceId, string? ReferenceNo);

/// <summary>
/// The only component allowed to change <see cref="Inventory.Quantity"/>. Every change is applied
/// with an atomic conditional UPDATE (no negative stock, never below ReservedQuantity) and recorded
/// as a <see cref="StockMovement"/> whose Before/After come from the same locked row, inside one
/// transaction: if any change fails, all changes and movements are rolled back.
/// </summary>
public interface IStockLedger
{
    /// <summary>
    /// Applies the changes. Joins the caller's transaction when one is active (documents should
    /// open one to also save their own status), otherwise opens and commits its own.
    /// </summary>
    Task<IReadOnlyList<StockMovement>> ApplyAsync(
        IReadOnlyCollection<StockChange> changes,
        StockReference reference,
        CancellationToken cancellationToken);

    /// <summary>Same as <see cref="ApplyAsync"/>, but returns the movements of each change (lot allocation).</summary>
    Task<IReadOnlyList<AppliedStockChange>> ApplyDetailedAsync(
        IReadOnlyCollection<StockChange> changes,
        StockReference reference,
        CancellationToken cancellationToken);
}

public sealed class StockLedger(
    WmsDbContext dbContext,
    ICurrentUser currentUser,
    TimeProvider timeProvider) : IStockLedger
{
    public async Task<IReadOnlyList<StockMovement>> ApplyAsync(
        IReadOnlyCollection<StockChange> changes,
        StockReference reference,
        CancellationToken cancellationToken) =>
        (await ApplyDetailedAsync(changes, reference, cancellationToken)).SelectMany(applied => applied.Movements).ToList();

    public async Task<IReadOnlyList<AppliedStockChange>> ApplyDetailedAsync(
        IReadOnlyCollection<StockChange> changes,
        StockReference reference,
        CancellationToken cancellationToken)
    {
        if (changes.Count == 0)
        {
            throw new BusinessRuleException("At least one stock change is required.");
        }

        foreach (var change in changes)
        {
            Validate(change);
        }

        var ownsTransaction = dbContext.Database.CurrentTransaction is null;
        await using var transaction = ownsTransaction
            ? await dbContext.Database.BeginTransactionAsync(cancellationToken)
            : null;

        await EnsureBatchesMatchProductsAsync(changes, cancellationToken);

        var results = new List<AppliedStockChange>(changes.Count);
        var userId = currentUser.UserId;
        var now = timeProvider.GetUtcNow().UtcDateTime;

        // Fixed lock order across all documents to avoid deadlocks between concurrent documents.
        foreach (var change in changes.OrderBy(c => c.ProductId).ThenBy(c => c.WarehouseId).ThenBy(c => c.LocationId))
        {
            var row = await ApplyToInventoryAsync(change, cancellationToken);
            var allocations = await ApplyToLotsAsync(change, row, cancellationToken);

            // One movement per lot touched; Before/After chain through the locked row.
            var running = row.QuantityBefore;
            var movements = allocations.Select(allocation =>
            {
                var movement = new StockMovement
                {
                    InventoryId = row.Id,
                    ProductId = change.ProductId,
                    WarehouseId = change.WarehouseId,
                    LocationId = change.LocationId,
                    BatchId = allocation.BatchId,
                    MovementType = change.MovementType,
                    Quantity = allocation.Quantity,
                    QuantityBefore = running,
                    QuantityAfter = running + allocation.Quantity,
                    ReferenceType = reference.ReferenceType,
                    ReferenceId = reference.ReferenceId,
                    ReferenceNo = reference.ReferenceNo,
                    Note = change.Note,
                    CreatedBy = userId,
                    CreatedAt = now
                };
                running += allocation.Quantity;
                return movement;
            }).ToList();

            results.Add(new AppliedStockChange(change, movements));
        }

        // Checked AFTER the updates: if a stocktake snapshot is being taken concurrently, our UPDATE
        // waited for it, so its lines are visible now and this change is rolled back.
        await EnsureNotBeingCountedAsync(changes, reference, cancellationToken);

        // Inserted one by one so identity order = ledger order (the Before/After chain relies on it).
        foreach (var movement in results.SelectMany(result => result.Movements))
        {
            dbContext.Set<StockMovement>().Add(movement);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }

        return results;
    }

    /// <summary>
    /// Applies the change to the lots of the (already updated and locked) inventory row and returns
    /// the signed quantity per lot (null = stock without lot). The sum always equals change.Quantity.
    /// </summary>
    private async Task<List<(int? BatchId, decimal Quantity)>> ApplyToLotsAsync(
        StockChange change,
        InventoryRow row,
        CancellationToken cancellationToken)
    {
        if (change.BatchId is { } batchId)
        {
            if (change.Quantity > 0)
            {
                var updated = await dbContext.Database.ExecuteSqlAsync($"""
                    UPDATE [InventoryLots] SET [Quantity] = [Quantity] + {change.Quantity}, [UpdatedAt] = SYSUTCDATETIME()
                    WHERE [InventoryId] = {row.Id} AND [BatchId] = {batchId}
                    """, cancellationToken);
                if (updated == 0)
                {
                    await dbContext.Database.ExecuteSqlAsync($"""
                        INSERT INTO [InventoryLots] ([InventoryId], [BatchId], [ProductId], [Quantity], [UpdatedAt])
                        VALUES ({row.Id}, {batchId}, {change.ProductId}, {change.Quantity}, SYSUTCDATETIME())
                        """, cancellationToken);
                }
            }
            else
            {
                var taken = await dbContext.Database.ExecuteSqlAsync($"""
                    UPDATE [InventoryLots] SET [Quantity] = [Quantity] + {change.Quantity}, [UpdatedAt] = SYSUTCDATETIME()
                    WHERE [InventoryId] = {row.Id} AND [BatchId] = {batchId} AND [Quantity] + {change.Quantity} >= 0
                    """, cancellationToken);
                if (taken == 0)
                {
                    var inLot = await dbContext.Set<InventoryLot>()
                        .Where(lot => lot.InventoryId == row.Id && lot.BatchId == batchId)
                        .Select(lot => (decimal?)lot.Quantity)
                        .SingleOrDefaultAsync(cancellationToken) ?? 0m;
                    throw new BusinessRuleException(
                        $"Batch {batchId} has only {inLot:0.###} at location {change.LocationId}; requested {-change.Quantity:0.###}.");
                }
            }

            return [(batchId, change.Quantity)];
        }

        if (change.Quantity > 0)
        {
            return [(null, change.Quantity)];
        }

        // Outbound without a lot: FEFO over the lots of this row, then the stock without lot.
        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        var lots = await dbContext.Set<InventoryLot>()
            .Where(lot => lot.InventoryId == row.Id && lot.Quantity > 0)
            .Select(lot => new { lot.Id, lot.BatchId, lot.Quantity, lot.Batch!.ExpiryDate })
            .ToListAsync(cancellationToken);

        var untracked = row.QuantityBefore - lots.Sum(lot => lot.Quantity);
        var skipExpired = change.MovementType == MovementType.Issue;
        var candidates = lots
            .Where(lot => !skipExpired || lot.ExpiryDate is null || lot.ExpiryDate >= today)
            .OrderBy(lot => lot.ExpiryDate is null)
            .ThenBy(lot => lot.ExpiryDate)
            .ThenBy(lot => lot.BatchId);

        var remaining = -change.Quantity;
        var allocations = new List<(int? BatchId, decimal Quantity)>();
        foreach (var lot in candidates)
        {
            if (remaining == 0)
            {
                break;
            }

            var take = Math.Min(lot.Quantity, remaining);
            await dbContext.Database.ExecuteSqlAsync($"""
                UPDATE [InventoryLots] SET [Quantity] = [Quantity] - {take}, [UpdatedAt] = SYSUTCDATETIME()
                WHERE [Id] = {lot.Id} AND [Quantity] >= {take}
                """, cancellationToken);
            allocations.Add((lot.BatchId, -take));
            remaining -= take;
        }

        if (remaining > 0 && untracked > 0)
        {
            var take = Math.Min(untracked, remaining);
            allocations.Add((null, -take));
            remaining -= take;
        }

        if (remaining > 0)
        {
            var expired = lots.Where(lot => lot.ExpiryDate < today).Sum(lot => lot.Quantity);
            throw new BusinessRuleException(
                $"Product {change.ProductId} at location {change.LocationId}: {expired:0.###} of the stock is expired " +
                $"and cannot be issued; requested {-change.Quantity:0.###}.");
        }

        return allocations;
    }

    /// <summary>A lot can only be used for its own product.</summary>
    private async Task EnsureBatchesMatchProductsAsync(IReadOnlyCollection<StockChange> changes, CancellationToken cancellationToken)
    {
        var batchIds = changes.Where(change => change.BatchId is not null).Select(change => change.BatchId!.Value).Distinct().ToList();
        if (batchIds.Count == 0)
        {
            return;
        }

        var products = await dbContext.Set<Batch>()
            .Where(batch => batchIds.Contains(batch.Id))
            .ToDictionaryAsync(batch => batch.Id, batch => batch.ProductId, cancellationToken);

        var wrong = changes.FirstOrDefault(change =>
            change.BatchId is { } id && (!products.TryGetValue(id, out var productId) || productId != change.ProductId));
        if (wrong is not null)
        {
            throw new BusinessRuleException($"Batch {wrong.BatchId} does not exist for product {wrong.ProductId}.");
        }
    }

    /// <summary>Statuses in which a stocktake freezes the stock of its counted locations (spec 5.3).</summary>
    public static readonly DocumentStatus[] StocktakeLockStatuses =
        [DocumentStatus.InProgress, DocumentStatus.PendingApproval, DocumentStatus.Approved];

    private async Task EnsureNotBeingCountedAsync(
        IReadOnlyCollection<StockChange> changes,
        StockReference reference,
        CancellationToken cancellationToken)
    {
        var locationIds = changes.Select(change => change.LocationId).Distinct().ToList();
        int? ownStocktakeId = reference.ReferenceType == DocumentTypes.Stocktake ? reference.ReferenceId : null;

        var lockedBy = await dbContext.Set<StocktakeLine>()
            .Where(line => locationIds.Contains(line.LocationId))
            .Join(dbContext.Set<Stocktake>(), line => line.StocktakeId, stocktake => stocktake.Id,
                (line, stocktake) => new { stocktake.Id, stocktake.DocumentNo, stocktake.Status, LocationCode = line.Location!.Code })
            .Where(x => StocktakeLockStatuses.Contains(x.Status) && x.Id != ownStocktakeId)
            .FirstOrDefaultAsync(cancellationToken);

        if (lockedBy is not null)
        {
            throw new BusinessRuleException(
                $"Location '{lockedBy.LocationCode}' is being counted by stocktake {lockedBy.DocumentNo}; " +
                "stock there cannot change until the stocktake is adjusted or cancelled.");
        }
    }

    private static void Validate(StockChange change)
    {
        if (change.Quantity == 0)
        {
            throw new BusinessRuleException("Stock change quantity must not be 0.");
        }

        if (decimal.Round(change.Quantity, 3) != change.Quantity)
        {
            throw new BusinessRuleException("Stock quantity supports at most 3 decimal places.");
        }

        if (change.MovementType.IsInbound() != change.Quantity > 0)
        {
            throw new BusinessRuleException(
                $"{change.MovementType} requires a {(change.MovementType.IsInbound() ? "positive" : "negative")} quantity.");
        }
    }

    private async Task<InventoryRow> ApplyToInventoryAsync(StockChange change, CancellationToken cancellationToken)
    {
        if (await TryUpdateAsync(change, cancellationToken) is { } updated)
        {
            return updated;
        }

        var existing = await dbContext.Set<Inventory>()
            .AsNoTracking()
            .Where(inventory => inventory.ProductId == change.ProductId
                                && inventory.WarehouseId == change.WarehouseId
                                && inventory.LocationId == change.LocationId)
            .Select(inventory => new { inventory.Quantity, inventory.ReservedQuantity, inventory.AvailableQuantity })
            .SingleOrDefaultAsync(cancellationToken);

        if (existing is not null && change.ReservedChange < 0 && existing.ReservedQuantity < -change.ReservedChange)
        {
            throw new ConflictException(
                $"Reservation for product {change.ProductId} at location {change.LocationId} is missing " +
                $"(reserved {existing.ReservedQuantity:0.###}, expected at least {-change.ReservedChange:0.###}).");
        }

        if (existing is not null)
        {
            throw new BusinessRuleException(
                $"Insufficient stock for product {change.ProductId} at location {change.LocationId}: " +
                $"available {existing.AvailableQuantity:0.###} (on hand {existing.Quantity:0.###}, reserved {existing.ReservedQuantity:0.###}), " +
                $"requested {-change.Quantity:0.###}.");
        }

        if (change.Quantity < 0 || change.ReservedChange != 0)
        {
            throw new BusinessRuleException(
                $"Product {change.ProductId} has no stock at location {change.LocationId} of warehouse {change.WarehouseId}.");
        }

        await EnsureLocationCanHoldStockAsync(change, cancellationToken);

        try
        {
            return await InsertAsync(change, cancellationToken);
        }
        catch (SqlException exception) when (exception.Number is 2601 or 2627)
        {
            // Another transaction created the row in between: it is now locked/visible, update it instead.
            return await TryUpdateAsync(change, cancellationToken)
                   ?? throw new ConflictException("Inventory changed concurrently, please retry.");
        }
    }

    private async Task<InventoryRow?> TryUpdateAsync(StockChange change, CancellationToken cancellationToken)
    {
        var rows = await dbContext.Database.SqlQuery<InventoryRow>($"""
            UPDATE [Inventories]
            SET [Quantity] = [Quantity] + {change.Quantity},
                [ReservedQuantity] = [ReservedQuantity] + {change.ReservedChange},
                [UpdatedAt] = SYSUTCDATETIME()
            OUTPUT inserted.[Id] AS [Id], deleted.[Quantity] AS [QuantityBefore], inserted.[Quantity] AS [QuantityAfter]
            WHERE [ProductId] = {change.ProductId}
              AND [WarehouseId] = {change.WarehouseId}
              AND [LocationId] = {change.LocationId}
              AND [ReservedQuantity] + {change.ReservedChange} >= 0
              AND [Quantity] + {change.Quantity} >= [ReservedQuantity] + {change.ReservedChange}
            """).ToListAsync(cancellationToken);

        return rows.SingleOrDefault();
    }

    private async Task<InventoryRow> InsertAsync(StockChange change, CancellationToken cancellationToken)
    {
        var rows = await dbContext.Database.SqlQuery<InventoryRow>($"""
            INSERT INTO [Inventories] ([ProductId], [WarehouseId], [LocationId], [Quantity], [ReservedQuantity], [MinQuantity], [CreatedAt], [UpdatedAt])
            OUTPUT inserted.[Id] AS [Id], CAST(0 AS decimal(18,3)) AS [QuantityBefore], inserted.[Quantity] AS [QuantityAfter]
            VALUES ({change.ProductId}, {change.WarehouseId}, {change.LocationId}, {change.Quantity}, 0, 0, SYSUTCDATETIME(), SYSUTCDATETIME())
            """).ToListAsync(cancellationToken);

        return rows.Single();
    }

    /// <summary>New stock may only be placed in an active Bin of an active warehouse (spec 4.2 rule 4).</summary>
    private async Task EnsureLocationCanHoldStockAsync(StockChange change, CancellationToken cancellationToken)
    {
        var location = await dbContext.Set<WarehouseLocation>()
            .AsNoTracking()
            .Where(l => l.Id == change.LocationId && l.WarehouseId == change.WarehouseId)
            .Select(l => new { l.Code, l.Type, l.IsActive, WarehouseActive = l.Warehouse!.IsActive })
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new BusinessRuleException(
                $"Location {change.LocationId} does not exist in warehouse {change.WarehouseId}.");

        if (location.Type != LocationType.Bin)
        {
            throw new BusinessRuleException($"Stock can only be stored in a Bin; '{location.Code}' is a {location.Type}.");
        }

        if (!location.IsActive || !location.WarehouseActive)
        {
            throw new BusinessRuleException($"Location '{location.Code}' or its warehouse is inactive.");
        }

        if (!await dbContext.Set<Product>().AnyAsync(product => product.Id == change.ProductId, cancellationToken))
        {
            throw new BusinessRuleException($"Product {change.ProductId} does not exist.");
        }
    }

}

/// <summary>Result row of the OUTPUT clause used by <see cref="StockLedger"/>.</summary>
internal sealed class InventoryRow
{
    public int Id { get; set; }

    public decimal QuantityBefore { get; set; }

    public decimal QuantityAfter { get; set; }
}
