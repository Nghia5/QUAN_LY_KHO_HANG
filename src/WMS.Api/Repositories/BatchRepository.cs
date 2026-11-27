using WMS.Api.Authorization;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using WMS.Api.Common;
using WMS.Api.Data;
using WMS.Api.Dtos.Batches;
using WMS.Api.Dtos.Common;
using WMS.Api.Models;

namespace WMS.Api.Repositories;

public interface IBatchRepository
{
    Task<PagedResult<BatchResponse>> SearchAsync(BatchQuery query, DateOnly today, int nearExpiryDays, CancellationToken cancellationToken);

    Task<BatchDetail?> GetDetailAsync(int id, DateOnly today, int nearExpiryDays, CancellationToken cancellationToken);

    Task<PagedResult<ExpiryAlertRow>> GetExpiryAlertsAsync(ExpiryAlertQuery query, DateOnly today, int nearExpiryDays, CancellationToken cancellationToken);

    /// <summary>Inventory rows of a product in a warehouse with their lots (for pick suggestions).</summary>
    Task<IReadOnlyList<PickRow>> GetPickRowsAsync(int productId, int warehouseId, CancellationToken cancellationToken);

    Task<Batch?> FindAsync(int id, CancellationToken cancellationToken);

    Task<Batch?> FindByCodeAsync(int productId, string batchCode, CancellationToken cancellationToken);

    Task<bool> IsReferencedAsync(int id, CancellationToken cancellationToken);
}

public sealed record PickRow(int LocationId, string LocationCode, decimal Quantity, decimal AvailableQuantity, IReadOnlyList<PickLot> Lots);

public sealed record PickLot(int BatchId, string BatchCode, DateOnly? ManufactureDate, DateOnly? ExpiryDate, DateTime CreatedAt, decimal Quantity);

public sealed class BatchRepository(WmsDbContext dbContext, WarehouseAccess? access = null) : IBatchRepository
{
    private static readonly IReadOnlyDictionary<string, Expression<Func<Batch, object?>>> SortMap =
        QueryableExtensions.SortMap<Batch>(
            ("batchCode", batch => batch.BatchCode),
            ("expiryDate", batch => batch.ExpiryDate),
            ("manufactureDate", batch => batch.ManufactureDate),
            ("createdAt", batch => batch.CreatedAt),
            ("productSku", batch => batch.Product!.Sku));

    public async Task<PagedResult<BatchResponse>> SearchAsync(
        BatchQuery query,
        DateOnly today,
        int nearExpiryDays,
        CancellationToken cancellationToken)
    {
        var batches = dbContext.Set<Batch>().AsNoTracking();
        var nearLimit = today.AddDays(nearExpiryDays);

        if (query.ProductId is { } productId)
        {
            batches = batches.Where(batch => batch.ProductId == productId);
        }

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var keyword = query.Keyword.Trim();
            batches = batches.Where(batch =>
                batch.BatchCode.Contains(keyword) || batch.Product!.Sku.Contains(keyword) || batch.Product.Name.Contains(keyword));
        }

        batches = query.ExpiryStatus switch
        {
            ExpiryStatus.NoExpiry => batches.Where(batch => batch.ExpiryDate == null),
            ExpiryStatus.Expired => batches.Where(batch => batch.ExpiryDate < today),
            ExpiryStatus.NearExpiry => batches.Where(batch => batch.ExpiryDate >= today && batch.ExpiryDate <= nearLimit),
            ExpiryStatus.Valid => batches.Where(batch => batch.ExpiryDate > nearLimit),
            _ => batches
        };

        if (query.ExpiringWithinDays is { } days)
        {
            var limit = today.AddDays(days);
            batches = batches.Where(batch => batch.ExpiryDate >= today && batch.ExpiryDate <= limit);
        }

        var warehouseId = query.WarehouseId;
        var allowed = access?.WarehouseIds;
        if (allowed is not null)
        {
            batches = batches.Where(batch => batch.Lots.Any(lot =>
                allowed.Contains(lot.Inventory!.WarehouseId) && (warehouseId == null || lot.Inventory.WarehouseId == warehouseId)));
        }
        if (query.HasStock == true)
        {
            batches = batches.Where(batch => batch.Lots.Any(lot =>
                lot.Quantity > 0 && (allowed == null || allowed.Contains(lot.Inventory!.WarehouseId))
                && (warehouseId == null || lot.Inventory!.WarehouseId == warehouseId)));
        }

