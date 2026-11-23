using Microsoft.EntityFrameworkCore;
using WMS.Api.Common;
using WMS.Api.Constants;
using WMS.Api.Data;
using WMS.Api.Dtos.Common;
using WMS.Api.Dtos.GoodsIssues;
using WMS.Api.Exceptions;
using WMS.Api.Models;
using WMS.Api.Models.Documents;
using WMS.Api.Repositories;
using WMS.Api.Services.Audit;
using WMS.Api.Services.Documents;
using WMS.Api.Services.Inventories;

namespace WMS.Api.Services.GoodsIssues;

public interface IGoodsIssueService
{
    Task<PagedResult<GoodsIssueSummary>> SearchAsync(GoodsIssueQuery query, CancellationToken cancellationToken);

    Task<GoodsIssueDetail> GetAsync(int id, CancellationToken cancellationToken);

    Task<GoodsIssueDetail> CreateAsync(CreateGoodsIssueRequest request, CancellationToken cancellationToken);

    Task<GoodsIssueDetail> UpdateAsync(int id, UpdateGoodsIssueRequest request, CancellationToken cancellationToken);

    Task DeleteAsync(int id, CancellationToken cancellationToken);

    Task<GoodsIssueDetail> SubmitAsync(int id, CancellationToken cancellationToken);

    /// <summary>Approves and atomically reserves the stock of every line (all or nothing).</summary>
    Task<GoodsIssueDetail> ApproveAsync(int id, CancellationToken cancellationToken);

    Task<GoodsIssueDetail> RejectAsync(int id, string reason, CancellationToken cancellationToken);

    Task<GoodsIssueDetail> ReopenAsync(int id, CancellationToken cancellationToken);

    /// <summary>Decreases stock and consumes the reservation; writes ISSUE movements and audit.</summary>
    Task<GoodsIssueDetail> CompleteAsync(int id, CancellationToken cancellationToken);

    /// <summary>Cancels; an approved issue gives its reserved stock back.</summary>
    Task<GoodsIssueDetail> CancelAsync(int id, string reason, CancellationToken cancellationToken);
}

