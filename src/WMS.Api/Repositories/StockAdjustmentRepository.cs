using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using WMS.Api.Common;
using WMS.Api.Data;
using WMS.Api.Dtos.Common;
using WMS.Api.Dtos.Documents;
using WMS.Api.Dtos.StockAdjustments;
using WMS.Api.Models;
using WMS.Api.Models.Documents;
using WMS.Api.Services.Documents;

namespace WMS.Api.Repositories;

public interface IStockAdjustmentRepository
{
    Task<PagedResult<StockAdjustmentSummary>> SearchAsync(StockAdjustmentQuery query, CancellationToken cancellationToken);

    Task<StockAdjustmentDetail?> GetDetailAsync(int id, bool requiresApproval, CancellationToken cancellationToken);

    /// <summary>Tracked adjustment with its lines.</summary>
    Task<StockAdjustment?> FindAsync(int id, CancellationToken cancellationToken);

    void Add(StockAdjustment adjustment);

    Task<int> CountOpenByWarehouseAsync(int warehouseId, CancellationToken cancellationToken);

    Task<int> CountByWarehouseAsync(int warehouseId, CancellationToken cancellationToken);

    Task<int> CountOpenByLocationAsync(int locationId, CancellationToken cancellationToken);
}

public sealed class StockAdjustmentRepository(WmsDbContext dbContext) : IStockAdjustmentRepository
{
    private static readonly IReadOnlyDictionary<string, Expression<Func<StockAdjustment, object?>>> SortMap =
        QueryableExtensions.SortMap<StockAdjustment>(
            ("documentNo", adjustment => adjustment.DocumentNo),
            ("status", adjustment => adjustment.Status),
            ("createdAt", adjustment => adjustment.CreatedAt),
            ("reasonCode", adjustment => adjustment.ReasonCode));

    private IQueryable<StockAdjustment> Adjustments => dbContext.Set<StockAdjustment>().AsNoTracking();

    public Task<PagedResult<StockAdjustmentSummary>> SearchAsync(StockAdjustmentQuery query, CancellationToken cancellationToken)
    {
        var adjustments = Adjustments;

        if (query.WarehouseId is { } warehouseId)
        {
            adjustments = adjustments.Where(a => a.WarehouseId == warehouseId);
        }

        if (query.Status is { } status)
        {
            adjustments = adjustments.Where(a => a.Status == status);
        }

        if (query.ReasonCode is { } reasonCode)
        {
            adjustments = adjustments.Where(a => a.ReasonCode == reasonCode);
        }

        if (query.ProductId is { } productId)
        {
            adjustments = adjustments.Where(a => a.Lines.Any(line => line.ProductId == productId));
        }

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var keyword = query.Keyword.Trim();
            adjustments = adjustments.Where(a => a.DocumentNo.Contains(keyword) || a.Reason.Contains(keyword));
        }

        var sorted = query.SortBy is null
            ? adjustments.OrderByDescending(a => a.Id)
            : adjustments.ApplySort(query, SortMap, "createdAt", a => a.Id);

        return sorted
            .Select(a => new StockAdjustmentSummary(
                a.Id,
                a.DocumentNo,
                a.WarehouseId,
                a.Warehouse!.Code,
                a.Status,
                a.ReasonCode,
                a.Reason,
                a.Lines.Count,
                a.Lines.Where(line => line.AdjustmentQuantity > 0).Sum(line => line.AdjustmentQuantity),
                -a.Lines.Where(line => line.AdjustmentQuantity < 0).Sum(line => line.AdjustmentQuantity),
                a.CreatedBy,
                a.CreatedAt))
            .ToPagedResultAsync(query, cancellationToken);
    }

    public async Task<StockAdjustmentDetail?> GetDetailAsync(int id, bool requiresApproval, CancellationToken cancellationToken)
    {
        var inventories = dbContext.Set<Inventory>();
        var adjustment = await Adjustments
            .Where(a => a.Id == id)
            .Select(a => new
            {
                a.Id,
                a.DocumentNo,
                a.WarehouseId,
                WarehouseCode = a.Warehouse!.Code,
                a.Status,
                a.ReasonCode,
                a.Reason,
                a.Note,
                Workflow = new DocumentWorkflowInfo(
                    a.CreatedBy, a.CreatedAt, a.SubmittedBy, a.SubmittedAt, a.ApprovedBy, a.ApprovedAt,
                    a.RejectedBy, a.RejectedAt, a.RejectionReason, a.CompletedBy, a.CompletedAt,
                    a.CancelledBy, a.CancelledAt, a.CancellationReason),
                Lines = a.Lines
                    .OrderBy(line => line.Id)
                    .Select(line => new
                    {
                        line.Id,
                        line.ProductId,
                        line.Product!.Sku,
                        line.Product.Name,
                        line.Product.UnitOfMeasure,
                        line.LocationId,
                        LocationCode = line.Location!.Code,
                        line.AdjustmentQuantity,
                        Current = inventories
                            .Where(inv => inv.ProductId == line.ProductId && inv.LocationId == line.LocationId)
                            .Select(inv => new { inv.Quantity, inv.ReservedQuantity })
                            .FirstOrDefault(),
                        line.BeforeQuantity,
                        line.AfterQuantity,
                        line.Note
                    })
                    .ToList()
            })
            .AsSplitQuery()
            .SingleOrDefaultAsync(cancellationToken);

        if (adjustment is null)
        {
            return null;
        }

        var lines = adjustment.Lines
            .Select(line =>
            {
                var current = line.Current?.Quantity ?? 0m;
                // Once applied, the preview is the recorded result, not "current + adjustment" again.
                var expectedAfter = line.AfterQuantity ?? current + line.AdjustmentQuantity;
                return new StockAdjustmentLineResponse(
                    line.Id, line.ProductId, line.Sku, line.Name, line.UnitOfMeasure, line.LocationId, line.LocationCode,
                    line.AdjustmentQuantity, current, line.Current?.ReservedQuantity ?? 0m, expectedAfter,
                    line.BeforeQuantity, line.AfterQuantity, line.Note);
            })
            .ToList();

        return new StockAdjustmentDetail(
            adjustment.Id,
            adjustment.DocumentNo,
            adjustment.WarehouseId,
            adjustment.WarehouseCode,
            adjustment.Status,
            adjustment.ReasonCode,
            adjustment.Reason,
            adjustment.Note,
            requiresApproval,
            lines,
            adjustment.Workflow,
            DocumentWorkflow.AvailableActions(adjustment.Status));
    }

    public Task<StockAdjustment?> FindAsync(int id, CancellationToken cancellationToken) =>
        dbContext.Set<StockAdjustment>()
            .Include(adjustment => adjustment.Lines)
            .SingleOrDefaultAsync(adjustment => adjustment.Id == id, cancellationToken);

    public void Add(StockAdjustment adjustment) => dbContext.Set<StockAdjustment>().Add(adjustment);

    public Task<int> CountOpenByWarehouseAsync(int warehouseId, CancellationToken cancellationToken) =>
        Adjustments.CountAsync(a => a.WarehouseId == warehouseId && DocumentStatuses.Open.Contains(a.Status), cancellationToken);

    public Task<int> CountByWarehouseAsync(int warehouseId, CancellationToken cancellationToken) =>
        Adjustments.CountAsync(a => a.WarehouseId == warehouseId, cancellationToken);

    public Task<int> CountOpenByLocationAsync(int locationId, CancellationToken cancellationToken) =>
        Adjustments.CountAsync(
            a => DocumentStatuses.Open.Contains(a.Status) && a.Lines.Any(line => line.LocationId == locationId),
            cancellationToken);
}
