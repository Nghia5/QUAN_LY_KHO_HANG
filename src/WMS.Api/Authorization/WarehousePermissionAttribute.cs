using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using WMS.Api.Common;
using WMS.Api.Data;
using WMS.Api.Exceptions;
using WMS.Api.Models;
using WMS.Api.Models.Documents;

namespace WMS.Api.Authorization;

public interface IWarehouseRequest
{
    int WarehouseId { get; }
}

public enum WarehouseResource
{
    None, Warehouse, Receipt, Issue, Transfer, TransferDestination, TransferEither,
    Stocktake, Adjustment, Inventory, Movement
}

[AttributeUsage(AttributeTargets.Method)]
public sealed class WarehousePermissionAttribute(string permission, WarehouseResource resource = WarehouseResource.None)
    : Attribute, IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var services = context.HttpContext.RequestServices;
        var access = services.GetRequiredService<WarehouseAccess>();
        var cancellationToken = context.HttpContext.RequestAborted;
        await access.LoadAsync(permission, cancellationToken);

        if (context.ActionArguments.TryGetValue("warehouseId", out var warehouse) && warehouse is int warehouseId)
        {
            access.Require(warehouseId);
        }

        foreach (var request in context.ActionArguments.Values.OfType<IWarehouseRequest>())
        {
            access.Require(request.WarehouseId);
        }

        if (resource != WarehouseResource.None && context.ActionArguments.TryGetValue("id", out var id))
        {
            await CheckResourceAsync(services.GetRequiredService<WmsDbContext>(), access, id!, cancellationToken);
        }

        await next();
    }

    private async Task CheckResourceAsync(WmsDbContext db, WarehouseAccess access, object id, CancellationToken cancellationToken)
    {
        if (resource is WarehouseResource.Transfer or WarehouseResource.TransferDestination or WarehouseResource.TransferEither)
        {
            var transfer = await db.Set<StockTransfer>().Where(t => t.Id == (int)id)
                .Select(t => new { t.WarehouseId, t.DestinationWarehouseId }).SingleOrDefaultAsync(cancellationToken)
                ?? throw new NotFoundException("Stock transfer", id);

            if (resource == WarehouseResource.TransferEither)
            {
                access.RequireEither(transfer.WarehouseId, transfer.DestinationWarehouseId);
                return;
            }

            access.Require(resource == WarehouseResource.TransferDestination ? transfer.DestinationWarehouseId : transfer.WarehouseId);
            return;
        }

        var warehouseId = resource switch
        {
            WarehouseResource.Warehouse => (int?)id,
            WarehouseResource.Receipt => await FindDocumentWarehouseAsync<GoodsReceipt>(db, (int)id, cancellationToken),
            WarehouseResource.Issue => await FindDocumentWarehouseAsync<GoodsIssue>(db, (int)id, cancellationToken),
            WarehouseResource.Stocktake => await FindDocumentWarehouseAsync<Stocktake>(db, (int)id, cancellationToken),
            WarehouseResource.Adjustment => await FindDocumentWarehouseAsync<StockAdjustment>(db, (int)id, cancellationToken),
            WarehouseResource.Inventory => await db.Set<Inventory>().Where(i => i.Id == (int)id)
                .Select(i => (int?)i.WarehouseId).SingleOrDefaultAsync(cancellationToken),
            WarehouseResource.Movement => await db.Set<StockMovement>().Where(m => m.Id == (long)id)
                .Select(m => (int?)m.WarehouseId).SingleOrDefaultAsync(cancellationToken),
            _ => throw new InvalidOperationException("Unknown warehouse resource.")
        };
        access.Require(warehouseId ?? throw new NotFoundException(resource.ToString(), id));
    }

    private static Task<int?> FindDocumentWarehouseAsync<T>(WmsDbContext db, int id, CancellationToken cancellationToken)
        where T : WarehouseDocument =>
        db.Set<T>().Where(d => d.Id == id).Select(d => (int?)d.WarehouseId).SingleOrDefaultAsync(cancellationToken);
}
