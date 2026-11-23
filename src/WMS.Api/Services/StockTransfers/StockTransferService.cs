using Microsoft.EntityFrameworkCore;
using WMS.Api.Common;
using WMS.Api.Constants;
using WMS.Api.Data;
using WMS.Api.Dtos.Common;
using WMS.Api.Dtos.StockTransfers;
using WMS.Api.Exceptions;
using WMS.Api.Models;
using WMS.Api.Models.Documents;
using WMS.Api.Repositories;
using WMS.Api.Services.Audit;
using WMS.Api.Services.Documents;
using WMS.Api.Services.Inventories;

namespace WMS.Api.Services.StockTransfers;

public interface IStockTransferService
{
    Task<PagedResult<StockTransferSummary>> SearchAsync(StockTransferQuery query, CancellationToken cancellationToken);

    Task<StockTransferDetail> GetAsync(int id, CancellationToken cancellationToken);

    Task<StockTransferDetail> CreateAsync(CreateStockTransferRequest request, CancellationToken cancellationToken);

    Task<StockTransferDetail> UpdateAsync(int id, UpdateStockTransferRequest request, CancellationToken cancellationToken);

    Task DeleteAsync(int id, CancellationToken cancellationToken);

    Task<StockTransferDetail> SubmitAsync(int id, CancellationToken cancellationToken);

    /// <summary>Approves and reserves the source stock of every line (all or nothing).</summary>
    Task<StockTransferDetail> ApproveAsync(int id, CancellationToken cancellationToken);

    Task<StockTransferDetail> RejectAsync(int id, string reason, CancellationToken cancellationToken);

    Task<StockTransferDetail> ReopenAsync(int id, CancellationToken cancellationToken);

    /// <summary>Approved → Picked: goods are picked at the source (still reserved there).</summary>
    Task<StockTransferDetail> PickAsync(int id, CancellationToken cancellationToken);

    /// <summary>Picked → InTransit: source stock and reservation decrease (TRANSFER_OUT).</summary>
    Task<StockTransferDetail> ShipAsync(int id, CancellationToken cancellationToken);

    /// <summary>InTransit → Received: destination stock increases (TRANSFER_IN).</summary>
    Task<StockTransferDetail> ReceiveAsync(int id, CancellationToken cancellationToken);

    /// <summary>Received → Completed: closes the transfer.</summary>
    Task<StockTransferDetail> CompleteAsync(int id, CancellationToken cancellationToken);

    /// <summary>Cancels before shipping; an approved/picked transfer releases its reservation.</summary>
    Task<StockTransferDetail> CancelAsync(int id, string reason, CancellationToken cancellationToken);
}

