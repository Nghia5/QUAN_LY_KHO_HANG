using WMS.Api.Authorization;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using WMS.Api.Common;
using WMS.Api.Data;
using WMS.Api.Dtos.Common;
using WMS.Api.Dtos.Documents;
using WMS.Api.Dtos.StockTransfers;
using WMS.Api.Models;
using WMS.Api.Models.Documents;
using WMS.Api.Services.Documents;

namespace WMS.Api.Repositories;

public interface IStockTransferRepository
{
    Task<PagedResult<StockTransferSummary>> SearchAsync(StockTransferQuery query, CancellationToken cancellationToken);

    Task<StockTransferDetail?> GetDetailAsync(int id, CancellationToken cancellationToken);

    /// <summary>Tracked transfer with its lines.</summary>
    Task<StockTransfer?> FindAsync(int id, CancellationToken cancellationToken);

    void Add(StockTransfer transfer);

    /// <summary>Transfers where the warehouse is source or destination.</summary>
    Task<int> CountOpenByWarehouseAsync(int warehouseId, CancellationToken cancellationToken);

    Task<int> CountByWarehouseAsync(int warehouseId, CancellationToken cancellationToken);

    Task<int> CountOpenByLocationAsync(int locationId, CancellationToken cancellationToken);
}

public sealed class StockTransferRepository(WmsDbContext dbContext, WarehouseAccess? access = null) : IStockTransferRepository
{
    private static readonly IReadOnlyDictionary<string, Expression<Func<StockTransfer, object?>>> SortMap =
        QueryableExtensions.SortMap<StockTransfer>(
            ("documentNo", transfer => transfer.DocumentNo),
            ("status", transfer => transfer.Status),
            ("createdAt", transfer => transfer.CreatedAt),
            ("totalQuantity", transfer => transfer.Lines.Sum(line => line.Quantity)));

    private IQueryable<StockTransfer> Transfers => dbContext.Set<StockTransfer>().AsNoTracking();

    public Task<PagedResult<StockTransferSummary>> SearchAsync(StockTransferQuery query, CancellationToken cancellationToken)
    {
        var transfers = Transfers;
        if (access?.WarehouseIds is { } ids)
        {
            transfers = transfers.Where(t => ids.Contains(t.WarehouseId) || ids.Contains(t.DestinationWarehouseId));
        }

        if (query.WarehouseId is { } warehouseId)
        {
            transfers = transfers.Where(t => t.WarehouseId == warehouseId || t.DestinationWarehouseId == warehouseId);
        }

        if (query.SourceWarehouseId is { } sourceId)
        {
            transfers = transfers.Where(t => t.WarehouseId == sourceId);
        }

        if (query.DestinationWarehouseId is { } destinationId)
        {
            transfers = transfers.Where(t => t.DestinationWarehouseId == destinationId);
        }

        if (query.Status is { } status)
        {
            transfers = transfers.Where(t => t.Status == status);
        }

        if (query.ProductId is { } productId)
        {
            transfers = transfers.Where(t => t.Lines.Any(line => line.ProductId == productId));
        }

        if (query.IsInternal is { } isInternal)
        {
            transfers = transfers.Where(t => (t.WarehouseId == t.DestinationWarehouseId) == isInternal);
        }

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var keyword = query.Keyword.Trim();
            transfers = transfers.Where(t => t.DocumentNo.Contains(keyword));
        }

        var sorted = query.SortBy is null
            ? transfers.OrderByDescending(t => t.Id)
            : transfers.ApplySort(query, SortMap, "createdAt", t => t.Id);

