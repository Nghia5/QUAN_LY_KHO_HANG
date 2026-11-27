using WMS.Api.Authorization;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using WMS.Api.Common;
using WMS.Api.Data;
using WMS.Api.Dtos.Common;
using WMS.Api.Dtos.Documents;
using WMS.Api.Dtos.Stocktakes;
using WMS.Api.Models.Documents;
using WMS.Api.Services.Documents;

namespace WMS.Api.Repositories;

public interface IStocktakeRepository
{
    Task<PagedResult<StocktakeSummary>> SearchAsync(StocktakeQuery query, CancellationToken cancellationToken);

    /// <param name="differencesOnly">Only lines whose counted quantity differs from the snapshot (COMPARE view).</param>
    Task<StocktakeDetail?> GetDetailAsync(int id, bool differencesOnly, CancellationToken cancellationToken);

    /// <summary>Tracked stocktake with its lines.</summary>
    Task<Stocktake?> FindAsync(int id, CancellationToken cancellationToken);

    void Add(Stocktake stocktake);

    Task<int> CountOpenByWarehouseAsync(int warehouseId, CancellationToken cancellationToken);

    Task<int> CountByWarehouseAsync(int warehouseId, CancellationToken cancellationToken);

    Task<int> CountOpenByLocationAsync(int locationId, CancellationToken cancellationToken);
}

public sealed class StocktakeRepository(WmsDbContext dbContext, WarehouseAccess? access = null) : IStocktakeRepository
{
    private static readonly IReadOnlyDictionary<string, Expression<Func<Stocktake, object?>>> SortMap =
        QueryableExtensions.SortMap<Stocktake>(
            ("documentNo", stocktake => stocktake.DocumentNo),
            ("status", stocktake => stocktake.Status),
            ("createdAt", stocktake => stocktake.CreatedAt));

    private IQueryable<Stocktake> Stocktakes => dbContext.Set<Stocktake>().AsNoTracking();

    public Task<PagedResult<StocktakeSummary>> SearchAsync(StocktakeQuery query, CancellationToken cancellationToken)
    {
        var stocktakes = access?.Filter(Stocktakes, d => d.WarehouseId) ?? Stocktakes;

        if (query.WarehouseId is { } warehouseId)
        {
            stocktakes = stocktakes.Where(s => s.WarehouseId == warehouseId);
        }

        if (query.Status is { } status)
        {
            stocktakes = stocktakes.Where(s => s.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var keyword = query.Keyword.Trim();
            stocktakes = stocktakes.Where(s => s.DocumentNo.Contains(keyword) || (s.Note != null && s.Note.Contains(keyword)));
        }

        var sorted = query.SortBy is null
            ? stocktakes.OrderByDescending(s => s.Id)
            : stocktakes.ApplySort(query, SortMap, "createdAt", s => s.Id);

        return sorted
            .Select(s => new StocktakeSummary(
                s.Id,
                s.DocumentNo,
                s.WarehouseId,
                s.Warehouse!.Code,
                s.Status,
                s.Lines.Count,
                s.Lines.Count(line => line.CountedQuantity != null),
                s.Lines.Count(line => line.Difference != null && line.Difference != 0),
                s.CreatedBy,
                s.CreatedAt))
            .ToPagedResultAsync(query, cancellationToken);
    }

    public async Task<StocktakeDetail?> GetDetailAsync(int id, bool differencesOnly, CancellationToken cancellationToken)
    {
        var stocktake = await Stocktakes
            .Where(s => s.Id == id)
            .Select(s => new
            {
                s.Id,
                s.DocumentNo,
                s.WarehouseId,
                WarehouseCode = s.Warehouse!.Code,
                s.Status,
                s.ScopeLocationIds,
                s.Note,
                s.SnapshotBy,
                s.SnapshotAt,
                s.AdjustedBy,
                s.AdjustedAt,
                Workflow = new DocumentWorkflowInfo(
                    s.CreatedBy, s.CreatedAt, s.SubmittedBy, s.SubmittedAt, s.ApprovedBy, s.ApprovedAt,
                    s.RejectedBy, s.RejectedAt, s.RejectionReason, s.CompletedBy, s.CompletedAt,
                    s.CancelledBy, s.CancelledAt, s.CancellationReason),
                Lines = s.Lines
                    .OrderBy(line => line.Location!.Code).ThenBy(line => line.Product!.Sku)
                    .Select(line => new StocktakeLineResponse(
                        line.Id,
                        line.ProductId,
                        line.Product!.Sku,
                        line.Product.Name,
                        line.Product.UnitOfMeasure,
                        line.LocationId,
                        line.Location!.Code,
                        line.SystemQuantity,
                        line.CountedQuantity,
                        line.Difference,
                        line.IsAddedDuringCount,
                        line.CountedBy,
                        line.CountedAt,
                        line.Note))
                    .ToList()
            })
            .AsSplitQuery()
            .SingleOrDefaultAsync(cancellationToken);

        if (stocktake is null)
        {
            return null;
        }

        var lines = stocktake.Lines;
        var totals = new StocktakeTotals(
            lines.Count,
            lines.Count(line => line.CountedQuantity is not null),
            lines.Count(line => line.CountedQuantity is null),
            lines.Count(line => line.Difference is { } difference && difference != 0),
            lines.Where(line => line.Difference > 0).Sum(line => line.Difference!.Value),
            -lines.Where(line => line.Difference < 0).Sum(line => line.Difference!.Value));

        return new StocktakeDetail(
            stocktake.Id,
            stocktake.DocumentNo,
            stocktake.WarehouseId,
            stocktake.WarehouseCode,
            stocktake.Status,
            stocktake.ScopeLocationIds,
            stocktake.Note,
            stocktake.SnapshotBy,
            stocktake.SnapshotAt,
            stocktake.AdjustedBy,
            stocktake.AdjustedAt,
            totals,
            differencesOnly ? lines.Where(line => line.Difference is { } d && d != 0).ToList() : lines,
            stocktake.Workflow,
            DocumentWorkflow.AvailableActions(stocktake.Status, DocumentFlow.Stocktake));
    }

    public Task<Stocktake?> FindAsync(int id, CancellationToken cancellationToken) =>
        dbContext.Set<Stocktake>()
            .Include(stocktake => stocktake.Lines)
            .SingleOrDefaultAsync(stocktake => stocktake.Id == id, cancellationToken);

    public void Add(Stocktake stocktake) => dbContext.Set<Stocktake>().Add(stocktake);

    public Task<int> CountOpenByWarehouseAsync(int warehouseId, CancellationToken cancellationToken) =>
        Stocktakes.CountAsync(s => s.WarehouseId == warehouseId && DocumentStatuses.Open.Contains(s.Status), cancellationToken);

    public Task<int> CountByWarehouseAsync(int warehouseId, CancellationToken cancellationToken) =>
        Stocktakes.CountAsync(s => s.WarehouseId == warehouseId, cancellationToken);

    public Task<int> CountOpenByLocationAsync(int locationId, CancellationToken cancellationToken) =>
        Stocktakes.CountAsync(
            s => DocumentStatuses.Open.Contains(s.Status) && s.Lines.Any(line => line.LocationId == locationId),
            cancellationToken);
}
