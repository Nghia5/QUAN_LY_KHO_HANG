using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using WMS.Api.Common;
using WMS.Api.Data;
using WMS.Api.Dtos.Batches;
using WMS.Api.Dtos.Common;
using WMS.Api.Exceptions;
using WMS.Api.Models;
using WMS.Api.Repositories;
using WMS.Api.Services.Audit;
using WMS.Api.Services.Documents;
using WMS.Api.Validators;

namespace WMS.Api.Services.Batches;

public interface IBatchService
{
    Task<PagedResult<BatchResponse>> SearchAsync(BatchQuery query, CancellationToken cancellationToken);

    Task<BatchDetail> GetAsync(int id, CancellationToken cancellationToken);

    Task<BatchDetail> CreateAsync(CreateBatchRequest request, CancellationToken cancellationToken);

    Task<BatchDetail> UpdateAsync(int id, UpdateBatchRequest request, CancellationToken cancellationToken);

    /// <summary>Only a batch never used for stock can be deleted.</summary>
    Task DeleteAsync(int id, CancellationToken cancellationToken);

    Task<PagedResult<ExpiryAlertRow>> GetExpiryAlertsAsync(ExpiryAlertQuery query, CancellationToken cancellationToken);

    Task<PickSuggestion> SuggestPickAsync(PickSuggestionQuery query, CancellationToken cancellationToken);

    /// <summary>
    /// Returns the batch (product, code), creating it when missing. Throws 422 when it exists with
    /// different dates. Used by goods receipts (inside their transaction).
    /// </summary>
    Task<Batch> EnsureAsync(int productId, string batchCode, DateOnly? manufactureDate, DateOnly? expiryDate, CancellationToken cancellationToken);

    /// <summary>Error message when (product, code) exists with other dates, otherwise null.</summary>
    Task<string?> CheckDatesAsync(int productId, string batchCode, DateOnly? manufactureDate, DateOnly? expiryDate, CancellationToken cancellationToken);
}