public sealed class GoodsIssueService(
    WmsDbContext dbContext,
    IGoodsIssueRepository repository,
    IDocumentNumberGenerator numberGenerator,
    DocumentWorkflow workflow,
    IStockLedger ledger,
    IStockReservationService reservations,
    IAuditLogger audit,
    ICurrentUser currentUser,
    TimeProvider timeProvider)
    : DocumentServiceBase<GoodsIssue, GoodsIssueDetail>(dbContext, workflow, audit, currentUser), IGoodsIssueService
{
    protected override string AuditEntity => "GoodsIssue";

    protected override string DisplayName => "Goods issue";

    protected override string ApprovePermission => Permissions.GoodsIssueApprove;

    protected override Task<GoodsIssue?> LoadAsync(int id, CancellationToken cancellationToken) =>
        repository.FindAsync(id, cancellationToken);

    public Task<PagedResult<GoodsIssueSummary>> SearchAsync(GoodsIssueQuery query, CancellationToken cancellationToken) =>
        repository.SearchAsync(query, cancellationToken);

    public override async Task<GoodsIssueDetail> GetAsync(int id, CancellationToken cancellationToken) =>
        await repository.GetDetailAsync(id, cancellationToken) ?? throw new NotFoundException(DisplayName, id);

    public async Task<GoodsIssueDetail> CreateAsync(CreateGoodsIssueRequest request, CancellationToken cancellationToken)
    {
        await ValidateAsync(request.WarehouseId, request.Lines, cancellationToken);

        var issue = new GoodsIssue
        {
            DocumentNo = await numberGenerator.NextAsync(DocumentTypes.GoodsIssue, cancellationToken),
            WarehouseId = request.WarehouseId
        };
        Apply(issue, request, request.IssueDate ?? Today);
        Workflow.Created(issue);

        await using (var transaction = await DbContext.Database.BeginTransactionAsync(cancellationToken))
        {
            repository.Add(issue);
            await SaveAsync(cancellationToken);
            Audit.Log(AuditEntity, issue.Id, "CREATED", new { issue.DocumentNo, LineCount = issue.Lines.Count });
            await SaveAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        return await GetAsync(issue.Id, cancellationToken);
    }

    public async Task<GoodsIssueDetail> UpdateAsync(int id, UpdateGoodsIssueRequest request, CancellationToken cancellationToken)
    {
        var issue = await FindAsync(id, cancellationToken);
        Workflow.EnsureCan(issue, DocumentAction.Edit);
        await ValidateAsync(issue.WarehouseId, request.Lines, cancellationToken);

        issue.Lines.Clear();
        Apply(issue, request, request.IssueDate ?? issue.IssueDate);
        Workflow.Stamp(issue);
        Audit.Log(AuditEntity, issue.Id, "UPDATED", new { issue.DocumentNo, LineCount = issue.Lines.Count });
        await SaveAsync(cancellationToken);

        return await GetAsync(id, cancellationToken);
    }

    protected override Task BeforeSubmitAsync(GoodsIssue issue, CancellationToken cancellationToken) =>
        ValidateAsync(issue.WarehouseId, issue.Lines.Select(ToRequest).ToList(), cancellationToken);

    /// <summary>The authoritative availability check: conditional, atomic reservation per line.</summary>
    protected override Task OnApprovedAsync(GoodsIssue issue, CancellationToken cancellationToken) =>
        reservations.ReserveAsync(ToReservations(issue), cancellationToken);

    protected override Task OnCancelledAsync(GoodsIssue issue, DocumentStatus previousStatus, CancellationToken cancellationToken) =>
        previousStatus == DocumentStatus.Approved
            ? reservations.ReleaseAsync(ToReservations(issue), cancellationToken)
            : Task.CompletedTask;

    protected override async Task<object?> OnCompletingAsync(GoodsIssue issue, CancellationToken cancellationToken)
    {
        // Quantity and ReservedQuantity decrease together: the stock was already held at approval.
        var movements = await ledger.ApplyAsync(
            issue.Lines
                .Select(line => new StockChange(
                    line.ProductId,
                    line.WarehouseId,
                    line.LocationId,
                    -line.Quantity,
                    MovementType.Issue,
                    line.Note,
                    ReservedChange: -line.Quantity))
                .ToList(),
            new StockReference(DocumentTypes.GoodsIssue, issue.Id, issue.DocumentNo),
            cancellationToken);

        return new
        {
            TotalQuantity = issue.Lines.Sum(line => line.Quantity),
            MovementIds = movements.Select(movement => movement.Id)
        };
    }

    private static List<StockReservationLine> ToReservations(GoodsIssue issue) =>
        issue.Lines.Select(line => new StockReservationLine(line.ProductId, line.WarehouseId, line.LocationId, line.Quantity)).ToList();

    private static void Apply(GoodsIssue issue, IGoodsIssuePayload request, DateOnly issueDate)
    {
        issue.CustomerId = request.CustomerId;
        issue.CustomerName = TrimToNull(request.CustomerName);
        issue.ReferenceNo = TrimToNull(request.ReferenceNo);
        issue.IssueDate = issueDate;
        issue.Note = TrimToNull(request.Note);
        issue.Lines.AddRange(request.Lines.Select(line => new GoodsIssueLine
        {
            WarehouseId = issue.WarehouseId,
            ProductId = line.ProductId,
            LocationId = line.LocationId,
            Quantity = line.Quantity,
            Note = TrimToNull(line.Note)
        }));
    }

    /// <summary>
    /// Early feedback: product/bin exist and quantity &lt;= current AvailableQuantity.
    /// (Approval re-checks atomically, because other documents may reserve stock in between.)
    /// </summary>
    private async Task ValidateAsync(int warehouseId, IReadOnlyList<GoodsIssueLineRequest> lines, CancellationToken cancellationToken)
    {
        var context = await DocumentLineValidator.LoadAsync(
            DbContext, warehouseId, lines.Select(line => (line.ProductId, line.LocationId)).ToList(), cancellationToken);

        var productIds = lines.Select(line => line.ProductId).Distinct().ToList();
        var available = await DbContext.Set<Inventory>()
            .AsNoTracking()
            .Where(i => i.WarehouseId == warehouseId && productIds.Contains(i.ProductId))
            .Select(i => new { i.ProductId, i.LocationId, i.AvailableQuantity })
            .ToDictionaryAsync(i => (i.ProductId, i.LocationId), i => i.AvailableQuantity, cancellationToken);

        var errors = new List<string>();
        for (var index = 0; index < lines.Count; index++)
        {
            var line = lines[index];
            var prefix = $"Line {index + 1}:";

            // Inactive products/locations may still be issued: that is how leftover stock is cleared.
            context.CheckProduct(errors, prefix, line.ProductId, requireActive: false);
            context.CheckBin(errors, prefix, line.LocationId, requireActive: false, "goods are issued from a Bin");

            var availableQuantity = available.GetValueOrDefault((line.ProductId, line.LocationId));
            if (line.Quantity > availableQuantity)
            {
                errors.Add($"{prefix} quantity {line.Quantity:0.###} exceeds available stock {availableQuantity:0.###}.");
            }
        }

        errors.AddRange(DocumentLineValidator.Duplicates(
            lines.Select(line => (object)(line.ProductId, line.LocationId)).ToList(),
            "repeat the same product and location; merge them."));

        DocumentLineValidator.ThrowIfAny(errors);
    }

    private static GoodsIssueLineRequest ToRequest(GoodsIssueLine line) => new()
    {
        ProductId = line.ProductId,
        LocationId = line.LocationId,
        Quantity = line.Quantity,
        Note = line.Note
    };

    private DateOnly Today => DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);

    private static string? TrimToNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
