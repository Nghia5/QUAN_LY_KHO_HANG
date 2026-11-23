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
using WMS.Api.Services.Batches;
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
    IBatchService batches,
    IAuditLogger audit,
    ICurrentUser currentUser,
    TimeProvider timeProvider)
    : DocumentServiceBase<GoodsReceipt, GoodsReceiptDetail>(dbContext, workflow, audit, currentUser), IGoodsReceiptService
{
    protected override string AuditEntity => "GoodsReceipt";

    protected override string DisplayName => "Goods receipt";

    protected override string ApprovePermission => Permissions.GoodsReceiptApprove;

    protected override Task<GoodsReceipt?> LoadAsync(int id, CancellationToken cancellationToken) =>
        repository.FindAsync(id, cancellationToken);

    public Task<PagedResult<GoodsReceiptSummary>> SearchAsync(GoodsReceiptQuery query, CancellationToken cancellationToken) =>
        repository.SearchAsync(query, cancellationToken);

    public override async Task<GoodsReceiptDetail> GetAsync(int id, CancellationToken cancellationToken) =>
        await repository.GetDetailAsync(id, cancellationToken) ?? throw new NotFoundException(DisplayName, id);

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
        Workflow.Created(receipt);

        await using (var transaction = await DbContext.Database.BeginTransactionAsync(cancellationToken))
        {
            repository.Add(receipt);
            await SaveAsync(cancellationToken); // receipt first, to know its Id for the audit entry
            Audit.Log(AuditEntity, receipt.Id, "CREATED", new { receipt.DocumentNo, LineCount = receipt.Lines.Count });
            await SaveAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        return await GetAsync(receipt.Id, cancellationToken);
    }

    public async Task<GoodsReceiptDetail> UpdateAsync(int id, UpdateGoodsReceiptRequest request, CancellationToken cancellationToken)
    {
        var receipt = await FindAsync(id, cancellationToken);
        Workflow.EnsureCan(receipt, DocumentAction.Edit);

        var receiptDate = request.ReceiptDate ?? receipt.ReceiptDate;
        await ValidateAsync(receipt.WarehouseId, receiptDate, request.Lines, cancellationToken);

        receipt.Lines.Clear(); // lines of a draft are replaced as a whole
        Apply(receipt, request, receiptDate);
        Workflow.Stamp(receipt);
        Audit.Log(AuditEntity, receipt.Id, "UPDATED", new { receipt.DocumentNo, LineCount = receipt.Lines.Count });
        await SaveAsync(cancellationToken);

        return await GetAsync(id, cancellationToken);
    }

    protected override Task BeforeSubmitAsync(GoodsReceipt receipt, CancellationToken cancellationToken) =>
        ValidateAsync(receipt.WarehouseId, receipt.ReceiptDate, receipt.Lines.Select(ToRequest).ToList(), cancellationToken);

    // Master data may have changed since approval (product/location deactivated...): re-check.
    protected override Task BeforeCompleteAsync(GoodsReceipt receipt, CancellationToken cancellationToken) =>
        ValidateAsync(receipt.WarehouseId, receipt.ReceiptDate, receipt.Lines.Select(ToRequest).ToList(), cancellationToken);

    protected override async Task<object?> OnCompletingAsync(GoodsReceipt receipt, CancellationToken cancellationToken)
    {
        // Lines with a batch code create (or reuse) the batch and put the quantity into that lot.
        var changes = new List<StockChange>(receipt.Lines.Count);
        foreach (var line in receipt.Lines)
        {
            int? batchId = line.BatchCode is null
                ? null
                : (await batches.EnsureAsync(line.ProductId, line.BatchCode, line.ManufactureDate, line.ExpiryDate, cancellationToken)).Id;

            changes.Add(new StockChange(
                line.ProductId,
                line.WarehouseId,
                line.LocationId,
                line.Quantity,
                MovementType.Receipt,
                line.BatchCode is null ? line.Note : $"Batch {line.BatchCode}",
                BatchId: batchId));
        }

        var movements = await ledger.ApplyAsync(
            changes,
            new StockReference(DocumentTypes.GoodsReceipt, receipt.Id, receipt.DocumentNo),
            cancellationToken);

        return new
        {
            TotalQuantity = receipt.Lines.Sum(line => line.Quantity),
            MovementIds = movements.Select(movement => movement.Id)
        };
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
        var context = await DocumentLineValidator.LoadAsync(
            DbContext, warehouseId, lines.Select(line => (line.ProductId, line.LocationId)).ToList(), cancellationToken);

        var errors = new List<string>();
        for (var index = 0; index < lines.Count; index++)
        {
            var line = lines[index];
            var prefix = $"Line {index + 1}:";

            context.CheckProduct(errors, prefix, line.ProductId, requireActive: true);
            context.CheckBin(errors, prefix, line.LocationId, requireActive: true, "goods can only be received into a Bin");

            if (line.ManufactureDate is { } manufactured && manufactured > receiptDate)
            {
                errors.Add($"{prefix} manufacture date cannot be after the receipt date.");
            }

            if (line.ExpiryDate is { } expiry && expiry <= receiptDate)
            {
                errors.Add($"{prefix} goods are already expired on the receipt date ({expiry:yyyy-MM-dd}).");
            }

            // An existing batch keeps its dates: a different date means a different (wrong) batch code.
            if (!string.IsNullOrWhiteSpace(line.BatchCode)
                && await batches.CheckDatesAsync(line.ProductId, line.BatchCode, line.ManufactureDate, line.ExpiryDate, cancellationToken)
                    is { } conflict)
            {
                errors.Add($"{prefix} {conflict}");
            }
        }

        errors.AddRange(DocumentLineValidator.Duplicates(
            lines.Select(line => (object)(line.ProductId, line.LocationId, NormalizeBatch(line.BatchCode))).ToList(),
            "repeat the same product, location and batch; merge them."));

        DocumentLineValidator.ThrowIfAny(errors);
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
