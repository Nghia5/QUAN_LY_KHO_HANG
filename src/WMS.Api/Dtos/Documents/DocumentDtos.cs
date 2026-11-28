namespace WMS.Api.Dtos.Documents;

/// <summary>Body for reject/cancel: a reason is mandatory.</summary>
public sealed record DocumentReasonRequest
{
    public required string Reason { get; init; }
}

/// <summary>Who did what and when on a document (shown in the detail screen).</summary>
public sealed record DocumentWorkflowInfo(
    int? CreatedBy,
    DateTime CreatedAt,
    int? SubmittedBy,
    DateTime? SubmittedAt,
    int? ApprovedBy,
    DateTime? ApprovedAt,
    int? RejectedBy,
    DateTime? RejectedAt,
    string? RejectionReason,
    int? CompletedBy,
    DateTime? CompletedAt,
    int? CancelledBy,
    DateTime? CancelledAt,
    string? CancellationReason);