public sealed class StockTransferService(
    WmsDbContext dbContext,
    IStockTransferRepository repository,
    IDocumentNumberGenerator numberGenerator,
    DocumentWorkflow workflow,
    IStockLedger ledger,
    IStockReservationService reservations,
    IAuditLogger audit,
    ICurrentUser currentUser)
    : DocumentServiceBase<StockTransfer, StockTransferDetail>(dbContext, workflow, audit, currentUser), IStockTransferService
{
    protected override string AuditEntity => "StockTransfer";

    protected override string DisplayName => "Stock transfer";

    protected override string ApprovePermission => Permissions.StockTransferApprove;

    protected override Task<StockTransfer?> LoadAsync(int id, CancellationToken cancellationToken) =>
        repository.FindAsync(id, cancellationToken);

    public Task<PagedResult<StockTransferSummary>> SearchAsync(StockTransferQuery query, CancellationToken cancellationToken) =>
        repository.SearchAsync(query, cancellationToken);

    public override async Task<StockTransferDetail> GetAsync(int id, CancellationToken cancellationToken) =>
        await repository.GetDetailAsync(id, cancellationToken) ?? throw new NotFoundException(DisplayName, id);

    public async Task<StockTransferDetail> CreateAsync(CreateStockTransferRequest request, CancellationToken cancellationToken)
    {
        await ValidateAsync(request.WarehouseId, request.DestinationWarehouseId, request.Lines, checkAvailability: true, cancellationToken);

        var transfer = new StockTransfer
        {
            DocumentNo = await numberGenerator.NextAsync(DocumentTypes.StockTransfer, cancellationToken),
            WarehouseId = request.WarehouseId,
            DestinationWarehouseId = request.DestinationWarehouseId
        };
        Apply(transfer, request);
        Workflow.Created(transfer);

        await using (var transaction = await DbContext.Database.BeginTransactionAsync(cancellationToken))
        {
            repository.Add(transfer);
            await SaveAsync(cancellationToken);
            Audit.Log(AuditEntity, transfer.Id, "CREATED", new { transfer.DocumentNo, LineCount = transfer.Lines.Count });
            await SaveAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        return await GetAsync(transfer.Id, cancellationToken);
    }

    public async Task<StockTransferDetail> UpdateAsync(int id, UpdateStockTransferRequest request, CancellationToken cancellationToken)
    {
        var transfer = await FindAsync(id, cancellationToken);
        Workflow.EnsureCan(transfer, DocumentAction.Edit);
        await ValidateAsync(transfer.WarehouseId, transfer.DestinationWarehouseId, request.Lines, checkAvailability: true, cancellationToken);

        transfer.Lines.Clear();
        Apply(transfer, request);
        Workflow.Stamp(transfer);
        Audit.Log(AuditEntity, transfer.Id, "UPDATED", new { transfer.DocumentNo, LineCount = transfer.Lines.Count });
        await SaveAsync(cancellationToken);

        return await GetAsync(id, cancellationToken);
    }

    protected override Task BeforeSubmitAsync(StockTransfer transfer, CancellationToken cancellationToken) =>
        ValidateAsync(transfer.WarehouseId, transfer.DestinationWarehouseId, ToRequests(transfer), checkAvailability: true, cancellationToken);

    protected override Task OnApprovedAsync(StockTransfer transfer, CancellationToken cancellationToken) =>
        reservations.ReserveAsync(ToReservations(transfer), cancellationToken);

    protected override Task OnCancelledAsync(StockTransfer transfer, DocumentStatus previousStatus, CancellationToken cancellationToken) =>
        previousStatus is DocumentStatus.Approved or DocumentStatus.Picked
            ? reservations.ReleaseAsync(ToReservations(transfer), cancellationToken)
            : Task.CompletedTask;

    public Task<StockTransferDetail> PickAsync(int id, CancellationToken cancellationToken) =>
        RunAsync(id, "PICKED", async transfer =>
        {
            Workflow.Advance(transfer, DocumentAction.Pick, DocumentStatus.Picked);
            transfer.PickedBy = Workflow.CurrentUserId;
            transfer.PickedAt = Workflow.UtcNow;
            await SaveAsync(cancellationToken);
            return null;
        }, cancellationToken);

    public Task<StockTransferDetail> ShipAsync(int id, CancellationToken cancellationToken) =>
        RunAsync(id, "SHIPPED", async transfer =>
        {
            Workflow.Advance(transfer, DocumentAction.Ship, DocumentStatus.InTransit);
            transfer.ShippedBy = Workflow.CurrentUserId;
            transfer.ShippedAt = Workflow.UtcNow;
            await SaveAsync(cancellationToken); // RowVersion: shipping twice concurrently fails here

            // The stock was reserved at approval: quantity and reservation leave the source together.
            var movements = await ledger.ApplyAsync(
                transfer.Lines
                    .Select(line => new StockChange(
                        line.ProductId, line.WarehouseId, line.FromLocationId, -line.Quantity,
                        MovementType.TransferOut, line.Note, ReservedChange: -line.Quantity))
                    .ToList(),
                Reference(transfer),
                cancellationToken);
            return new { MovementIds = movements.Select(movement => movement.Id) };
        }, cancellationToken);

    public Task<StockTransferDetail> ReceiveAsync(int id, CancellationToken cancellationToken) =>
        RunAsync(id, "RECEIVED", async transfer =>
        {
            Workflow.Advance(transfer, DocumentAction.Receive, DocumentStatus.Received);
            transfer.ReceivedBy = Workflow.CurrentUserId;
            transfer.ReceivedAt = Workflow.UtcNow;

            // Destination bins/warehouse may have been deactivated while the goods were in transit.
            await ValidateDestinationAsync(transfer.DestinationWarehouseId, transfer.Lines.Select(line => line.ToLocationId).ToList(), cancellationToken);
            await SaveAsync(cancellationToken);

            var movements = await ledger.ApplyAsync(
                transfer.Lines
                    .Select(line => new StockChange(
                        line.ProductId, line.DestinationWarehouseId, line.ToLocationId, line.Quantity,
                        MovementType.TransferIn, line.Note))
                    .ToList(),
                Reference(transfer),
                cancellationToken);
            return new { MovementIds = movements.Select(movement => movement.Id) };
        }, cancellationToken);

    /// <summary>Stock already moved at ship/receive; completion only closes the document.</summary>
    protected override Task<object?> OnCompletingAsync(StockTransfer transfer, CancellationToken cancellationToken) =>
        Task.FromResult<object?>(new { TotalQuantity = transfer.Lines.Sum(line => line.Quantity) });

    private static StockReference Reference(StockTransfer transfer) =>
        new(DocumentTypes.StockTransfer, transfer.Id, transfer.DocumentNo);

    private static List<StockReservationLine> ToReservations(StockTransfer transfer) =>
        transfer.Lines.Select(line => new StockReservationLine(line.ProductId, line.WarehouseId, line.FromLocationId, line.Quantity)).ToList();

    private static List<StockTransferLineRequest> ToRequests(StockTransfer transfer) =>
        transfer.Lines.Select(line => new StockTransferLineRequest
        {
            ProductId = line.ProductId,
            FromLocationId = line.FromLocationId,
            ToLocationId = line.ToLocationId,
            Quantity = line.Quantity,
            Note = line.Note
        }).ToList();

    private static void Apply(StockTransfer transfer, IStockTransferPayload request)
    {
        transfer.Reason = TrimToNull(request.Reason);
        transfer.Note = TrimToNull(request.Note);
        transfer.Lines.AddRange(request.Lines.Select(line => new StockTransferLine
        {
            WarehouseId = transfer.WarehouseId,
            DestinationWarehouseId = transfer.DestinationWarehouseId,
            ProductId = line.ProductId,
            FromLocationId = line.FromLocationId,
            ToLocationId = line.ToLocationId,
            Quantity = line.Quantity,
            Note = TrimToNull(line.Note)
        }));
    }

    private async Task ValidateAsync(
        int sourceWarehouseId,
        int destinationWarehouseId,
        IReadOnlyList<StockTransferLineRequest> lines,
        bool checkAvailability,
        CancellationToken cancellationToken)
    {
        var source = await DocumentLineValidator.LoadAsync(
            DbContext, sourceWarehouseId, lines.Select(line => (line.ProductId, line.FromLocationId)).ToList(), cancellationToken);
        var destination = await DocumentLineValidator.LoadAsync(
            DbContext, destinationWarehouseId, lines.Select(line => (line.ProductId, line.ToLocationId)).ToList(), cancellationToken);

        var productIds = lines.Select(line => line.ProductId).Distinct().ToList();
        var available = checkAvailability
            ? await DbContext.Set<Inventory>()
                .AsNoTracking()
                .Where(i => i.WarehouseId == sourceWarehouseId && productIds.Contains(i.ProductId))
                .Select(i => new { i.ProductId, i.LocationId, i.AvailableQuantity })
                .ToDictionaryAsync(i => (i.ProductId, i.LocationId), i => i.AvailableQuantity, cancellationToken)
            : [];

        var errors = new List<string>();
        for (var index = 0; index < lines.Count; index++)
        {
            var line = lines[index];
            var prefix = $"Line {index + 1}:";

            source.CheckProduct(errors, prefix, line.ProductId, requireActive: false);
            source.CheckBin(errors, $"{prefix} source", line.FromLocationId, requireActive: false, "goods are taken from a Bin");
            destination.CheckBin(errors, $"{prefix} destination", line.ToLocationId, requireActive: true, "goods can only be put into a Bin");

            if (checkAvailability)
            {
                var availableQuantity = available.GetValueOrDefault((line.ProductId, line.FromLocationId));
                if (line.Quantity > availableQuantity)
                {
                    errors.Add($"{prefix} quantity {line.Quantity:0.###} exceeds available stock {availableQuantity:0.###} at the source.");
                }
            }
        }

        errors.AddRange(DocumentLineValidator.Duplicates(
            lines.Select(line => (object)(line.ProductId, line.FromLocationId, line.ToLocationId)).ToList(),
            "repeat the same product, source and destination; merge them."));

        DocumentLineValidator.ThrowIfAny(errors);
    }

    private async Task ValidateDestinationAsync(int warehouseId, IReadOnlyList<int> locationIds, CancellationToken cancellationToken)
    {
        var destination = await DocumentLineValidator.LoadAsync(
            DbContext, warehouseId, locationIds.Select(id => (0, id)).ToList(), cancellationToken);

        var errors = new List<string>();
        for (var index = 0; index < locationIds.Count; index++)
        {
            destination.CheckBin(errors, $"Line {index + 1}: destination", locationIds[index], requireActive: true, "goods can only be put into a Bin");
        }

        DocumentLineValidator.ThrowIfAny(errors);
    }

    private static string? TrimToNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
