using System.ComponentModel.DataAnnotations;

namespace WMS.Api.Models;

/// <summary>
/// System role (e.g. ADMIN, WAREHOUSE_MANAGER, WAREHOUSE_STAFF, VIEWER).
/// </summary>
public sealed class Role
{
    public int Id { get; set; }

    [Required]
    [MaxLength(50)]
    public required string Code { get; set; }

    [Required]
    [MaxLength(100)]
    public required string Name { get; set; }

    [MaxLength(250)]
    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public ICollection<UserRole> UserRoles { get; set; } = [];
    public ICollection<RolePermission> RolePermissions { get; set; } = [];
}
