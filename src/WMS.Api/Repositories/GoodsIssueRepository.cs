using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using WMS.Api.Common;
using WMS.Api.Data;
using WMS.Api.Dtos.Common;
using WMS.Api.Dtos.Documents;
using WMS.Api.Dtos.GoodsIssues;
using WMS.Api.Models;
using WMS.Api.Models.Documents;
using WMS.Api.Services.Documents;

namespace WMS.Api.Repositories;

public interface IGoodsIssueRepository
{
    Task<PagedResult<GoodsIssueSummary>> SearchAsync(GoodsIssueQuery query, CancellationToken cancellationToken);

    Task<GoodsIssueDetail?> GetDetailAsync(int id, CancellationToken cancellationToken);

    /// <summary>Tracked issue with its lines.</summary>
    Task<GoodsIssue?> FindAsync(int id, CancellationToken cancellationToken);

    void Add(GoodsIssue issue);

    Task<int> CountOpenByWarehouseAsync(int warehouseId, CancellationToken cancellationToken);

    Task<int> CountByWarehouseAsync(int warehouseId, CancellationToken cancellationToken);

    Task<int> CountOpenByLocationAsync(int locationId, CancellationToken cancellationToken);
}

public sealed class GoodsIssueRepository(WmsDbContext dbContext) : IGoodsIssueRepository
{
    private static readonly IReadOnlyDictionary<string, Expression<Func<GoodsIssue, object?>>> SortMap =
        QueryableExtensions.SortMap<GoodsIssue>(
            ("documentNo", issue => issue.DocumentNo),
            ("issueDate", issue => issue.IssueDate),
            ("status", issue => issue.Status),
            ("createdAt", issue => issue.CreatedAt),
            ("totalQuantity", issue => issue.Lines.Sum(line => line.Quantity)));

    private IQueryable<GoodsIssue> Issues => dbContext.Set<GoodsIssue>().AsNoTracking();

    public Task<PagedResult<GoodsIssueSummary>> SearchAsync(GoodsIssueQuery query, CancellationToken cancellationToken)
    {
        var issues = Issues;

        if (query.WarehouseId is { } warehouseId)
        {
            issues = issues.Where(issue => issue.WarehouseId == warehouseId);
        }

        if (query.Status is { } status)
        {
            issues = issues.Where(issue => issue.Status == status);
        }

        if (query.CustomerId is { } customerId)
        {
            issues = issues.Where(issue => issue.CustomerId == customerId);
        }

        if (query.ProductId is { } productId)
        {
            issues = issues.Where(issue => issue.Lines.Any(line => line.ProductId == productId));
        }

        if (query.FromDate is { } fromDate)
        {
            issues = issues.Where(issue => issue.IssueDate >= fromDate);
        }

        if (query.ToDate is { } toDate)
        {
            issues = issues.Where(issue => issue.IssueDate <= toDate);
        }

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var keyword = query.Keyword.Trim();
            issues = issues.Where(issue =>
                issue.DocumentNo.Contains(keyword)
                || (issue.ReferenceNo != null && issue.ReferenceNo.Contains(keyword))
                || (issue.CustomerName != null && issue.CustomerName.Contains(keyword)));
        }

        var sorted = query.SortBy is null
            ? issues.OrderByDescending(issue => issue.Id)
            : issues.ApplySort(query, SortMap, "createdAt", issue => issue.Id);

        return sorted
            .Select(issue => new GoodsIssueSummary(
                issue.Id,
                issue.DocumentNo,
                issue.WarehouseId,
                issue.Warehouse!.Code,
                issue.Status,
                issue.CustomerId,
                issue.CustomerName,
                issue.ReferenceNo,
                issue.IssueDate,
                issue.Lines.Count,
                issue.Lines.Sum(line => line.Quantity),
                issue.CreatedBy,
                issue.CreatedAt))
            .ToPagedResultAsync(query, cancellationToken);
    }

    public async Task<GoodsIssueDetail?> GetDetailAsync(int id, CancellationToken cancellationToken)
    {
        var inventories = dbContext.Set<Inventory>();
        var issue = await Issues
            .Where(i => i.Id == id)
            .Select(i => new
            {
                i.Id,
                i.DocumentNo,
                i.WarehouseId,
                WarehouseCode = i.Warehouse!.Code,
                WarehouseName = i.Warehouse.Name,
                i.Status,
                i.CustomerId,
                i.CustomerName,
                i.ReferenceNo,
                i.IssueDate,
                i.Note,
                Workflow = new DocumentWorkflowInfo(
                    i.CreatedBy, i.CreatedAt, i.SubmittedBy, i.SubmittedAt, i.ApprovedBy, i.ApprovedAt,
                    i.RejectedBy, i.RejectedAt, i.RejectionReason, i.CompletedBy, i.CompletedAt,
                    i.CancelledBy, i.CancelledAt, i.CancellationReason),
                Lines = i.Lines
                    .OrderBy(line => line.Id)
                    .Select(line => new GoodsIssueLineResponse(
                        line.Id,
                        line.ProductId,
                        line.Product!.Sku,
                        line.Product.Name,
                        line.Product.UnitOfMeasure,
                        line.LocationId,
                        line.Location!.Code,
                        line.Quantity,
                        inventories
                            .Where(inv => inv.ProductId == line.ProductId && inv.LocationId == line.LocationId)
                            .Select(inv => inv.AvailableQuantity)
                            .FirstOrDefault(),
                        line.Note))
                    .ToList()
            })
            .AsSplitQuery()
            .SingleOrDefaultAsync(cancellationToken);

        return issue is null
            ? null
            : new GoodsIssueDetail(
                issue.Id,
                issue.DocumentNo,
                issue.WarehouseId,
                issue.WarehouseCode,
                issue.WarehouseName,
                issue.Status,
                issue.CustomerId,
                issue.CustomerName,
                issue.ReferenceNo,
                issue.IssueDate,
                issue.Note,
                issue.Lines.Sum(line => line.Quantity),
                issue.Lines,
                issue.Workflow,
                DocumentWorkflow.AvailableActions(issue.Status));
    }

    public Task<GoodsIssue?> FindAsync(int id, CancellationToken cancellationToken) =>
        dbContext.Set<GoodsIssue>()
            .Include(issue => issue.Lines)
            .SingleOrDefaultAsync(issue => issue.Id == id, cancellationToken);

    public void Add(GoodsIssue issue) => dbContext.Set<GoodsIssue>().Add(issue);

    public Task<int> CountOpenByWarehouseAsync(int warehouseId, CancellationToken cancellationToken) =>
        Issues.CountAsync(i => i.WarehouseId == warehouseId && DocumentStatuses.Open.Contains(i.Status), cancellationToken);

    public Task<int> CountByWarehouseAsync(int warehouseId, CancellationToken cancellationToken) =>
        Issues.CountAsync(i => i.WarehouseId == warehouseId, cancellationToken);

    public Task<int> CountOpenByLocationAsync(int locationId, CancellationToken cancellationToken) =>
        Issues.CountAsync(
            issue => DocumentStatuses.Open.Contains(issue.Status) && issue.Lines.Any(line => line.LocationId == locationId),
            cancellationToken);
}
