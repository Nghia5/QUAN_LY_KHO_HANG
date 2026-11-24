namespace WMS.Api.Models;

/// <summary>
/// Mapping entity between Role and Permission.
/// </summary>
public sealed class RolePermission
{
    public int RoleId { get; set; }
    public Role Role { get; set; } = null!;

    public int PermissionId { get; set; }
    public Permission Permission { get; set; } = null!;
}