public sealed class BatchService(
    WmsDbContext dbContext,
    IBatchRepository repository,
    IAuditLogger audit,
    ICurrentUser currentUser,
    TimeProvider timeProvider,
    IOptions<WarehouseOptions> options) : IBatchService
{
    private const string AuditEntity = "Batch";

    private DateOnly Today => DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);

    private int NearExpiryDays => options.Value.NearExpiryDays;

    public Task<PagedResult<BatchResponse>> SearchAsync(BatchQuery query, CancellationToken cancellationToken) =>
        repository.SearchAsync(query, Today, NearExpiryDays, cancellationToken);

    public async Task<BatchDetail> GetAsync(int id, CancellationToken cancellationToken) =>
        await repository.GetDetailAsync(id, Today, NearExpiryDays, cancellationToken) ?? throw new NotFoundException("Batch", id);

    public Task<PagedResult<ExpiryAlertRow>> GetExpiryAlertsAsync(ExpiryAlertQuery query, CancellationToken cancellationToken) =>
        repository.GetExpiryAlertsAsync(query, Today, NearExpiryDays, cancellationToken);

    public async Task<BatchDetail> CreateAsync(CreateBatchRequest request, CancellationToken cancellationToken)
    {
        if (!await dbContext.Set<Product>().AnyAsync(product => product.Id == request.ProductId, cancellationToken))
        {
            throw new BusinessRuleException($"Product {request.ProductId} does not exist.");
        }

        ValidateManufactureDate(request.ManufactureDate);
        var code = CommonRules.NormalizeCode(request.BatchCode);
        if (await repository.FindByCodeAsync(request.ProductId, code, cancellationToken) is not null)
        {
            throw new ConflictException($"Batch '{code}' already exists for this product.");
        }

        var batch = new Batch
        {
            ProductId = request.ProductId,
            BatchCode = code,
            ManufactureDate = request.ManufactureDate,
            ExpiryDate = request.ExpiryDate,
            Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(),
            CreatedBy = currentUser.UserId,
            CreatedAt = timeProvider.GetUtcNow().UtcDateTime
        };

        await using (var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken))
        {
            dbContext.Add(batch);
            await SaveAsync(cancellationToken);
            audit.Log(AuditEntity, batch.Id, "CREATED", new { batch.ProductId, batch.BatchCode, batch.ManufactureDate, batch.ExpiryDate });
            await SaveAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        return await GetAsync(batch.Id, cancellationToken);
    }

    public async Task<BatchDetail> UpdateAsync(int id, UpdateBatchRequest request, CancellationToken cancellationToken)
    {
        var batch = await repository.FindAsync(id, cancellationToken) ?? throw new NotFoundException("Batch", id);
        ValidateManufactureDate(request.ManufactureDate);

        audit.Log(AuditEntity, batch.Id, "UPDATED", new
        {
            Previous = new { batch.ManufactureDate, batch.ExpiryDate },
            Current = new { request.ManufactureDate, request.ExpiryDate }
        });
        batch.ManufactureDate = request.ManufactureDate;
        batch.ExpiryDate = request.ExpiryDate;
        batch.Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
        batch.UpdatedAt = timeProvider.GetUtcNow().UtcDateTime;
        await SaveAsync(cancellationToken);

        return await GetAsync(id, cancellationToken);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken)
    {
        var batch = await repository.FindAsync(id, cancellationToken) ?? throw new NotFoundException("Batch", id);
        if (await repository.IsReferencedAsync(id, cancellationToken))
        {
            throw new ConflictException($"Batch '{batch.BatchCode}' has stock history and cannot be deleted.");
        }

        audit.Log(AuditEntity, batch.Id, "DELETED", new { batch.ProductId, batch.BatchCode });
        dbContext.Remove(batch);
        await SaveAsync(cancellationToken);
    }

    public async Task<string?> CheckDatesAsync(
        int productId,
        string batchCode,
        DateOnly? manufactureDate,
        DateOnly? expiryDate,
        CancellationToken cancellationToken)
    {
        var existing = await dbContext.Set<Batch>()
            .AsNoTracking()
            .Where(batch => batch.ProductId == productId && batch.BatchCode == CommonRules.NormalizeCode(batchCode))
            .Select(batch => new { batch.ManufactureDate, batch.ExpiryDate })
            .SingleOrDefaultAsync(cancellationToken);

        if (existing is null)
        {
            return null;
        }

        var conflicts = (manufactureDate is not null && manufactureDate != existing.ManufactureDate)
                        || (expiryDate is not null && expiryDate != existing.ExpiryDate);
        return conflicts
            ? $"batch '{batchCode.ToUpperInvariant()}' already exists with manufacture {existing.ManufactureDate:yyyy-MM-dd} / expiry {existing.ExpiryDate:yyyy-MM-dd}."
            : null;
    }

    public async Task<Batch> EnsureAsync(
        int productId,
        string batchCode,
        DateOnly? manufactureDate,
        DateOnly? expiryDate,
        CancellationToken cancellationToken)
    {
        var code = CommonRules.NormalizeCode(batchCode);
        if (await CheckDatesAsync(productId, code, manufactureDate, expiryDate, cancellationToken) is { } conflict)
        {
            throw new BusinessRuleException($"Product {productId}: {conflict}");
        }

        if (await repository.FindByCodeAsync(productId, code, cancellationToken) is { } existing)
        {
            return existing;
        }

        // Inserted with raw SQL so a concurrent creation of the same code does not break the
        // caller's unit of work: on a unique violation we simply read the winner's row.
        try
        {
            await dbContext.Database.ExecuteSqlAsync($"""
                INSERT INTO [Batches] ([ProductId], [BatchCode], [ManufactureDate], [ExpiryDate], [CreatedBy], [CreatedAt])
                VALUES ({productId}, {code}, {manufactureDate}, {expiryDate}, {currentUser.UserId}, SYSUTCDATETIME())
                """, cancellationToken);
        }
        catch (SqlException exception) when (exception.Number is 2601 or 2627)
        {
            // created concurrently: fall through and read it
        }

        return await repository.FindByCodeAsync(productId, code, cancellationToken)
               ?? throw new InvalidOperationException($"Batch {code} could not be created.");
    }

    public async Task<PickSuggestion> SuggestPickAsync(PickSuggestionQuery query, CancellationToken cancellationToken)
    {
        var rows = await repository.GetPickRowsAsync(query.ProductId, query.WarehouseId, cancellationToken);
        var today = Today;

        // Candidates: every lot of every bin, plus the stock without lot of each bin (always last).
        var candidates = rows.SelectMany(row =>
        {
            var lots = row.Lots
                .Where(lot => query.IncludeExpired || lot.ExpiryDate is null || lot.ExpiryDate >= today)
                .Select(lot => new
                {
                    Row = row,
                    BatchId = (int?)lot.BatchId,
                    BatchCode = (string?)lot.BatchCode,
                    lot.ManufactureDate,
                    lot.ExpiryDate,
                    CreatedAt = (DateTime?)lot.CreatedAt,
                    lot.Quantity
                });
            var untracked = row.Quantity - row.Lots.Sum(lot => lot.Quantity);
            return untracked > 0
                ? lots.Append(new
                {
                    Row = row, BatchId = (int?)null, BatchCode = (string?)null, ManufactureDate = (DateOnly?)null,
                    ExpiryDate = (DateOnly?)null, CreatedAt = (DateTime?)null, Quantity = untracked
                })
                : lots;
        });

        var ordered = query.Strategy == PickStrategy.Fefo
            ? candidates.OrderBy(c => c.BatchId is null).ThenBy(c => c.ExpiryDate is null).ThenBy(c => c.ExpiryDate).ThenBy(c => c.CreatedAt)
            : candidates.OrderBy(c => c.BatchId is null).ThenBy(c => c.ManufactureDate ?? DateOnly.MaxValue).ThenBy(c => c.CreatedAt);

        // Reserved stock of a bin cannot be picked: each bin contributes at most its AvailableQuantity.
        var budget = rows.ToDictionary(row => row.LocationId, row => row.AvailableQuantity);
        var remaining = query.Quantity;
        var lines = new List<PickSuggestionLine>();
        foreach (var candidate in ordered)
        {
            if (remaining <= 0)
            {
                break;
            }

            var take = Math.Min(Math.Min(candidate.Quantity, budget[candidate.Row.LocationId]), remaining);
            if (take <= 0)
            {
                continue;
            }

            budget[candidate.Row.LocationId] -= take;
            remaining -= take;
            lines.Add(new PickSuggestionLine(candidate.BatchId, candidate.BatchCode, candidate.ManufactureDate, candidate.ExpiryDate,
                candidate.Row.LocationId, candidate.Row.LocationCode, candidate.Quantity, take));
        }

        return new PickSuggestion(query.Strategy, query.Quantity, query.Quantity - remaining, remaining, lines);
    }

    private void ValidateManufactureDate(DateOnly? manufactureDate)
    {
        if (manufactureDate > Today)
        {
            throw new BusinessRuleException("Manufacture date cannot be in the future.");
        }
    }

    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.GetBaseException() is SqlException { Number: 2601 or 2627 })
        {
            throw new ConflictException("A batch with this code already exists for this product.");
        }
        catch (DbUpdateException exception) when (exception.GetBaseException() is SqlException { Number: 547 })
        {
            throw new ConflictException("The batch is referenced by stock data.");
        }
    }
}
