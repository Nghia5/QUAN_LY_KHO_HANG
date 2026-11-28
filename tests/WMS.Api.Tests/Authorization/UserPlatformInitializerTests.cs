using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WMS.Api.Common;
using WMS.Api.Constants;
using WMS.Api.Data;
using WMS.Api.Dtos.Roles;
using WMS.Api.Dtos.Users;
using WMS.Api.Exceptions;
using WMS.Api.Extensions;
using WMS.Api.Models;
using WMS.Api.Repositories;
using WMS.Api.Services.Roles;
using WMS.Api.Services.Users;
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
        var actor = new FixedCurrentUser(null);
        services.AddSingleton<ICurrentUser>(actor);
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
        actor.UserId = admin.Id;
        Assert.True(await new UserRepository(db).IsActiveAdminAsync(admin.Id, default));
        var staffPermissions = await db.RolePermissions
            .Where(grant => grant.Role.Code == "WAREHOUSE_STAFF")
            .Select(grant => grant.Permission.Code).ToListAsync();
        Assert.Contains(Permissions.ProductView, staffPermissions);
        Assert.Contains(Permissions.StockTransferReceive, staffPermissions);
        Assert.DoesNotContain(Permissions.StockTransferApprove, staffPermissions);

        var adminRole = await db.Roles.SingleAsync(role => role.Code == "ADMIN");
        await using var firstScope = provider.CreateAsyncScope();
        var users = firstScope.ServiceProvider.GetRequiredService<IUserService>();
        var roles = firstScope.ServiceProvider.GetRequiredService<IRoleService>();
        await Assert.ThrowsAsync<ConflictException>(() => users.SoftDeleteAsync(admin.Id, default));
        await Assert.ThrowsAsync<ConflictException>(() => users.UpdateAsync(admin.Id,
            new UpdateUserRequest { FullName = admin.FullName, Email = admin.Email, RoleIds = [] }, default));
        await Assert.ThrowsAsync<ConflictException>(() => roles.UpdateRoleAsync(adminRole.Id,
            new UpdateRoleRequest { Name = adminRole.Name, IsActive = false }, default));

        var secondAdmin = await users.CreateAsync(new CreateUserRequest
        {
            Username = "second.admin",
            FullName = "Second administrator",
            Email = "second@example.com",
            Password = "Only.For.Test.456",
            RoleIds = [adminRole.Id]
        }, default);

        var invalidState = await Assert.ThrowsAsync<SqlException>(() => db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE dbo.Users SET DeletedAt = SYSUTCDATETIME(), DeletedBy = NULL WHERE Id = {secondAdmin.Id}"));
        Assert.Equal(547, invalidState.Number);
        Assert.Contains("CK_Users_SoftDeleteState", invalidState.Message);

        var createdAt = TimeProvider.System.GetUtcNow().UtcDateTime;
        var refreshToken = new RefreshToken
        {
            UserId = admin.Id, Token = "integration-test-token", CreatedAt = createdAt, ExpiresAt = createdAt.AddDays(7)
        };
        db.RefreshTokens.Add(refreshToken);
        await db.SaveChangesAsync();
        var invalidExpiry = await Assert.ThrowsAsync<SqlException>(() => db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE dbo.RefreshTokens SET ExpiresAt = CreatedAt WHERE Id = {refreshToken.Id}"));
        Assert.Equal(547, invalidExpiry.Number);
        Assert.Contains("CK_RefreshTokens_ExpiresAfterCreation", invalidExpiry.Message);
        var invalidRevocation = await Assert.ThrowsAsync<SqlException>(() => db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE dbo.RefreshTokens SET RevokedAt = DATEADD(second, -1, CreatedAt) WHERE Id = {refreshToken.Id}"));
        Assert.Equal(547, invalidRevocation.Number);
        Assert.Contains("CK_RefreshTokens_RevokedAfterCreation", invalidRevocation.Message);

        await Task.WhenAll(RemoveAdminAsync(admin.Id), RemoveAdminAsync(secondAdmin.Id));
        Assert.Equal(1, await db.Users.CountAsync(user => user.IsActive &&
            user.UserRoles.Any(assignment => assignment.RoleId == adminRole.Id)));

        async Task RemoveAdminAsync(int id)
        {
            await using var scope = provider.CreateAsyncScope();
            var service = scope.ServiceProvider.GetRequiredService<IUserService>();
            try
            {
                await service.SoftDeleteAsync(id, default);
            }
            catch (ConflictException exception)
            {
                Assert.Contains("active administrator", exception.Message);
            }
        }
    }
}
