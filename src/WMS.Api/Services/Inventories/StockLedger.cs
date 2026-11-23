using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using WMS.Api.Common;
using WMS.Api.Data;
using WMS.Api.Exceptions;
using WMS.Api.Models;

namespace WMS.Api.Services.Inventories;

/// <summary>One quantity change of one inventory row (Product + Warehouse + Location).</summary>
/// <param name="Quantity">Signed: positive for inbound types, negative for outbound types.</param>
public sealed record StockChange(
    int ProductId,
    int WarehouseId,
    int LocationId,
    decimal Quantity,
    MovementType MovementType,
    string? Note = null);

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
}

public sealed class StockLedger(
    WmsDbContext dbContext,
    ICurrentUser currentUser,
    TimeProvider timeProvider) : IStockLedger
{
    public async Task<IReadOnlyList<StockMovement>> ApplyAsync(
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

        var movements = new List<StockMovement>(changes.Count);
        var userId = currentUser.UserId;
        var now = timeProvider.GetUtcNow().UtcDateTime;

        // Fixed lock order across all documents to avoid deadlocks between concurrent documents.
        foreach (var change in changes.OrderBy(c => c.ProductId).ThenBy(c => c.WarehouseId).ThenBy(c => c.LocationId))
        {
            var row = await ApplyToInventoryAsync(change, cancellationToken);
            movements.Add(new StockMovement
            {
                InventoryId = row.Id,
                ProductId = change.ProductId,
                WarehouseId = change.WarehouseId,
                LocationId = change.LocationId,
                MovementType = change.MovementType,
                Quantity = change.Quantity,
                QuantityBefore = row.QuantityBefore,
                QuantityAfter = row.QuantityAfter,
                ReferenceType = reference.ReferenceType,
                ReferenceId = reference.ReferenceId,
                ReferenceNo = reference.ReferenceNo,
                Note = change.Note,
                CreatedBy = userId,
                CreatedAt = now
            });
        }

        dbContext.Set<StockMovement>().AddRange(movements);
        await dbContext.SaveChangesAsync(cancellationToken);

        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }

        return movements;
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

        if (existing is not null)
        {
            throw new BusinessRuleException(
                $"Insufficient stock for product {change.ProductId} at location {change.LocationId}: " +
                $"available {existing.AvailableQuantity:0.###} (on hand {existing.Quantity:0.###}, reserved {existing.ReservedQuantity:0.###}), " +
                $"requested {-change.Quantity:0.###}.");
        }

        if (change.Quantity < 0)
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
            SET [Quantity] = [Quantity] + {change.Quantity}, [UpdatedAt] = SYSUTCDATETIME()
            OUTPUT inserted.[Id] AS [Id], deleted.[Quantity] AS [QuantityBefore], inserted.[Quantity] AS [QuantityAfter]
            WHERE [ProductId] = {change.ProductId}
              AND [WarehouseId] = {change.WarehouseId}
              AND [LocationId] = {change.LocationId}
              AND [Quantity] + {change.Quantity} >= [ReservedQuantity]
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
