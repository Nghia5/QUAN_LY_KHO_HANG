using Microsoft.EntityFrameworkCore;
using WMS.Api.Common;
using WMS.Api.Constants;
using WMS.Api.Data;
using WMS.Api.Dtos.Common;
using WMS.Api.Dtos.Stocktakes;
using WMS.Api.Exceptions;
using WMS.Api.Models;
using WMS.Api.Models.Documents;
using WMS.Api.Repositories;
using WMS.Api.Services.Audit;
using WMS.Api.Services.Documents;
using WMS.Api.Services.Inventories;

namespace WMS.Api.Services.Stocktakes;

public interface IStocktakeService
{
    Task<PagedResult<StocktakeSummary>> SearchAsync(StocktakeQuery query, CancellationToken cancellationToken);

    Task<StocktakeDetail> GetAsync(int id, CancellationToken cancellationToken);

    /// <summary>COMPARE view: only lines whose count differs from the snapshot.</summary>
    Task<StocktakeDetail> GetDifferencesAsync(int id, CancellationToken cancellationToken);

    Task<StocktakeDetail> CreateAsync(CreateStocktakeRequest request, CancellationToken cancellationToken);

    Task<StocktakeDetail> UpdateAsync(int id, UpdateStocktakeRequest request, CancellationToken cancellationToken);

    Task DeleteAsync(int id, CancellationToken cancellationToken);

    /// <summary>SNAPSHOT: captures SystemQuantity of every stock in scope and locks those locations.</summary>
    Task<StocktakeDetail> SnapshotAsync(int id, CancellationToken cancellationToken);

    /// <summary>COUNT: records counted quantities (can be called many times while InProgress).</summary>
    Task<StocktakeDetail> CountAsync(int id, StocktakeCountRequest request, CancellationToken cancellationToken);

    /// <summary>COMPARE → PendingApproval; every line must be counted.</summary>
    Task<StocktakeDetail> SubmitAsync(int id, CancellationToken cancellationToken);

    Task<StocktakeDetail> ApproveAsync(int id, CancellationToken cancellationToken);

    /// <summary>Sends the stocktake back to counting (InProgress).</summary>
    Task<StocktakeDetail> RejectAsync(int id, string reason, CancellationToken cancellationToken);

    /// <summary>ADJUSTMENT: posts STOCKTAKE_IN/OUT movements for every difference and unlocks the locations.</summary>
    Task<StocktakeDetail> AdjustAsync(int id, CancellationToken cancellationToken);

    Task<StocktakeDetail> CompleteAsync(int id, CancellationToken cancellationToken);

    Task<StocktakeDetail> CancelAsync(int id, string reason, CancellationToken cancellationToken);
}

