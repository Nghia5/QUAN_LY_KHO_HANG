namespace WMS.Api.Models;

/// <summary>
/// Minimal append-only audit trail (who did what to which record, when). Written in the same
/// transaction as the business change. Proposed by the warehouse module; the User/Audit module
/// (Người 3) can extend it (IP, user agent, FK to Users...).
/// </summary>
public sealed class AuditLog
{
    public long Id { get; set; }

    /// <summary>e.g. "GoodsReceipt".</summary>
    public required string EntityType { get; set; }

    public required string EntityId { get; set; }

    /// <summary>e.g. CREATED, SUBMITTED, APPROVED, COMPLETED.</summary>
    public required string Action { get; set; }

    public int? UserId { get; set; }

    public DateTime CreatedAt { get; set; }

    /// <summary>JSON with action details (document number, changed values, reason...).</summary>
    public string? Data { get; set; }
}
