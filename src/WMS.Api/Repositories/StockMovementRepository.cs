using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using WMS.Api.Common;
using WMS.Api.Data;
using WMS.Api.Dtos.Common;
using WMS.Api.Dtos.StockMovements;
using WMS.Api.Models;

namespace WMS.Api.Repositories;

/// <summary>Read-only access to the stock ledger. Writes go exclusively through IStockLedger.</summary>
public interface IStockMovementRepository
{
    Task<PagedResult<StockMovementResponse>> SearchAsync(StockMovementQuery query, CancellationToken cancellationToken);

    Task<StockMovementResponse?> GetByIdAsync(long id, CancellationToken cancellationToken);

    Task<PagedResult<StockReportRow>> GetStockReportAsync(StockReportQuery query, CancellationToken cancellationToken);

    Task<int> CountByWarehouseAsync(int warehouseId, CancellationToken cancellationToken);

    Task<int> CountByLocationAsync(int locationId, CancellationToken cancellationToken);
}

public sealed class StockMovementRepository(WmsDbContext dbContext) : IStockMovementRepository
{
    private static readonly IReadOnlyDictionary<string, Expression<Func<StockMovement, object?>>> SortMap =
        QueryableExtensions.SortMap<StockMovement>(
            ("createdAt", movement => movement.CreatedAt),
            ("productSku", movement => movement.Product!.Sku),
            ("quantity", movement => movement.Quantity),
            ("movementType", movement => movement.MovementType),
            ("referenceNo", movement => movement.ReferenceNo));

    private static readonly Expression<Func<StockMovement, StockMovementResponse>> ToResponse =
        movement => new StockMovementResponse(
            movement.Id,
            movement.InventoryId,
            movement.ProductId,
            movement.Product!.Sku,
            movement.Product.Name,
            movement.WarehouseId,
            movement.Warehouse!.Code,
            movement.LocationId,
            movement.Location!.Code,
            movement.MovementType,
            movement.Quantity,
            movement.QuantityBefore,
            movement.QuantityAfter,
            movement.ReferenceType,
            movement.ReferenceId,
            movement.ReferenceNo,
            movement.Note,
            movement.CreatedBy,
            movement.CreatedAt);

    private IQueryable<StockMovement> Movements => dbContext.Set<StockMovement>().AsNoTracking();

    public Task<PagedResult<StockMovementResponse>> SearchAsync(StockMovementQuery query, CancellationToken cancellationToken)
    {
        var movements = Movements;

        if (query.WarehouseId is { } warehouseId)
        {
            movements = movements.Where(movement => movement.WarehouseId == warehouseId);
        }

        if (query.LocationId is { } locationId)
        {
            movements = movements.Where(movement => movement.LocationId == locationId);
        }

        if (query.ProductId is { } productId)
        {
            movements = movements.Where(movement => movement.ProductId == productId);
        }

        if (query.MovementType is { } movementType)
        {
            movements = movements.Where(movement => movement.MovementType == movementType);
        }

        if (!string.IsNullOrWhiteSpace(query.ReferenceType))
        {
            var referenceType = query.ReferenceType.Trim().ToUpperInvariant();
            movements = movements.Where(movement => movement.ReferenceType == referenceType);
        }

        if (query.ReferenceId is { } referenceId)
        {
            movements = movements.Where(movement => movement.ReferenceId == referenceId);
        }

        if (query.CreatedBy is { } createdBy)
        {
            movements = movements.Where(movement => movement.CreatedBy == createdBy);
        }

        if (query.From is { } from)
        {
            var fromUtc = ToUtc(from);
            movements = movements.Where(movement => movement.CreatedAt >= fromUtc);
        }

        if (query.To is { } to)
        {
            var toUtc = ToUtc(to);
            movements = movements.Where(movement => movement.CreatedAt < toUtc);
        }

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var keyword = query.Keyword.Trim();
            movements = movements.Where(movement =>
                movement.Product!.Sku.Contains(keyword)
                || movement.Product.Name.Contains(keyword)
                || (movement.ReferenceNo != null && movement.ReferenceNo.Contains(keyword)));
        }

        // Default: newest first. Id is the tie-breaker and also the true order of the ledger.
        var sorted = query.SortBy is null
            ? movements.OrderByDescending(movement => movement.Id)
            : movements.ApplySort(query, SortMap, "createdAt", movement => movement.Id);

