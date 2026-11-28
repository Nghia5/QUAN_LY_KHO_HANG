namespace WMS.Api.Models.Documents;

/// <summary>Unified document status (spec section 5).</summary>
public enum DocumentStatus
{
    Draft = 1,
    PendingApproval = 2,
    Approved = 3,
    InProgress = 4,
    Completed = 5,
    Rejected = 6,
    Cancelled = 7,

    /// <summary>Transfer: goods picked at the source, still reserved there.</summary>
    Picked = 8,

    /// <summary>Transfer: goods left the source (TRANSFER_OUT posted), not yet received.</summary>
    InTransit = 9,

    /// <summary>Transfer: goods received at the destination (TRANSFER_IN posted).</summary>
    Received = 10,

    /// <summary>Stocktake: differences posted as stock movements.</summary>
    Adjusted = 11
}

/// <summary>Which state machine a document follows.</summary>
public enum DocumentFlow
{
    Standard,
    Transfer,
    Stocktake
}

/// <summary>
/// Common header of warehouse documents (receipt, issue, transfer, stocktake, adjustment).
/// Not mapped as an EF hierarchy: each document has its own table, configured with
/// <c>DocumentConfigurationExtensions.ConfigureDocument</c>.
/// </summary>
public abstract class WarehouseDocument
{
    public int Id { get; set; }

    /// <summary>Generated number, e.g. GR-2026-000001. Unique per document type.</summary>
    public string DocumentNo { get; set; } = "";

    /// <summary>Owning warehouse. Immutable after creation (lines reference (Id, WarehouseId)).</summary>
    public int WarehouseId { get; set; }

    public DocumentStatus Status { get; set; } = DocumentStatus.Draft;

    public string? Note { get; set; }

    public int? CreatedBy { get; set; }

    public DateTime CreatedAt { get; set; }

    public int? UpdatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public int? SubmittedBy { get; set; }

    public DateTime? SubmittedAt { get; set; }

    public int? ApprovedBy { get; set; }

    public DateTime? ApprovedAt { get; set; }

    public int? RejectedBy { get; set; }

    public DateTime? RejectedAt { get; set; }

    public string? RejectionReason { get; set; }

    public int? CompletedBy { get; set; }

    public DateTime? CompletedAt { get; set; }

    public int? CancelledBy { get; set; }

    public DateTime? CancelledAt { get; set; }

    public string? CancellationReason { get; set; }

    /// <summary>Optimistic concurrency: two users cannot move the same document at the same time.</summary>
    public byte[] RowVersion { get; set; } = [];

    public Warehouse? Warehouse { get; set; }

    /// <summary>State machine of this document type (not mapped: read-only property).</summary>
    public virtual DocumentFlow Flow => DocumentFlow.Standard;
}
