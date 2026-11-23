using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using WMS.Api.Models;

namespace WMS.Api.Data.Interceptors;

/// <summary>
/// Application-level guard: stock movements may only be inserted. The database trigger
/// TR_StockMovements_Immutable is the final guard for anything that bypasses EF.
/// </summary>
public sealed class StockMovementImmutabilityInterceptor : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Guard(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Guard(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private static void Guard(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        var violation = context.ChangeTracker.Entries<StockMovement>()
            .FirstOrDefault(entry => entry.State is EntityState.Modified or EntityState.Deleted);

        if (violation is not null)
        {
            throw new InvalidOperationException(
                $"Stock movement {violation.Entity.Id} is immutable and cannot be {violation.State.ToString().ToLowerInvariant()}.");
        }
    }
}