        return sorted.Select(ToResponse).ToPagedResultAsync(query, cancellationToken);
    }

    public Task<StockMovementResponse?> GetByIdAsync(long id, CancellationToken cancellationToken) =>
        Movements.Where(movement => movement.Id == id).Select(ToResponse).SingleOrDefaultAsync(cancellationToken);

    public async Task<PagedResult<StockReportRow>> GetStockReportAsync(StockReportQuery query, CancellationToken cancellationToken)
    {
        var from = ToUtc(query.From);
        var to = ToUtc(query.To);

        var movements = Movements.Where(movement => movement.CreatedAt < to);
        if (query.WarehouseId is { } warehouseId)
        {
            movements = movements.Where(movement => movement.WarehouseId == warehouseId);
        }

        if (query.ProductId is { } productId)
        {
            movements = movements.Where(movement => movement.ProductId == productId);
        }

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var keyword = query.Keyword.Trim();
            movements = movements.Where(movement =>
                movement.Product!.Sku.Contains(keyword) || movement.Product.Name.Contains(keyword));
        }

        // Every quantity change is in the ledger, so stock at a point in time = sum of movements before it.
        var totals = movements
            .GroupBy(movement => new { movement.ProductId, movement.WarehouseId })
            .Select(group => new
            {
                group.Key.ProductId,
                group.Key.WarehouseId,
                Opening = group.Sum(m => m.CreatedAt < from ? m.Quantity : 0m),
                In = group.Sum(m => m.CreatedAt >= from && m.Quantity > 0 ? m.Quantity : 0m),
                Out = group.Sum(m => m.CreatedAt >= from && m.Quantity < 0 ? -m.Quantity : 0m)
            });

        // Member-initialized shape (not a constructor) so EF can translate ORDER BY on its members.
        var rows =
            from total in totals
            join product in dbContext.Set<Product>() on total.ProductId equals product.Id
            join warehouse in dbContext.Set<Warehouse>() on total.WarehouseId equals warehouse.Id
            select new ReportRowData
            {
                ProductId = total.ProductId,
                ProductSku = product.Sku,
                ProductName = product.Name,
                UnitOfMeasure = product.UnitOfMeasure,
                WarehouseId = total.WarehouseId,
                WarehouseCode = warehouse.Code,
                OpeningQuantity = total.Opening,
                InQuantity = total.In,
                OutQuantity = total.Out,
                ClosingQuantity = total.Opening + total.In - total.Out
            };

        return await rows
            .ApplySort(query, ReportSortMap, "productSku", row => row.WarehouseCode)
            .Select(row => new StockReportRow(
                row.ProductId,
                row.ProductSku,
                row.ProductName,
                row.UnitOfMeasure,
                row.WarehouseId,
                row.WarehouseCode,
                row.OpeningQuantity,
                row.InQuantity,
                row.OutQuantity,
                row.ClosingQuantity))
            .ToPagedResultAsync(query, cancellationToken);
    }

    private static readonly IReadOnlyDictionary<string, Expression<Func<ReportRowData, object?>>> ReportSortMap =
        QueryableExtensions.SortMap<ReportRowData>(
            ("productSku", row => row.ProductSku),
            ("productName", row => row.ProductName),
            ("warehouseCode", row => row.WarehouseCode),
            ("inQuantity", row => row.InQuantity),
            ("outQuantity", row => row.OutQuantity),
            ("closingQuantity", row => row.ClosingQuantity));

    private sealed class ReportRowData
    {
        public int ProductId { get; init; }
        public string ProductSku { get; init; } = "";
        public string ProductName { get; init; } = "";
        public string UnitOfMeasure { get; init; } = "";
        public int WarehouseId { get; init; }
        public string WarehouseCode { get; init; } = "";
        public decimal OpeningQuantity { get; init; }
        public decimal InQuantity { get; init; }
        public decimal OutQuantity { get; init; }
        public decimal ClosingQuantity { get; init; }
    }

    public Task<int> CountByWarehouseAsync(int warehouseId, CancellationToken cancellationToken) =>
        Movements.CountAsync(movement => movement.WarehouseId == warehouseId, cancellationToken);

    public Task<int> CountByLocationAsync(int locationId, CancellationToken cancellationToken) =>
        Movements.CountAsync(movement => movement.LocationId == locationId, cancellationToken);

    private static DateTime ToUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };
}