        var sorted = query.SortBy is null
            ? batches.OrderBy(batch => batch.ExpiryDate == null).ThenBy(batch => batch.ExpiryDate).ThenBy(batch => batch.Id)
            : batches.ApplySort(query, SortMap, "expiryDate", batch => batch.Id);

        var page = await sorted
            .Select(batch => new
            {
                batch.Id,
                batch.ProductId,
                batch.Product!.Sku,
                batch.Product.Name,
                batch.BatchCode,
                batch.ManufactureDate,
                batch.ExpiryDate,
                Quantity = batch.Lots
                    .Where(lot => (allowed == null || allowed.Contains(lot.Inventory!.WarehouseId))
                        && (warehouseId == null || lot.Inventory!.WarehouseId == warehouseId))
                    .Sum(lot => (decimal?)lot.Quantity) ?? 0m,
                batch.Note,
                batch.CreatedAt
            })
            .ToPagedResultAsync(query, cancellationToken);

        return new PagedResult<BatchResponse>(
            page.Items.Select(b => ToResponse(b.Id, b.ProductId, b.Sku, b.Name, b.BatchCode, b.ManufactureDate, b.ExpiryDate,
                b.Quantity, b.Note, b.CreatedAt, today, nearExpiryDays)).ToList(),
            page.Page,
            page.PageSize,
            page.TotalCount);
    }

    public async Task<BatchDetail?> GetDetailAsync(int id, DateOnly today, int nearExpiryDays, CancellationToken cancellationToken)
    {
        var allowed = access?.WarehouseIds;
        var batch = await dbContext.Set<Batch>()
            .AsNoTracking()
            .Where(b => b.Id == id && (allowed == null || b.Lots.Any(lot => allowed.Contains(lot.Inventory!.WarehouseId))))
            .Select(b => new
            {
                b.Id,
                b.ProductId,
                b.Product!.Sku,
                b.Product.Name,
                b.BatchCode,
                b.ManufactureDate,
                b.ExpiryDate,
                b.Note,
                b.CreatedAt,
                Lots = b.Lots
                    .Where(lot => lot.Quantity > 0 && (allowed == null || allowed.Contains(lot.Inventory!.WarehouseId)))
                    .OrderBy(lot => lot.Inventory!.Warehouse!.Code).ThenBy(lot => lot.Inventory!.Location!.Code)
                    .Select(lot => new BatchLotResponse(
                        lot.Inventory!.WarehouseId,
                        lot.Inventory.Warehouse!.Code,
                        lot.Inventory.LocationId,
                        lot.Inventory.Location!.Code,
                        lot.Quantity))
                    .ToList()
            })
            .SingleOrDefaultAsync(cancellationToken);

        return batch is null
            ? null
            : new BatchDetail(
                ToResponse(batch.Id, batch.ProductId, batch.Sku, batch.Name, batch.BatchCode, batch.ManufactureDate, batch.ExpiryDate,
                    batch.Lots.Sum(lot => lot.Quantity), batch.Note, batch.CreatedAt, today, nearExpiryDays),
                batch.Lots);
    }

    public async Task<PagedResult<ExpiryAlertRow>> GetExpiryAlertsAsync(
        ExpiryAlertQuery query,
        DateOnly today,
        int nearExpiryDays,
        CancellationToken cancellationToken)
    {
        var nearLimit = today.AddDays(query.Days ?? nearExpiryDays);
        var lots = dbContext.Set<InventoryLot>()
            .AsNoTracking()
            .Where(lot => lot.Quantity > 0 && lot.Batch!.ExpiryDate != null && lot.Batch.ExpiryDate <= nearLimit);

        if (access?.WarehouseIds is { } ids)
        {
            lots = lots.Where(lot => ids.Contains(lot.Inventory!.WarehouseId));
        }

        lots = query.Status switch
        {
            ExpiryStatus.Expired => lots.Where(lot => lot.Batch!.ExpiryDate < today),
            ExpiryStatus.NearExpiry => lots.Where(lot => lot.Batch!.ExpiryDate >= today),
            _ => lots
        };

        if (query.WarehouseId is { } warehouseId)
        {
            lots = lots.Where(lot => lot.Inventory!.WarehouseId == warehouseId);
        }

        if (query.ProductId is { } productId)
        {
            lots = lots.Where(lot => lot.ProductId == productId);
        }

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var keyword = query.Keyword.Trim();
            lots = lots.Where(lot => lot.Batch!.BatchCode.Contains(keyword) || lot.Batch.Product!.Sku.Contains(keyword));
        }

        var page = await lots
            .OrderBy(lot => lot.Batch!.ExpiryDate).ThenBy(lot => lot.Id)
            .Select(lot => new
            {
                lot.BatchId,
                lot.Batch!.BatchCode,
                lot.ProductId,
                lot.Batch.Product!.Sku,
                lot.Batch.Product.Name,
                lot.Inventory!.WarehouseId,
                WarehouseCode = lot.Inventory.Warehouse!.Code,
                lot.Inventory.LocationId,
                LocationCode = lot.Inventory.Location!.Code,
                lot.Quantity,
                ExpiryDate = lot.Batch.ExpiryDate!.Value
            })
            .ToPagedResultAsync(query, cancellationToken);

        return new PagedResult<ExpiryAlertRow>(
            page.Items.Select(row =>
            {
                var days = row.ExpiryDate.DayNumber - today.DayNumber;
                return new ExpiryAlertRow(row.BatchId, row.BatchCode, row.ProductId, row.Sku, row.Name, row.WarehouseId,
                    row.WarehouseCode, row.LocationId, row.LocationCode, row.Quantity, row.ExpiryDate, days,
                    days < 0 ? ExpiryStatus.Expired : ExpiryStatus.NearExpiry);
            }).ToList(),
            page.Page,
            page.PageSize,
            page.TotalCount);
    }

    public async Task<IReadOnlyList<PickRow>> GetPickRowsAsync(int productId, int warehouseId, CancellationToken cancellationToken) =>
        await dbContext.Set<Inventory>()
            .AsNoTracking()
            .Where(inventory => inventory.ProductId == productId && inventory.WarehouseId == warehouseId && inventory.Quantity > 0)
            .Select(inventory => new PickRow(
                inventory.LocationId,
                inventory.Location!.Code,
                inventory.Quantity,
                inventory.AvailableQuantity,
                dbContext.Set<InventoryLot>()
                    .Where(lot => lot.InventoryId == inventory.Id && lot.Quantity > 0)
                    .Select(lot => new PickLot(lot.BatchId, lot.Batch!.BatchCode, lot.Batch.ManufactureDate, lot.Batch.ExpiryDate,
                        lot.Batch.CreatedAt, lot.Quantity))
                    .ToList()))
            .ToListAsync(cancellationToken);

    public Task<Batch?> FindAsync(int id, CancellationToken cancellationToken) =>
        dbContext.Set<Batch>().SingleOrDefaultAsync(batch => batch.Id == id, cancellationToken);

    public Task<Batch?> FindByCodeAsync(int productId, string batchCode, CancellationToken cancellationToken) =>
        dbContext.Set<Batch>().SingleOrDefaultAsync(batch => batch.ProductId == productId && batch.BatchCode == batchCode, cancellationToken);

    public async Task<bool> IsReferencedAsync(int id, CancellationToken cancellationToken) =>
        await dbContext.Set<InventoryLot>().AnyAsync(lot => lot.BatchId == id, cancellationToken)
        || await dbContext.Set<StockMovement>().AnyAsync(movement => movement.BatchId == id, cancellationToken)
        || await dbContext.Set<Models.Documents.GoodsIssueLine>().AnyAsync(line => line.BatchId == id, cancellationToken);

    public static BatchResponse ToResponse(
        int id, int productId, string sku, string name, string batchCode, DateOnly? manufactureDate, DateOnly? expiryDate,
        decimal quantity, string? note, DateTime createdAt, DateOnly today, int nearExpiryDays)
    {
        int? days = expiryDate is { } expiry ? expiry.DayNumber - today.DayNumber : null;
        var status = days switch
        {
            null => ExpiryStatus.NoExpiry,
            < 0 => ExpiryStatus.Expired,
            _ when days <= nearExpiryDays => ExpiryStatus.NearExpiry,
            _ => ExpiryStatus.Valid
        };
        return new BatchResponse(id, productId, sku, name, batchCode, manufactureDate, expiryDate, days, status, quantity, note, createdAt);
    }
}
