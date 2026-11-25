using System.Collections.Concurrent;
using System.Data.Common;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using WMS.Api.Data;

namespace WMS.Api.Tests.Infrastructure;

/// <summary>
/// Creates a throw-away SQL Server database (real migrations, real constraints), and drops it
/// after the test class finishes.
/// Server: env var <c>WMS_TEST_SQLSERVER</c> (a connection string without Database), default LocalDB.
/// </summary>
public sealed class SqlServerDatabaseFixture : IAsyncLifetime
{
    private const string DefaultServer =
        @"Server=(localdb)\MSSQLLocalDB;Trusted_Connection=True;TrustServerCertificate=True";

    public SqlServerDatabaseFixture()
    {
        var builder = new SqlConnectionStringBuilder(
            Environment.GetEnvironmentVariable("WMS_TEST_SQLSERVER") ?? DefaultServer)
        {
            InitialCatalog = $"WMS_Test_{Guid.NewGuid():N}"
        };
        ConnectionString = builder.ConnectionString;
    }

    public string ConnectionString { get; }

    public SqlCapture Sql { get; } = new();

    public WmsDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<WmsDbContext>()
            .UseSqlServer(ConnectionString)
            .AddInterceptors(Sql, new WMS.Api.Data.Interceptors.StockMovementImmutabilityInterceptor())
            .Options);

    public async Task InitializeAsync()
    {
        await using var context = CreateContext();
        await context.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await using var context = CreateContext();
        await context.Database.EnsureDeletedAsync();
    }
}

/// <summary>Records SQL commands so tests can assert on what reaches the database.</summary>
public sealed class SqlCapture : DbCommandInterceptor
{
    private readonly ConcurrentQueue<string> commands = new();

    public IReadOnlyList<string> Commands => commands.ToArray();

    public void Clear() => commands.Clear();

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        commands.Enqueue(command.CommandText);
        return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
    }

    public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<object> result,
        CancellationToken cancellationToken = default)
    {
        commands.Enqueue(command.CommandText);
        return base.ScalarExecutingAsync(command, eventData, result, cancellationToken);
    }
}
