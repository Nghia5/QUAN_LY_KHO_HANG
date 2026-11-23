using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using WMS.Api.Common;
using WMS.Api.Data;
using WMS.Api.Dtos.Common;
using WMS.Api.Dtos.Inventories;
using WMS.Api.Models;

namespace WMS.Api.Repositories;

public sealed class InventoryRepository(WmsDbContext dbContext) : IInventoryRepository
{
    // Whitelist: only these keys can reach ORDER BY.
    private static readonly IReadOnlyDictionary<string, Expression<Func<Inventory, object?>>> SortMap =
        QueryableExtensions.SortMap<Inventory>(
            ("productSku", inventory => inventory.Product!.Sku),
            ("productName", inventory => inventory.Product!.Name),
            ("warehouseCode", inventory => inventory.Warehouse!.Code),
            ("locationCode", inventory => inventory.Location!.Code),
            ("quantity", inventory => inventory.Quantity),
            ("reservedQuantity", inventory => inventory.ReservedQuantity),
            ("availableQuantity", inventory => inventory.AvailableQuantity),
            ("minQuantity", inventory => inventory.MinQuantity),
            ("updatedAt", inventory => inventory.UpdatedAt));

    /// <summary>
    /// Projection used by every read: navigations are translated to INNER JOINs and only the
    /// listed columns are selected (no Include, no entity tracking).
    /// </summary>
    private static readonly Expression<Func<Inventory, InventoryResponse>> ToResponse =
        inventory => new InventoryResponse(
            inventory.Id,
            inventory.ProductId,
            inventory.Product!.Sku,
            inventory.Product.Name,
            inventory.Product.UnitOfMeasure,
            inventory.WarehouseId,
            inventory.Warehouse!.Code,
            inventory.Warehouse.Name,
            inventory.LocationId,
            inventory.Location!.Code,
            inventory.Quantity,
            inventory.ReservedQuantity,
            inventory.AvailableQuantity,
            inventory.MinQuantity,
            inventory.AvailableQuantity <= 0
                ? StockStatus.OutOfStock
                : inventory.AvailableQuantity <= inventory.MinQuantity
                    ? StockStatus.LowStock
                    : StockStatus.InStock,
            inventory.UpdatedAt);

    public Task<PagedResult<InventoryResponse>> SearchAsync(InventoryQuery query, CancellationToken cancellationToken) =>
        ApplyFilters(dbContext.Set<Inventory>().AsNoTracking(), query)
            .ApplySort(query, SortMap, "productSku", inventory => inventory.Id)
            .Select(ToResponse)
            .ToPagedResultAsync(query, cancellationToken);

    public Task<InventoryResponse?> GetByIdAsync(int id, CancellationToken cancellationToken) =>
        dbContext.Set<Inventory>()
            .AsNoTracking()
            .Where(inventory => inventory.Id == id)
            .Select(ToResponse)
            .SingleOrDefaultAsync(cancellationToken);

    internal static IQueryable<Inventory> ApplyFilters(IQueryable<Inventory> inventories, InventoryQuery query)
    {
        if (query.WarehouseId is { } warehouseId)
        {
            inventories = inventories.Where(inventory => inventory.WarehouseId == warehouseId);
        }

        if (query.LocationId is { } locationId)
        {
            inventories = inventories.Where(inventory => inventory.LocationId == locationId);
        }

        if (query.ProductId is { } productId)
        {
            inventories = inventories.Where(inventory => inventory.ProductId == productId);
        }

        if (!string.IsNullOrWhiteSpace(query.Category))
        {
            var category = query.Category.Trim();
            inventories = inventories.Where(inventory => inventory.Product!.Category == category);
        }

        if (query.HasStock == true)
        {
            inventories = inventories.Where(inventory => inventory.Quantity > 0);
        }

        inventories = query.StockStatus switch
        {
            StockStatus.OutOfStock => inventories.Where(inventory => inventory.AvailableQuantity <= 0),
            StockStatus.LowStock => inventories.Where(inventory =>
                inventory.AvailableQuantity > 0 && inventory.AvailableQuantity <= inventory.MinQuantity),
            StockStatus.InStock => inventories.Where(inventory => inventory.AvailableQuantity > inventory.MinQuantity),
            _ => inventories
        };

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var keyword = query.Keyword.Trim();
            inventories = inventories.Where(inventory =>
                inventory.Product!.Sku.Contains(keyword)
                || inventory.Product.Name.Contains(keyword)
                || inventory.Location!.Code.Contains(keyword)
                || inventory.Warehouse!.Code.Contains(keyword));
        }

        return inventories;
    }
}
