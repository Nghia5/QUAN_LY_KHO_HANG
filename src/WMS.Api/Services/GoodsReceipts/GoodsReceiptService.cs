using Microsoft.EntityFrameworkCore;
using WMS.Api.Common;
using WMS.Api.Constants;
using WMS.Api.Data;
using WMS.Api.Dtos.Common;
using WMS.Api.Dtos.GoodsReceipts;
using WMS.Api.Exceptions;
using WMS.Api.Models;
using WMS.Api.Models.Documents;
using WMS.Api.Repositories;
using WMS.Api.Services.Audit;
using WMS.Api.Services.Documents;
using WMS.Api.Services.Inventories;

namespace WMS.Api.Services.GoodsReceipts;

public interface IGoodsReceiptService
{
    Task<PagedResult<GoodsReceiptSummary>> SearchAsync(GoodsReceiptQuery query, CancellationToken cancellationToken);

    Task<GoodsReceiptDetail> GetAsync(int id, CancellationToken cancellationToken);

    Task<GoodsReceiptDetail> CreateAsync(CreateGoodsReceiptRequest request, CancellationToken cancellationToken);

    Task<GoodsReceiptDetail> UpdateAsync(int id, UpdateGoodsReceiptRequest request, CancellationToken cancellationToken);

    Task DeleteAsync(int id, CancellationToken cancellationToken);

    Task<GoodsReceiptDetail> SubmitAsync(int id, CancellationToken cancellationToken);

    Task<GoodsReceiptDetail> ApproveAsync(int id, CancellationToken cancellationToken);

    Task<GoodsReceiptDetail> RejectAsync(int id, string reason, CancellationToken cancellationToken);

    Task<GoodsReceiptDetail> ReopenAsync(int id, CancellationToken cancellationToken);

    /// <summary>Posts the receipt: increases stock and writes movements + audit in one transaction.</summary>
    Task<GoodsReceiptDetail> CompleteAsync(int id, CancellationToken cancellationToken);

    Task<GoodsReceiptDetail> CancelAsync(int id, string reason, CancellationToken cancellationToken);
}

