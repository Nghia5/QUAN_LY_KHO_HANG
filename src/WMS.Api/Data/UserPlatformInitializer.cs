using System.ComponentModel.DataAnnotations;
using System.Data;
using Microsoft.EntityFrameworkCore;
using WMS.Api.Constants;
using WMS.Api.Dtos.Users;
using WMS.Api.Models;
using WMS.Api.Services.Users;

namespace WMS.Api.Data;

public static class UserPlatformInitializer
{
    private static readonly string[] ReadPermissions =
    [
        Permissions.WarehouseView, Permissions.LocationView, Permissions.InventoryView, Permissions.StockMovementView,
        Permissions.GoodsReceiptView, Permissions.GoodsIssueView, Permissions.StockTransferView,
        Permissions.StocktakeView, Permissions.StockAdjustmentView, Permissions.BatchView
    ];

    private static readonly string[] StaffPermissions =
    [
        .. ReadPermissions, Permissions.GoodsReceiptCreate, Permissions.GoodsIssueCreate,
        Permissions.StockTransferCreate, Permissions.StockTransferPick, Permissions.StockTransferShip,
        Permissions.StockTransferReceive, Permissions.StocktakeCreate, Permissions.StocktakeCount,
        Permissions.StockAdjustmentCreate
    ];

    private static readonly string[] ManagerPermissions =
    [
        .. StaffPermissions, Permissions.LocationCreate, Permissions.LocationUpdate, Permissions.LocationDelete,
        Permissions.GoodsReceiptApprove, Permissions.GoodsReceiptComplete, Permissions.GoodsIssueApprove,
        Permissions.GoodsIssueComplete, Permissions.StockTransferApprove, Permissions.StockTransferComplete,
        Permissions.StocktakeApprove, Permissions.StocktakeAdjust, Permissions.StocktakeComplete,
        Permissions.StockAdjustmentApprove
    ];

    public static async Task InitializeAsync(IServiceProvider services, IConfiguration configuration, CancellationToken cancellationToken)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<WmsDbContext>();
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var permissions = await db.Permissions.ToDictionaryAsync(p => p.Code, StringComparer.OrdinalIgnoreCase, cancellationToken);
        foreach (var code in Permissions.All.Where(code => !permissions.ContainsKey(code)))
        {
            var permission = new Permission { Code = code, Group = "WMS" };
            db.Permissions.Add(permission);
            permissions.Add(code, permission);
        }

        var roles = await db.Roles.ToDictionaryAsync(r => r.Code, StringComparer.OrdinalIgnoreCase, cancellationToken);
        AddRole("ADMIN", "System administrator", []);
        AddRole("WAREHOUSE_MANAGER", "Warehouse manager", ManagerPermissions);
        AddRole("WAREHOUSE_STAFF", "Warehouse staff", StaffPermissions);
        AddRole("VIEWER", "Warehouse viewer", ReadPermissions);
        await db.SaveChangesAsync(cancellationToken);

        var adminRole = roles["ADMIN"];
        if (!await db.UserRoles.AnyAsync(assignment => assignment.RoleId == adminRole.Id, cancellationToken))
        {
            if (!adminRole.IsActive)
            {
                throw new InvalidOperationException("The ADMIN role must be active before initializing the first administrator.");
            }

            var request = new CreateUserRequest
            {
                Username = configuration["BootstrapAdmin:Username"] ?? "",
                Email = configuration["BootstrapAdmin:Email"] ?? "",
                Password = configuration["BootstrapAdmin:Password"] ?? "",
                FullName = configuration["BootstrapAdmin:FullName"] ?? "System administrator",
                RoleIds = [adminRole.Id]
            };

            Validator.ValidateObject(request, new ValidationContext(request), validateAllProperties: true);
            await scope.ServiceProvider.GetRequiredService<IUserService>().CreateAsync(request, cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);

        void AddRole(string code, string name, IEnumerable<string> permissionCodes)
        {
            if (roles.ContainsKey(code))
            {
                return;
            }

            var role = new Role
            {
                Code = code,
                Name = name,
                RolePermissions = permissionCodes.Select(permissionCode => new RolePermission
                {
                    Permission = permissions[permissionCode]
                }).ToList()
            };
            db.Roles.Add(role);
            roles.Add(code, role);
        }
    }
}
