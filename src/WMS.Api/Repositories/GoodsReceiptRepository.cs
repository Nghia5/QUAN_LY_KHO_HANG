using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using WMS.Api.Common;
using WMS.Api.Data;
using WMS.Api.Dtos.Common;
using WMS.Api.Dtos.Documents;
using WMS.Api.Dtos.GoodsReceipts;
using WMS.Api.Models.Documents;
using WMS.Api.Services.Documents;

namespace WMS.Api.Repositories;

public interface IGoodsReceiptRepository
{
    Task<PagedResult<GoodsReceiptSummary>> SearchAsync(GoodsReceiptQuery query, CancellationToken cancellationToken);

    Task<GoodsReceiptDetail?> GetDetailAsync(int id, CancellationToken cancellationToken);

    /// <summary>Tracked receipt with its lines.</summary>
    Task<GoodsReceipt?> FindAsync(int id, CancellationToken cancellationToken);

    void Add(GoodsReceipt receipt);

    void Remove(GoodsReceipt receipt);

    Task<int> CountOpenByWarehouseAsync(int warehouseId, CancellationToken cancellationToken);

    Task<int> CountByWarehouseAsync(int warehouseId, CancellationToken cancellationToken);

    Task<int> CountOpenByLocationAsync(int locationId, CancellationToken cancellationToken);
}

public sealed class GoodsReceiptRepository(WmsDbContext dbContext) : IGoodsReceiptRepository
{
    private static readonly DocumentStatus[] OpenStatuses = DocumentStatuses.Open;

    private static readonly IReadOnlyDictionary<string, Expression<Func<GoodsReceipt, object?>>> SortMap =
        QueryableExtensions.SortMap<GoodsReceipt>(
            ("documentNo", receipt => receipt.DocumentNo),
            ("receiptDate", receipt => receipt.ReceiptDate),
            ("status", receipt => receipt.Status),
            ("createdAt", receipt => receipt.CreatedAt),
            ("totalQuantity", receipt => receipt.Lines.Sum(line => line.Quantity)));

    private IQueryable<GoodsReceipt> Receipts => dbContext.Set<GoodsReceipt>().AsNoTracking();

    public Task<PagedResult<GoodsReceiptSummary>> SearchAsync(GoodsReceiptQuery query, CancellationToken cancellationToken)
    {
        var receipts = Receipts;

        if (query.WarehouseId is { } warehouseId)
        {
            receipts = receipts.Where(receipt => receipt.WarehouseId == warehouseId);
        }

        if (query.Status is { } status)
        {
            receipts = receipts.Where(receipt => receipt.Status == status);
        }

        if (query.SupplierId is { } supplierId)
        {
            receipts = receipts.Where(receipt => receipt.SupplierId == supplierId);
        }

        if (query.ProductId is { } productId)
        {
            receipts = receipts.Where(receipt => receipt.Lines.Any(line => line.ProductId == productId));
        }

        if (query.FromDate is { } fromDate)
        {
            receipts = receipts.Where(receipt => receipt.ReceiptDate >= fromDate);
        }

        if (query.ToDate is { } toDate)
        {
            receipts = receipts.Where(receipt => receipt.ReceiptDate <= toDate);
        }

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var keyword = query.Keyword.Trim();
            receipts = receipts.Where(receipt =>
                receipt.DocumentNo.Contains(keyword)
                || (receipt.ReferenceNo != null && receipt.ReferenceNo.Contains(keyword))
                || (receipt.SupplierName != null && receipt.SupplierName.Contains(keyword)));
        }

        var sorted = query.SortBy is null
            ? receipts.OrderByDescending(receipt => receipt.Id)
            : receipts.ApplySort(query, SortMap, "createdAt", receipt => receipt.Id);

