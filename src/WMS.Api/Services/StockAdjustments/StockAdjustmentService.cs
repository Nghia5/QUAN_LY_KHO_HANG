using Microsoft.EntityFrameworkCore;
using WMS.Api.Common;
using WMS.Api.Constants;
using WMS.Api.Data;
using WMS.Api.Dtos.Common;
using WMS.Api.Dtos.StockAdjustments;
using WMS.Api.Exceptions;
using WMS.Api.Models;
using WMS.Api.Models.Documents;
using WMS.Api.Repositories;
using WMS.Api.Services.Audit;
using WMS.Api.Services.Documents;
using WMS.Api.Services.Inventories;

namespace WMS.Api.Services.StockAdjustments;

public interface IStockAdjustmentService
{
    Task<PagedResult<StockAdjustmentSummary>> SearchAsync(StockAdjustmentQuery query, CancellationToken cancellationToken);

    Task<StockAdjustmentDetail> GetAsync(int id, CancellationToken cancellationToken);

    Task<StockAdjustmentDetail> CreateAsync(CreateStockAdjustmentRequest request, CancellationToken cancellationToken);

    Task<StockAdjustmentDetail> UpdateAsync(int id, UpdateStockAdjustmentRequest request, CancellationToken cancellationToken);

    Task DeleteAsync(int id, CancellationToken cancellationToken);

    /// <summary>Draft → PendingApproval, or applied immediately when the policy does not require approval.</summary>
    Task<StockAdjustmentDetail> SubmitAsync(int id, CancellationToken cancellationToken);

    /// <summary>Approves and applies the adjustment (stock + movements + audit) → Completed.</summary>
    Task<StockAdjustmentDetail> ApproveAsync(int id, CancellationToken cancellationToken);

    Task<StockAdjustmentDetail> RejectAsync(int id, string reason, CancellationToken cancellationToken);

    Task<StockAdjustmentDetail> ReopenAsync(int id, CancellationToken cancellationToken);

    Task<StockAdjustmentDetail> CancelAsync(int id, string reason, CancellationToken cancellationToken);
}