public sealed class GoodsReceiptService(
    WmsDbContext dbContext,
    IGoodsReceiptRepository repository,
    IDocumentNumberGenerator numberGenerator,
    DocumentWorkflow workflow,
    IStockLedger ledger,
    IAuditLogger audit,
    ICurrentUser currentUser,
    TimeProvider timeProvider) : IGoodsReceiptService
{
    private const string AuditEntity = "GoodsReceipt";

    public Task<PagedResult<GoodsReceiptSummary>> SearchAsync(GoodsReceiptQuery query, CancellationToken cancellationToken) =>
        repository.SearchAsync(query, cancellationToken);

    public async Task<GoodsReceiptDetail> GetAsync(int id, CancellationToken cancellationToken) =>
        await repository.GetDetailAsync(id, cancellationToken) ?? throw new NotFoundException("Goods receipt", id);

    public async Task<GoodsReceiptDetail> CreateAsync(CreateGoodsReceiptRequest request, CancellationToken cancellationToken)
    {
        var receiptDate = request.ReceiptDate ?? Today;
        await ValidateAsync(request.WarehouseId, receiptDate, request.Lines, cancellationToken);

        var receipt = new GoodsReceipt
        {
            DocumentNo = await numberGenerator.NextAsync(DocumentTypes.GoodsReceipt, cancellationToken),
            WarehouseId = request.WarehouseId
        };
        Apply(receipt, request, receiptDate);
        workflow.Created(receipt);

        await using (var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken))
        {
            repository.Add(receipt);
            await SaveAsync(cancellationToken); // receipt first, to know its Id for the audit entry
            audit.Log(AuditEntity, receipt.Id, "CREATED", new { receipt.DocumentNo, LineCount = receipt.Lines.Count });
            await SaveAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        return await GetAsync(receipt.Id, cancellationToken);
    }

    public async Task<GoodsReceiptDetail> UpdateAsync(int id, UpdateGoodsReceiptRequest request, CancellationToken cancellationToken)
    {
        var receipt = await FindAsync(id, cancellationToken);
        workflow.EnsureCan(receipt, DocumentAction.Edit);

        var receiptDate = request.ReceiptDate ?? receipt.ReceiptDate;
        await ValidateAsync(receipt.WarehouseId, receiptDate, request.Lines, cancellationToken);

        receipt.Lines.Clear(); // lines of a draft are replaced as a whole
        Apply(receipt, request, receiptDate);
        workflow.Stamp(receipt);
        audit.Log(AuditEntity, receipt.Id, "UPDATED", new { receipt.DocumentNo, LineCount = receipt.Lines.Count });
        await SaveAsync(cancellationToken);

        return await GetAsync(id, cancellationToken);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken)
    {
        var receipt = await FindAsync(id, cancellationToken);
        workflow.EnsureCan(receipt, DocumentAction.Delete);

        audit.Log(AuditEntity, receipt.Id, "DELETED", new { receipt.DocumentNo });
        repository.Remove(receipt);
        await SaveAsync(cancellationToken);
    }

    public async Task<GoodsReceiptDetail> SubmitAsync(int id, CancellationToken cancellationToken)
    {
        var receipt = await FindAsync(id, cancellationToken);
        await ValidateAsync(receipt.WarehouseId, receipt.ReceiptDate, receipt.Lines.Select(ToRequest).ToList(), cancellationToken);
        return await TransitionAsync(receipt, "SUBMITTED", workflow.Submit, null, cancellationToken);
    }

    public async Task<GoodsReceiptDetail> ApproveAsync(int id, CancellationToken cancellationToken) =>
        await TransitionAsync(await FindAsync(id, cancellationToken), "APPROVED", workflow.Approve, null, cancellationToken);

    public async Task<GoodsReceiptDetail> RejectAsync(int id, string reason, CancellationToken cancellationToken) =>
        await TransitionAsync(await FindAsync(id, cancellationToken), "REJECTED", r => workflow.Reject(r, reason), reason, cancellationToken);

    public async Task<GoodsReceiptDetail> ReopenAsync(int id, CancellationToken cancellationToken) =>
        await TransitionAsync(await FindAsync(id, cancellationToken), "REOPENED", workflow.Reopen, null, cancellationToken);

    public async Task<GoodsReceiptDetail> CancelAsync(int id, string reason, CancellationToken cancellationToken)
    {
        var receipt = await FindAsync(id, cancellationToken);
        if (receipt.Status == DocumentStatus.Approved && !currentUser.HasPermission(Permissions.GoodsReceiptApprove))
        {
            throw new ForbiddenException($"Cancelling an approved receipt requires {Permissions.GoodsReceiptApprove}.");
        }

        return await TransitionAsync(receipt, "CANCELLED", r => workflow.Cancel(r, reason), reason, cancellationToken);
    }

    public async Task<GoodsReceiptDetail> CompleteAsync(int id, CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var receipt = await FindAsync(id, cancellationToken);
        workflow.Complete(receipt);

        // Master data may have changed since approval (product/location deactivated...): re-check.
        await ValidateAsync(receipt.WarehouseId, receipt.ReceiptDate, receipt.Lines.Select(ToRequest).ToList(), cancellationToken);

        // Saving the status first takes the row lock and checks RowVersion, so a second
        // concurrent "complete" of the same receipt fails here instead of posting stock twice.
        await SaveAsync(cancellationToken);

        var movements = await ledger.ApplyAsync(
            receipt.Lines
                .Select(line => new StockChange(
                    line.ProductId,
                    line.WarehouseId,
                    line.LocationId,
                    line.Quantity,
                    MovementType.Receipt,
                    line.BatchCode is null ? line.Note : $"Batch {line.BatchCode}"))
                .ToList(),
            new StockReference(DocumentTypes.GoodsReceipt, receipt.Id, receipt.DocumentNo),
            cancellationToken);

        audit.Log(AuditEntity, receipt.Id, "COMPLETED", new
        {
            receipt.DocumentNo,
            TotalQuantity = receipt.Lines.Sum(line => line.Quantity),
            MovementIds = movements.Select(movement => movement.Id)
        });
        await SaveAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    private async Task<GoodsReceiptDetail> TransitionAsync(
        GoodsReceipt receipt,
        string action,
        Action<GoodsReceipt> transition,
        string? reason,
        CancellationToken cancellationToken)
    {
        transition(receipt);
        audit.Log(AuditEntity, receipt.Id, action, new { receipt.DocumentNo, Status = receipt.Status.ToString(), Reason = reason });
        await SaveAsync(cancellationToken);
        return await GetAsync(receipt.Id, cancellationToken);
    }

    private static void Apply(GoodsReceipt receipt, IGoodsReceiptPayload request, DateOnly receiptDate)
    {
        receipt.SupplierId = request.SupplierId;
        receipt.SupplierName = TrimToNull(request.SupplierName);
        receipt.ReferenceNo = TrimToNull(request.ReferenceNo);
        receipt.ReceiptDate = receiptDate;
        receipt.Note = TrimToNull(request.Note);
        receipt.Lines.AddRange(request.Lines.Select(line => new GoodsReceiptLine
        {
            WarehouseId = receipt.WarehouseId,
            ProductId = line.ProductId,
            LocationId = line.LocationId,
            Quantity = line.Quantity,
            UnitCost = line.UnitCost,
            BatchCode = NormalizeBatch(line.BatchCode),
            ManufactureDate = line.ManufactureDate,
            ExpiryDate = line.ExpiryDate,
            Note = TrimToNull(line.Note)
        }));
    }

    /// <summary>
    /// Database-dependent rules; all problems are collected and reported together with line numbers.
    /// </summary>
    private async Task ValidateAsync(
        int warehouseId,
        DateOnly receiptDate,
        IReadOnlyList<GoodsReceiptLineRequest> lines,
        CancellationToken cancellationToken)
    {
        var warehouse = await dbContext.Set<Warehouse>()
            .AsNoTracking()
            .Where(w => w.Id == warehouseId)
            .Select(w => new { w.Code, w.IsActive })
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new BusinessRuleException($"Warehouse {warehouseId} does not exist.");
        if (!warehouse.IsActive)
        {
            throw new BusinessRuleException($"Warehouse '{warehouse.Code}' is inactive.");
        }

        var productIds = lines.Select(line => line.ProductId).Distinct().ToList();
        var products = await dbContext.Set<Product>()
            .AsNoTracking()
            .Where(p => productIds.Contains(p.Id))
            .Select(p => new { p.Id, p.Sku, p.IsActive })
            .ToDictionaryAsync(p => p.Id, cancellationToken);

        var locationIds = lines.Select(line => line.LocationId).Distinct().ToList();
        var locations = await dbContext.Set<WarehouseLocation>()
            .AsNoTracking()
            .Where(l => locationIds.Contains(l.Id) && l.WarehouseId == warehouseId)
            .Select(l => new { l.Id, l.Code, l.Type, l.IsActive })
            .ToDictionaryAsync(l => l.Id, cancellationToken);

        var errors = new List<string>();
        for (var index = 0; index < lines.Count; index++)
        {
            var line = lines[index];
            var prefix = $"Line {index + 1}:";

            if (!products.TryGetValue(line.ProductId, out var product))
            {
                errors.Add($"{prefix} product {line.ProductId} does not exist.");
            }
            else if (!product.IsActive)
            {
                errors.Add($"{prefix} product '{product.Sku}' is inactive.");
            }

            if (!locations.TryGetValue(line.LocationId, out var location))
            {
                errors.Add($"{prefix} location {line.LocationId} does not belong to warehouse '{warehouse.Code}'.");
            }
            else if (location.Type != LocationType.Bin)
            {
                errors.Add($"{prefix} '{location.Code}' is a {location.Type}; goods can only be received into a Bin.");
            }
            else if (!location.IsActive)
            {
                errors.Add($"{prefix} location '{location.Code}' is inactive.");
            }

            if (line.ManufactureDate is { } manufactured && manufactured > receiptDate)
            {
                errors.Add($"{prefix} manufacture date cannot be after the receipt date.");
            }

            if (line.ExpiryDate is { } expiry && expiry <= receiptDate)
            {
                errors.Add($"{prefix} goods are already expired on the receipt date ({expiry:yyyy-MM-dd}).");
            }
        }

        var duplicates = lines
            .Select((line, index) => (Key: (line.ProductId, line.LocationId, Batch: NormalizeBatch(line.BatchCode)), Number: index + 1))
            .GroupBy(item => item.Key)
            .Where(group => group.Count() > 1)
            .Select(group => $"Lines {string.Join(", ", group.Select(item => item.Number))} repeat the same product, location and batch; merge them.");
        errors.AddRange(duplicates);

        if (errors.Count > 0)
        {
            throw new BusinessRuleException(string.Join(" ", errors));
        }
    }

    private async Task<GoodsReceipt> FindAsync(int id, CancellationToken cancellationToken) =>
        await repository.FindAsync(id, cancellationToken) ?? throw new NotFoundException("Goods receipt", id);

    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException("The receipt was changed by someone else. Reload it and try again.");
        }
    }

    private static GoodsReceiptLineRequest ToRequest(GoodsReceiptLine line) => new()
    {
        ProductId = line.ProductId,
        LocationId = line.LocationId,
        Quantity = line.Quantity,
        UnitCost = line.UnitCost,
        BatchCode = line.BatchCode,
        ManufactureDate = line.ManufactureDate,
        ExpiryDate = line.ExpiryDate,
        Note = line.Note
    };

    private DateOnly Today => DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);

    private static string? NormalizeBatch(string? batch) =>
        string.IsNullOrWhiteSpace(batch) ? null : batch.Trim().ToUpperInvariant();

    private static string? TrimToNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