        return sorted
            .Select(receipt => new GoodsReceiptSummary(
                receipt.Id,
                receipt.DocumentNo,
                receipt.WarehouseId,
                receipt.Warehouse!.Code,
                receipt.Status,
                receipt.SupplierId,
                receipt.SupplierName,
                receipt.ReferenceNo,
                receipt.ReceiptDate,
                receipt.Lines.Count,
                receipt.Lines.Sum(line => line.Quantity),
                receipt.CreatedBy,
                receipt.CreatedAt))
            .ToPagedResultAsync(query, cancellationToken);
    }

    public async Task<GoodsReceiptDetail?> GetDetailAsync(int id, CancellationToken cancellationToken)
    {
        var receipt = await Receipts
            .Where(r => r.Id == id)
            .Select(r => new
            {
                r.Id,
                r.DocumentNo,
                r.WarehouseId,
                WarehouseCode = r.Warehouse!.Code,
                WarehouseName = r.Warehouse.Name,
                r.Status,
                r.SupplierId,
                r.SupplierName,
                r.ReferenceNo,
                r.ReceiptDate,
                r.Note,
                Workflow = new DocumentWorkflowInfo(
                    r.CreatedBy, r.CreatedAt, r.SubmittedBy, r.SubmittedAt, r.ApprovedBy, r.ApprovedAt,
                    r.RejectedBy, r.RejectedAt, r.RejectionReason, r.CompletedBy, r.CompletedAt,
                    r.CancelledBy, r.CancelledAt, r.CancellationReason),
                Lines = r.Lines
                    .OrderBy(line => line.Id)
                    .Select(line => new GoodsReceiptLineResponse(
                        line.Id,
                        line.ProductId,
                        line.Product!.Sku,
                        line.Product.Name,
                        line.Product.UnitOfMeasure,
                        line.LocationId,
                        line.Location!.Code,
                        line.Quantity,
                        line.UnitCost,
                        line.BatchCode,
                        line.ManufactureDate,
                        line.ExpiryDate,
                        line.Note))
                    .ToList()
            })
            .AsSplitQuery()
            .SingleOrDefaultAsync(cancellationToken);

        return receipt is null
            ? null
            : new GoodsReceiptDetail(
                receipt.Id,
                receipt.DocumentNo,
                receipt.WarehouseId,
                receipt.WarehouseCode,
                receipt.WarehouseName,
                receipt.Status,
                receipt.SupplierId,
                receipt.SupplierName,
                receipt.ReferenceNo,
                receipt.ReceiptDate,
                receipt.Note,
                receipt.Lines.Sum(line => line.Quantity),
                receipt.Lines,
                receipt.Workflow,
                DocumentWorkflow.AvailableActions(receipt.Status));
    }

    public Task<GoodsReceipt?> FindAsync(int id, CancellationToken cancellationToken) =>
        dbContext.Set<GoodsReceipt>()
            .Include(receipt => receipt.Lines)
            .SingleOrDefaultAsync(receipt => receipt.Id == id, cancellationToken);

    public void Add(GoodsReceipt receipt) => dbContext.Set<GoodsReceipt>().Add(receipt);

    public void Remove(GoodsReceipt receipt) => dbContext.Set<GoodsReceipt>().Remove(receipt);

    public Task<int> CountOpenByWarehouseAsync(int warehouseId, CancellationToken cancellationToken) =>
        Receipts.CountAsync(r => r.WarehouseId == warehouseId && OpenStatuses.Contains(r.Status), cancellationToken);

    public Task<int> CountByWarehouseAsync(int warehouseId, CancellationToken cancellationToken) =>
        Receipts.CountAsync(r => r.WarehouseId == warehouseId, cancellationToken);

    public Task<int> CountOpenByLocationAsync(int locationId, CancellationToken cancellationToken) =>
        dbContext.Set<GoodsReceiptLine>()
            .AsNoTracking()
            .Where(line => line.LocationId == locationId)
            .Join(dbContext.Set<GoodsReceipt>(), line => line.GoodsReceiptId, receipt => receipt.Id, (_, receipt) => receipt)
            .Where(receipt => OpenStatuses.Contains(receipt.Status))
            .Select(receipt => receipt.Id)
            .Distinct()
            .CountAsync(cancellationToken);
}