public sealed class StocktakeService(
    WmsDbContext dbContext,
    IStocktakeRepository repository,
    IDocumentNumberGenerator numberGenerator,
    DocumentWorkflow workflow,
    IStockLedger ledger,
    IAuditLogger audit,
    ICurrentUser currentUser)
    : DocumentServiceBase<Stocktake, StocktakeDetail>(dbContext, workflow, audit, currentUser), IStocktakeService
{
    protected override string AuditEntity => "Stocktake";

    protected override string DisplayName => "Stocktake";

    protected override string ApprovePermission => Permissions.StocktakeApprove;

    protected override Task<Stocktake?> LoadAsync(int id, CancellationToken cancellationToken) =>
        repository.FindAsync(id, cancellationToken);

    public Task<PagedResult<StocktakeSummary>> SearchAsync(StocktakeQuery query, CancellationToken cancellationToken) =>
        repository.SearchAsync(query, cancellationToken);

    public override async Task<StocktakeDetail> GetAsync(int id, CancellationToken cancellationToken) =>
        await repository.GetDetailAsync(id, differencesOnly: false, cancellationToken) ?? throw new NotFoundException(DisplayName, id);

    public async Task<StocktakeDetail> GetDifferencesAsync(int id, CancellationToken cancellationToken) =>
        await repository.GetDetailAsync(id, differencesOnly: true, cancellationToken) ?? throw new NotFoundException(DisplayName, id);

    public async Task<StocktakeDetail> CreateAsync(CreateStocktakeRequest request, CancellationToken cancellationToken)
    {
        await ValidateScopeAsync(request.WarehouseId, request.ScopeLocationIds, cancellationToken);

        var stocktake = new Stocktake
        {
            DocumentNo = await numberGenerator.NextAsync(DocumentTypes.Stocktake, cancellationToken),
            WarehouseId = request.WarehouseId,
            ScopeLocationIds = request.ScopeLocationIds.Distinct().ToList(),
            Note = TrimToNull(request.Note)
        };
        Workflow.Created(stocktake);

        await using (var transaction = await DbContext.Database.BeginTransactionAsync(cancellationToken))
        {
            repository.Add(stocktake);
            await SaveAsync(cancellationToken);
            Audit.Log(AuditEntity, stocktake.Id, "CREATED", new { stocktake.DocumentNo, stocktake.ScopeLocationIds });
            await SaveAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        return await GetAsync(stocktake.Id, cancellationToken);
    }

    public async Task<StocktakeDetail> UpdateAsync(int id, UpdateStocktakeRequest request, CancellationToken cancellationToken)
    {
        var stocktake = await FindAsync(id, cancellationToken);
        Workflow.EnsureCan(stocktake, DocumentAction.Edit);
        await ValidateScopeAsync(stocktake.WarehouseId, request.ScopeLocationIds, cancellationToken);

        stocktake.ScopeLocationIds = request.ScopeLocationIds.Distinct().ToList();
        stocktake.Note = TrimToNull(request.Note);
        Workflow.Stamp(stocktake);
        Audit.Log(AuditEntity, stocktake.Id, "UPDATED", new { stocktake.DocumentNo, stocktake.ScopeLocationIds });
        await SaveAsync(cancellationToken);

        return await GetAsync(id, cancellationToken);
    }

    public Task<StocktakeDetail> SnapshotAsync(int id, CancellationToken cancellationToken) =>
        RunAsync(id, "SNAPSHOT", async stocktake =>
        {
            Workflow.Advance(stocktake, DocumentAction.Snapshot, DocumentStatus.InProgress);

            // Serialize snapshots of the same warehouse so two stocktakes cannot lock the same bins.
            await DbContext.Database.ExecuteSqlAsync(
                $"EXEC sp_getapplock @Resource = {"stocktake-snapshot-" + stocktake.WarehouseId}, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 15000",
                cancellationToken);

            var binIds = await BinsInScopeAsync(stocktake.WarehouseId, stocktake.ScopeLocationIds, cancellationToken);

            var overlapping = await DbContext.Set<StocktakeLine>()
                .Where(line => binIds.Contains(line.LocationId))
                .Join(DbContext.Set<Stocktake>(), line => line.StocktakeId, other => other.Id, (line, other) => other)
                .Where(other => other.Id != stocktake.Id && StockLedger.StocktakeLockStatuses.Contains(other.Status))
                .Select(other => other.DocumentNo)
                .FirstOrDefaultAsync(cancellationToken);
            if (overlapping is not null)
            {
                throw new BusinessRuleException($"Some of these locations are already being counted by stocktake {overlapping}.");
            }

            // UPDLOCK/HOLDLOCK: a concurrent stock change waits for this snapshot and is then
            // rejected by the ledger's stocktake lock, so SystemQuantity cannot go stale.
            var stock = await DbContext.Set<Inventory>()
                .FromSql($"SELECT * FROM [Inventories] WITH (UPDLOCK, HOLDLOCK) WHERE [WarehouseId] = {stocktake.WarehouseId}")
                .Where(inventory => binIds.Contains(inventory.LocationId) && inventory.Quantity > 0)
                .Select(inventory => new { inventory.ProductId, inventory.LocationId, inventory.Quantity })
                .ToListAsync(cancellationToken);

            stocktake.Lines.AddRange(stock.Select(row => new StocktakeLine
            {
                WarehouseId = stocktake.WarehouseId,
                ProductId = row.ProductId,
                LocationId = row.LocationId,
                SystemQuantity = row.Quantity
            }));
            stocktake.SnapshotBy = Workflow.CurrentUserId;
            stocktake.SnapshotAt = Workflow.UtcNow;
            await SaveAsync(cancellationToken);

            return new { Bins = binIds.Count, Lines = stocktake.Lines.Count };
        }, cancellationToken);

    public Task<StocktakeDetail> CountAsync(int id, StocktakeCountRequest request, CancellationToken cancellationToken) =>
        RunAsync(id, "COUNTED", async stocktake =>
        {
            Workflow.EnsureCan(stocktake, DocumentAction.Count);

            var existing = stocktake.Lines.ToDictionary(line => (line.ProductId, line.LocationId));
            var newItems = request.Items.Where(item => !existing.ContainsKey((item.ProductId, item.LocationId))).ToList();
            await ValidateNewItemsAsync(stocktake, newItems, cancellationToken);

            var changes = new List<object>();
            foreach (var item in request.Items)
            {
                if (!existing.TryGetValue((item.ProductId, item.LocationId), out var line))
                {
                    // Goods found during counting: system quantity = current stock (normally 0);
                    // the new line locks this bin from now on.
                    var current = await DbContext.Set<Inventory>()
                        .FromSql($"SELECT * FROM [Inventories] WITH (UPDLOCK, HOLDLOCK) WHERE [ProductId] = {item.ProductId} AND [LocationId] = {item.LocationId}")
                        .Select(inventory => (decimal?)inventory.Quantity)
                        .SingleOrDefaultAsync(cancellationToken) ?? 0m;
                    line = new StocktakeLine
                    {
                        WarehouseId = stocktake.WarehouseId,
                        ProductId = item.ProductId,
                        LocationId = item.LocationId,
                        SystemQuantity = current,
                        IsAddedDuringCount = true
                    };
                    stocktake.Lines.Add(line);
                }

                changes.Add(new { item.ProductId, item.LocationId, Previous = line.CountedQuantity, Counted = item.CountedQuantity });
                line.CountedQuantity = item.CountedQuantity;
                line.CountedBy = Workflow.CurrentUserId;
                line.CountedAt = Workflow.UtcNow;
                line.Note = TrimToNull(item.Note) ?? line.Note;
            }

            Workflow.Stamp(stocktake);
            await SaveAsync(cancellationToken);
            return new { Changes = changes };
        }, cancellationToken);

    protected override Task BeforeSubmitAsync(Stocktake stocktake, CancellationToken cancellationToken)
    {
        if (stocktake.Lines.Count == 0)
        {
            throw new BusinessRuleException("Nothing was counted: the stocktake has no lines.");
        }

        var uncounted = stocktake.Lines.Count(line => line.CountedQuantity is null);
        if (uncounted > 0)
        {
            throw new BusinessRuleException($"{uncounted} line(s) are not counted yet; count every line before submitting.");
        }

        return Task.CompletedTask;
    }

    public Task<StocktakeDetail> AdjustAsync(int id, CancellationToken cancellationToken) =>
        RunAsync(id, "ADJUSTED", async stocktake =>
        {
            Workflow.Advance(stocktake, DocumentAction.Adjust, DocumentStatus.Adjusted);
            stocktake.AdjustedBy = Workflow.CurrentUserId;
            stocktake.AdjustedAt = Workflow.UtcNow;
            await SaveAsync(cancellationToken); // RowVersion: adjusting twice concurrently fails here

            var differences = stocktake.Lines
                .Where(line => line.CountedQuantity is not null && line.CountedQuantity != line.SystemQuantity)
                .ToList();

            if (differences.Count == 0)
            {
                return new { Adjusted = 0 };
            }

            // Defensive: the locations were locked, so the stock must still equal the snapshot.
            var keys = differences.Select(line => line.LocationId).Distinct().ToList();
            var current = await DbContext.Set<Inventory>()
                .Where(inventory => keys.Contains(inventory.LocationId))
                .Select(inventory => new { inventory.ProductId, inventory.LocationId, inventory.Quantity })
                .ToDictionaryAsync(inventory => (inventory.ProductId, inventory.LocationId), inventory => inventory.Quantity, cancellationToken);
            var drifted = differences.FirstOrDefault(line => current.GetValueOrDefault((line.ProductId, line.LocationId)) != line.SystemQuantity);
            if (drifted is not null)
            {
                throw new ConflictException(
                    $"Stock of product {drifted.ProductId} at location {drifted.LocationId} changed after the snapshot; cancel and recount.");
            }

            var movements = await ledger.ApplyAsync(
                differences
                    .Select(line =>
                    {
                        var delta = line.CountedQuantity!.Value - line.SystemQuantity;
                        return new StockChange(
                            line.ProductId, line.WarehouseId, line.LocationId, delta,
                            delta > 0 ? MovementType.StocktakeIn : MovementType.StocktakeOut,
                            $"Stocktake {stocktake.DocumentNo}: system {line.SystemQuantity:0.###}, counted {line.CountedQuantity:0.###}");
                    })
                    .ToList(),
                new StockReference(DocumentTypes.Stocktake, stocktake.Id, stocktake.DocumentNo),
                cancellationToken);

            return new { Adjusted = differences.Count, MovementIds = movements.Select(movement => movement.Id) };
        }, cancellationToken);

    protected override Task<object?> OnCompletingAsync(Stocktake stocktake, CancellationToken cancellationToken) =>
        Task.FromResult<object?>(new
        {
            Lines = stocktake.Lines.Count,
            LinesWithDifference = stocktake.Lines.Count(line => line.CountedQuantity != line.SystemQuantity)
        });

    /// <summary>Bins of the warehouse covered by the scope (all bins when the scope is empty).</summary>
    private async Task<List<int>> BinsInScopeAsync(int warehouseId, IReadOnlyCollection<int> scope, CancellationToken cancellationToken)
    {
        var bins = await DbContext.Set<WarehouseLocation>()
            .AsNoTracking()
            .Where(location => location.WarehouseId == warehouseId && location.Type == LocationType.Bin)
            .Select(location => new { location.Id, location.Code })
            .ToListAsync(cancellationToken);

        if (scope.Count == 0)
        {
            return bins.Select(bin => bin.Id).ToList();
        }

        var scopeCodes = await DbContext.Set<WarehouseLocation>()
            .AsNoTracking()
            .Where(location => scope.Contains(location.Id) && location.WarehouseId == warehouseId)
            .Select(location => location.Code)
            .ToListAsync(cancellationToken);

        return bins
            .Where(bin => scopeCodes.Any(code => bin.Code == code || bin.Code.StartsWith(code + "-", StringComparison.Ordinal)))
            .Select(bin => bin.Id)
            .ToList();
    }

    private async Task ValidateScopeAsync(int warehouseId, IReadOnlyCollection<int> scope, CancellationToken cancellationToken)
    {
        var context = await DocumentLineValidator.LoadAsync(
            DbContext, warehouseId, scope.Select(id => (0, id)).ToList(), cancellationToken);

        var missing = scope.Where(id => !context.Locations.ContainsKey(id)).ToList();
        if (missing.Count > 0)
        {
            throw new BusinessRuleException(
                $"Location(s) {string.Join(", ", missing)} do not belong to warehouse '{context.WarehouseCode}'.");
        }
    }

    private async Task ValidateNewItemsAsync(Stocktake stocktake, IReadOnlyList<StocktakeCountItem> items, CancellationToken cancellationToken)
    {
        if (items.Count == 0)
        {
            return;
        }

        var context = await DocumentLineValidator.LoadAsync(
            DbContext, stocktake.WarehouseId, items.Select(item => (item.ProductId, item.LocationId)).ToList(), cancellationToken);
        var binsInScope = (await BinsInScopeAsync(stocktake.WarehouseId, stocktake.ScopeLocationIds, cancellationToken)).ToHashSet();

        var errors = new List<string>();
        foreach (var item in items)
        {
            var prefix = $"Product {item.ProductId} at location {item.LocationId}:";
            context.CheckProduct(errors, prefix, item.ProductId, requireActive: false);
            context.CheckBin(errors, prefix, item.LocationId, requireActive: true, "only bins are counted");
            if (context.Locations.ContainsKey(item.LocationId) && !binsInScope.Contains(item.LocationId))
            {
                errors.Add($"{prefix} the location is outside the stocktake scope.");
            }
        }

        DocumentLineValidator.ThrowIfAny(errors);
    }

    private static string? TrimToNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