        return sorted
            .Select(t => new StockTransferSummary(
                t.Id,
                t.DocumentNo,
                t.WarehouseId,
                t.Warehouse!.Code,
                t.DestinationWarehouseId,
                t.DestinationWarehouse!.Code,
                t.WarehouseId == t.DestinationWarehouseId,
                t.Status,
                t.Lines.Count,
                t.Lines.Sum(line => line.Quantity),
                t.CreatedBy,
                t.CreatedAt))
            .ToPagedResultAsync(query, cancellationToken);
    }

    public async Task<StockTransferDetail?> GetDetailAsync(int id, CancellationToken cancellationToken)
    {
        var inventories = dbContext.Set<Inventory>();
        var transfer = await Transfers
            .Where(t => t.Id == id)
            .Select(t => new
            {
                t.Id,
                t.DocumentNo,
                t.WarehouseId,
                SourceCode = t.Warehouse!.Code,
                t.DestinationWarehouseId,
                DestinationCode = t.DestinationWarehouse!.Code,
                t.Status,
                t.Reason,
                t.Note,
                Workflow = new DocumentWorkflowInfo(
                    t.CreatedBy, t.CreatedAt, t.SubmittedBy, t.SubmittedAt, t.ApprovedBy, t.ApprovedAt,
                    t.RejectedBy, t.RejectedAt, t.RejectionReason, t.CompletedBy, t.CompletedAt,
                    t.CancelledBy, t.CancelledAt, t.CancellationReason),
                Progress = new TransferProgressInfo(t.PickedBy, t.PickedAt, t.ShippedBy, t.ShippedAt, t.ReceivedBy, t.ReceivedAt),
                Lines = t.Lines
                    .OrderBy(line => line.Id)
                    .Select(line => new StockTransferLineResponse(
                        line.Id,
                        line.ProductId,
                        line.Product!.Sku,
                        line.Product.Name,
                        line.Product.UnitOfMeasure,
                        line.FromLocationId,
                        line.FromLocation!.Code,
                        line.ToLocationId,
                        line.ToLocation!.Code,
                        line.Quantity,
                        inventories
                            .Where(inv => inv.ProductId == line.ProductId && inv.LocationId == line.FromLocationId)
                            .Select(inv => inv.AvailableQuantity)
                            .FirstOrDefault(),
                        line.Note))
                    .ToList()
            })
            .AsSplitQuery()
            .SingleOrDefaultAsync(cancellationToken);

        return transfer is null
            ? null
            : new StockTransferDetail(
                transfer.Id,
                transfer.DocumentNo,
                transfer.WarehouseId,
                transfer.SourceCode,
                transfer.DestinationWarehouseId,
                transfer.DestinationCode,
                transfer.WarehouseId == transfer.DestinationWarehouseId,
                transfer.Status,
                transfer.Reason,
                transfer.Note,
                transfer.Lines.Sum(line => line.Quantity),
                transfer.Lines,
                transfer.Workflow,
                transfer.Progress,
                DocumentWorkflow.AvailableActions(transfer.Status, DocumentFlow.Transfer));
    }

    public Task<StockTransfer?> FindAsync(int id, CancellationToken cancellationToken) =>
        dbContext.Set<StockTransfer>()
            .Include(transfer => transfer.Lines)
            .SingleOrDefaultAsync(transfer => transfer.Id == id, cancellationToken);

    public void Add(StockTransfer transfer) => dbContext.Set<StockTransfer>().Add(transfer);

    public Task<int> CountOpenByWarehouseAsync(int warehouseId, CancellationToken cancellationToken) =>
        Transfers.CountAsync(
            t => (t.WarehouseId == warehouseId || t.DestinationWarehouseId == warehouseId) && DocumentStatuses.Open.Contains(t.Status),
            cancellationToken);

    public Task<int> CountByWarehouseAsync(int warehouseId, CancellationToken cancellationToken) =>
        Transfers.CountAsync(t => t.WarehouseId == warehouseId || t.DestinationWarehouseId == warehouseId, cancellationToken);

    public Task<int> CountOpenByLocationAsync(int locationId, CancellationToken cancellationToken) =>
        Transfers.CountAsync(
            t => DocumentStatuses.Open.Contains(t.Status)
                 && t.Lines.Any(line => line.FromLocationId == locationId || line.ToLocationId == locationId),
            cancellationToken);
}
