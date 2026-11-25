namespace WMS.Api.Models;

public sealed class Warehouse
{
    public int Id { get; set; }

    /// <summary>Unique business code, stored upper-case. Immutable after creation.</summary>
    public required string Code { get; set; }

    public required string Name { get; set; }

    public string? Address { get; set; }

    public string? Phone { get; set; }

    public string? Description { get; set; }

    /// <summary>
    /// Manager user id. The FK to the User table is added once the User module (Người 3) is merged.
    /// </summary>
    public int? ManagerId { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; }

    public int? CreatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public int? UpdatedBy { get; set; }
}
