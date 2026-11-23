using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using WMS.Api.Models;

namespace WMS.Api.Data.Interceptors;

/// <summary>
/// Application-level guard: stock movements and audit logs may only be inserted. The database
/// triggers TR_StockMovements_Immutable / TR_AuditLogs_Immutable are the final guard for
/// anything that bypasses EF.
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

        var violation = context.ChangeTracker.Entries()
            .Where(entry => entry.Entity is StockMovement or AuditLog)
            .FirstOrDefault(entry => entry.State is EntityState.Modified or EntityState.Deleted);

        if (violation is not null)
        {
            throw new InvalidOperationException(
                $"{violation.Entity.GetType().Name} records are immutable and cannot be {violation.State.ToString().ToLowerInvariant()}.");
        }
    }
}
