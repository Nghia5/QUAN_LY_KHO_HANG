using System.ComponentModel.DataAnnotations;

namespace WMS.Api.Models;

/// <summary>
/// Granular permission code (matching Permissions.cs constants like WAREHOUSE_VIEW, USER_CREATE).
/// </summary>
public sealed class Permission
{
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    public required string Code { get; set; }

    [Required]
    [MaxLength(50)]
    public required string Group { get; set; }

    [MaxLength(250)]
    public string? Description { get; set; }

    // Navigation properties
    public ICollection<RolePermission> RolePermissions { get; set; } = [];
}