public sealed class StockAdjustmentService(
    WmsDbContext dbContext,
    IStockAdjustmentRepository repository,
    IDocumentNumberGenerator numberGenerator,
    DocumentWorkflow workflow,
    IStockLedger ledger,
    IAuditLogger audit,
    ICurrentUser currentUser)
    : DocumentServiceBase<StockAdjustment, StockAdjustmentDetail>(dbContext, workflow, audit, currentUser), IStockAdjustmentService
{
    protected override string AuditEntity => "StockAdjustment";

    protected override string DisplayName => "Stock adjustment";

    protected override string ApprovePermission => Permissions.StockAdjustmentApprove;

    private bool RequiresApproval => Workflow.Requirements.AdjustmentApproval;

    protected override Task<StockAdjustment?> LoadAsync(int id, CancellationToken cancellationToken) =>
        repository.FindAsync(id, cancellationToken);

    public Task<PagedResult<StockAdjustmentSummary>> SearchAsync(StockAdjustmentQuery query, CancellationToken cancellationToken) =>
        repository.SearchAsync(query, cancellationToken);

    public override async Task<StockAdjustmentDetail> GetAsync(int id, CancellationToken cancellationToken) =>
        await repository.GetDetailAsync(id, RequiresApproval, cancellationToken) ?? throw new NotFoundException(DisplayName, id);

    public async Task<StockAdjustmentDetail> CreateAsync(CreateStockAdjustmentRequest request, CancellationToken cancellationToken)
    {
        await ValidateAsync(request.WarehouseId, request.Lines, cancellationToken);

        var adjustment = new StockAdjustment
        {
            DocumentNo = await numberGenerator.NextAsync(DocumentTypes.StockAdjustment, cancellationToken),
            WarehouseId = request.WarehouseId
        };
        Apply(adjustment, request);
        Workflow.Created(adjustment);

        await using (var transaction = await DbContext.Database.BeginTransactionAsync(cancellationToken))
        {
            repository.Add(adjustment);
            await SaveAsync(cancellationToken);
            Audit.Log(AuditEntity, adjustment.Id, "CREATED", new
            {
                adjustment.DocumentNo,
                ReasonCode = adjustment.ReasonCode.ToString(),
                adjustment.Reason,
                LineCount = adjustment.Lines.Count
            });
            await SaveAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        return await GetAsync(adjustment.Id, cancellationToken);
    }

    public async Task<StockAdjustmentDetail> UpdateAsync(int id, UpdateStockAdjustmentRequest request, CancellationToken cancellationToken)
    {
        var adjustment = await FindAsync(id, cancellationToken);
        Workflow.EnsureCan(adjustment, DocumentAction.Edit);
        await ValidateAsync(adjustment.WarehouseId, request.Lines, cancellationToken);

        adjustment.Lines.Clear();
        Apply(adjustment, request);
        Workflow.Stamp(adjustment);
        Audit.Log(AuditEntity, adjustment.Id, "UPDATED", new { adjustment.DocumentNo, LineCount = adjustment.Lines.Count });
        await SaveAsync(cancellationToken);

        return await GetAsync(id, cancellationToken);
    }

    protected override Task BeforeSubmitAsync(StockAdjustment adjustment, CancellationToken cancellationToken) =>
        ValidateAsync(adjustment.WarehouseId, ToRequests(adjustment), cancellationToken);

    /// <summary>Policy without approval: the submit itself approves and applies the adjustment.</summary>
    protected override async Task<object?> OnSubmittedAsync(StockAdjustment adjustment, CancellationToken cancellationToken)
    {
        if (RequiresApproval)
        {
            return null;
        }

        Workflow.ApproveByPolicy(adjustment);
        await SaveAsync(cancellationToken);
        return await ApplyAsync(adjustment, cancellationToken);
    }

    /// <summary>
    /// Approval applies the adjustment right away (spec 4.4) and completes the document; the APPROVED
    /// audit entry then holds status Completed and the before/after quantities of every line.
    /// </summary>
    protected override async Task<object?> OnApprovedAsync(StockAdjustment adjustment, CancellationToken cancellationToken) =>
        await ApplyAsync(adjustment, cancellationToken);

    /// <summary>Adjustments complete on approval; a separate "complete" call is never needed.</summary>
    protected override Task<object?> OnCompletingAsync(StockAdjustment adjustment, CancellationToken cancellationToken) =>
        throw new BusinessRuleException("Stock adjustments are completed automatically when approved.");

    private async Task<object> ApplyAsync(StockAdjustment adjustment, CancellationToken cancellationToken)
    {
        // Re-check against the current stock: other documents may have changed it since submission.
        await ValidateAsync(adjustment.WarehouseId, ToRequests(adjustment), cancellationToken);

        Workflow.Complete(adjustment);
        await SaveAsync(cancellationToken);

        var movements = await ledger.ApplyAsync(
            adjustment.Lines
                .Select(line => new StockChange(
                    line.ProductId,
                    line.WarehouseId,
                    line.LocationId,
                    line.AdjustmentQuantity,
                    line.AdjustmentQuantity > 0 ? MovementType.AdjustmentIn : MovementType.AdjustmentOut,
                    $"{adjustment.ReasonCode}: {adjustment.Reason}"))
                .ToList(),
            new StockReference(DocumentTypes.StockAdjustment, adjustment.Id, adjustment.DocumentNo),
            cancellationToken);

        // Before/After come from the ledger (the locked inventory row), not from an earlier read.
        foreach (var line in adjustment.Lines)
        {
            var movement = movements.Single(m => m.ProductId == line.ProductId && m.LocationId == line.LocationId);
            line.BeforeQuantity = movement.QuantityBefore;
            line.AfterQuantity = movement.QuantityAfter;
        }

        await SaveAsync(cancellationToken);
        return new
        {
            Lines = adjustment.Lines.Select(line => new { line.ProductId, line.LocationId, line.BeforeQuantity, line.AdjustmentQuantity, line.AfterQuantity }),
            MovementIds = movements.Select(movement => movement.Id)
        };
    }

    private static void Apply(StockAdjustment adjustment, IStockAdjustmentPayload request)
    {
        adjustment.ReasonCode = request.ReasonCode;
        adjustment.Reason = request.Reason.Trim();
        adjustment.Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
        adjustment.Lines.AddRange(request.Lines.Select(line => new StockAdjustmentLine
        {
            WarehouseId = adjustment.WarehouseId,
            ProductId = line.ProductId,
            LocationId = line.LocationId,
            AdjustmentQuantity = line.AdjustmentQuantity,
            Note = string.IsNullOrWhiteSpace(line.Note) ? null : line.Note.Trim()
        }));
    }

    private static List<StockAdjustmentLineRequest> ToRequests(StockAdjustment adjustment) =>
        adjustment.Lines.Select(line => new StockAdjustmentLineRequest
        {
            ProductId = line.ProductId,
            LocationId = line.LocationId,
            AdjustmentQuantity = line.AdjustmentQuantity,
            Note = line.Note
        }).ToList();

    /// <summary>
    /// Every line: product exists, location is a bin of the warehouse, and the result is not negative
    /// (current - decrease &gt;= reserved). New stock (increase on an empty bin) needs an active bin.
    /// </summary>
    private async Task ValidateAsync(int warehouseId, IReadOnlyList<StockAdjustmentLineRequest> lines, CancellationToken cancellationToken)
    {
        var context = await DocumentLineValidator.LoadAsync(
            DbContext, warehouseId, lines.Select(line => (line.ProductId, line.LocationId)).ToList(), cancellationToken);

        var productIds = lines.Select(line => line.ProductId).Distinct().ToList();
        var stock = await DbContext.Set<Inventory>()
            .AsNoTracking()
            .Where(i => i.WarehouseId == warehouseId && productIds.Contains(i.ProductId))
            .Select(i => new { i.ProductId, i.LocationId, i.Quantity, i.ReservedQuantity })
            .ToDictionaryAsync(i => (i.ProductId, i.LocationId), cancellationToken);

        var errors = new List<string>();
        for (var index = 0; index < lines.Count; index++)
        {
            var line = lines[index];
            var prefix = $"Line {index + 1}:";
            var current = stock.GetValueOrDefault((line.ProductId, line.LocationId));
            var quantity = current?.Quantity ?? 0m;
            var reserved = current?.ReservedQuantity ?? 0m;

            context.CheckProduct(errors, prefix, line.ProductId, requireActive: false);
            context.CheckBin(errors, prefix, line.LocationId, requireActive: current is null, "stock is kept in Bins");

            if (quantity + line.AdjustmentQuantity < 0)
            {
                errors.Add($"{prefix} stock would become negative ({quantity:0.###} {line.AdjustmentQuantity:+0.###;-0.###}).");
            }
            else if (quantity + line.AdjustmentQuantity < reserved)
            {
                errors.Add($"{prefix} {reserved:0.###} is reserved for approved documents; stock cannot go below it.");
            }
        }

        errors.AddRange(DocumentLineValidator.Duplicates(
            lines.Select(line => (object)(line.ProductId, line.LocationId)).ToList(),
            "repeat the same product and location; merge them."));

        DocumentLineValidator.ThrowIfAny(errors);
    }
}
