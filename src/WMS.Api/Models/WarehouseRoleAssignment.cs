namespace WMS.Api.Models;

public sealed class WarehouseRoleAssignment
{
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public int WarehouseId { get; set; }
    public Warehouse Warehouse { get; set; } = null!;
    public int RoleId { get; set; }
    public Role Role { get; set; } = null!;
}
