using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WMS.Api.Common;
using WMS.Api.Constants;
using WMS.Api.Data;
using WMS.Api.Extensions;
using WMS.Api.Repositories;
using WMS.Api.Tests.Infrastructure;

namespace WMS.Api.Tests.Authorization;

public sealed class UserPlatformInitializerTests(SqlServerDatabaseFixture fixture) : IClassFixture<SqlServerDatabaseFixture>
{
    [Fact]
    public async Task Initialization_creates_admin_and_default_grants_and_can_be_repeated()
    {
        var services = new ServiceCollection();
        services.AddOptions();
        services.AddDbContext<WmsDbContext>(options => options.UseSqlServer(fixture.ConnectionString));
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, HttpContextCurrentUser>();
        services.AddSingleton(TimeProvider.System);
        services.AddUserPlatformModule();
        await using var provider = services.BuildServiceProvider();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["BootstrapAdmin:Username"] = "bootstrap.admin",
            ["BootstrapAdmin:Email"] = "bootstrap@example.com",
            ["BootstrapAdmin:Password"] = "Only.For.Test.123"
        }).Build();

        await UserPlatformInitializer.InitializeAsync(provider, configuration, default);
        await UserPlatformInitializer.InitializeAsync(provider, new ConfigurationBuilder().Build(), default);

        await using var db = fixture.CreateContext();
        var admin = await db.Users.SingleAsync();
        Assert.True(await new UserRepository(db).IsActiveAdminAsync(admin.Id, default));
        var staffPermissions = await db.RolePermissions
            .Where(grant => grant.Role.Code == "WAREHOUSE_STAFF")
            .Select(grant => grant.Permission.Code).ToListAsync();
        Assert.Contains(Permissions.ProductView, staffPermissions);
        Assert.Contains(Permissions.StockTransferReceive, staffPermissions);
        Assert.DoesNotContain(Permissions.StockTransferApprove, staffPermissions);
    }
}
