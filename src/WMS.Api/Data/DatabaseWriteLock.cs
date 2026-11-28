using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using WMS.Api.Exceptions;

namespace WMS.Api.Data;

internal static class DatabaseWriteLock
{
    public static async Task<IDbContextTransaction> BeginAsync(
        WmsDbContext db, string resource, CancellationToken cancellationToken)
    {
        var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                DECLARE @result int;
                EXEC @result = sys.sp_getapplock @Resource = {resource},
                    @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 10000;
                IF @result < 0 THROW 51020, 'Could not acquire the write lock.', 1;
                """, cancellationToken);
            return transaction;
        }
        catch (SqlException exception) when (exception.Number == 51020)
        {
            await transaction.DisposeAsync();
            throw new ConflictException("Another request is updating this resource. Retry the operation.");
        }
        catch
        {
            await transaction.DisposeAsync();
            throw;
        }
    }
}
