using Microsoft.Extensions.Options;
using WMS.Api.Common;
using WMS.Api.Exceptions;
using WMS.Api.Models.Documents;

namespace WMS.Api.Services.Documents;

/// <summary>Warehouse module settings (section "Warehouse" in appsettings).</summary>
public sealed class WarehouseOptions
{
    public const string SectionName = "Warehouse";

    /// <summary>
    /// Four-eyes rule: when false (default) the creator of a document cannot approve it.
    /// </summary>
    public bool AllowSelfApproval { get; set; }

    /// <summary>
    /// When true (default) stock adjustments must be approved by a STOCK_ADJUSTMENT_APPROVE user;
    /// when false, submitting an adjustment applies it immediately.
    /// </summary>
    public bool RequireAdjustmentApproval { get; set; } = true;

    /// <summary>A lot is NEAR_EXPIRY when 0 &lt;= days to expiry &lt;= this value (spec section 7).</summary>
    public int NearExpiryDays { get; set; } = 30;
}

/// <summary>Workflow actions; also returned to the UI as the actions currently allowed.</summary>
public enum DocumentAction
{
    Edit,
    Delete,
    Submit,
    Approve,
    Reject,
    Reopen,
    Complete,
    Cancel,
    Pick,
    Ship,
    Receive,
    Snapshot,
    Count,
    Adjust
}

/// <summary>
/// State machine shared by warehouse documents.
/// Standard flow: Draft → PendingApproval → Approved → Completed (+ Rejected → Draft, Cancel).
/// Transfer flow: ... Approved → Picked → InTransit → Received → Completed.
/// Each method validates the current status, stamps who/when and throws 422 on an invalid transition.
/// </summary>
public sealed class DocumentWorkflow(ICurrentUser currentUser, TimeProvider timeProvider, IOptions<WarehouseOptions> options)
{
    private DateTime Now => timeProvider.GetUtcNow().UtcDateTime;

    public static IReadOnlyList<DocumentAction> AvailableActions(DocumentStatus status, DocumentFlow flow = DocumentFlow.Standard) =>
        (status, flow) switch
        {
            // Stocktake: Draft → snapshot → InProgress (count) → submit → PendingApproval → approve
            // → Approved → adjust → Adjusted → complete. Reject sends it back to InProgress (recount).
            (DocumentStatus.Draft, DocumentFlow.Stocktake) => [DocumentAction.Edit, DocumentAction.Delete, DocumentAction.Snapshot, DocumentAction.Cancel],
            (DocumentStatus.InProgress, DocumentFlow.Stocktake) => [DocumentAction.Count, DocumentAction.Submit, DocumentAction.Cancel],
            (DocumentStatus.Approved, DocumentFlow.Stocktake) => [DocumentAction.Adjust, DocumentAction.Cancel],
            (DocumentStatus.Adjusted, DocumentFlow.Stocktake) => [DocumentAction.Complete],

            (DocumentStatus.Draft, _) => [DocumentAction.Edit, DocumentAction.Delete, DocumentAction.Submit, DocumentAction.Cancel],
            (DocumentStatus.PendingApproval, _) => [DocumentAction.Approve, DocumentAction.Reject, DocumentAction.Cancel],
            (DocumentStatus.Rejected, _) => [DocumentAction.Reopen, DocumentAction.Cancel],
            (DocumentStatus.Approved, DocumentFlow.Standard) => [DocumentAction.Complete, DocumentAction.Cancel],
            (DocumentStatus.Approved, DocumentFlow.Transfer) => [DocumentAction.Pick, DocumentAction.Cancel],
            (DocumentStatus.Picked, DocumentFlow.Transfer) => [DocumentAction.Ship, DocumentAction.Cancel],
            (DocumentStatus.InTransit, DocumentFlow.Transfer) => [DocumentAction.Receive],
            (DocumentStatus.Received, DocumentFlow.Transfer) => [DocumentAction.Complete],
            _ => []
        };

    /// <summary>Moves a document to <paramref name="status"/> through <paramref name="action"/> (used by flow-specific steps).</summary>
    public void Advance(WarehouseDocument document, DocumentAction action, DocumentStatus status)
    {
        EnsureCan(document, action);
        document.Status = status;
        Stamp(document);
    }

    public int? CurrentUserId => currentUser.UserId;

    public DateTime UtcNow => Now;

    public void EnsureCan(WarehouseDocument document, DocumentAction action)
    {
        if ((action is DocumentAction.Edit or DocumentAction.Delete or DocumentAction.Submit or DocumentAction.Reopen ||
            action == DocumentAction.Cancel && DocumentStatuses.BeforeApproval.Contains(document.Status)) &&
            document.CreatedBy is int ownerId && ownerId != currentUser.UserId)
        {
            throw new ForbiddenException("Only the document creator can perform this action.");
        }

        if (!AvailableActions(document.Status, document.Flow).Contains(action))
        {
            throw new BusinessRuleException(
                $"Cannot {action.ToString().ToLowerInvariant()} document {document.DocumentNo} in status {document.Status}.");
        }
    }

    public void Stamp(WarehouseDocument document, int? userId = null)
    {
        document.UpdatedAt = Now;
        document.UpdatedBy = userId ?? currentUser.UserId;
    }

    public void Created(WarehouseDocument document)
    {
        document.Status = DocumentStatus.Draft;
        document.CreatedAt = Now;
        document.CreatedBy = currentUser.UserId;
    }

    public void Submit(WarehouseDocument document)
    {
        EnsureCan(document, DocumentAction.Submit);
        document.Status = DocumentStatus.PendingApproval;
        document.SubmittedAt = Now;
        document.SubmittedBy = currentUser.UserId;
        Stamp(document);
    }

    public void Approve(WarehouseDocument document)
    {
        EnsureCan(document, DocumentAction.Approve);
        EnsureNotOwnDocument(document);
        document.Status = DocumentStatus.Approved;
        document.ApprovedAt = Now;
        document.ApprovedBy = currentUser.UserId;
        Stamp(document);
    }

    public RequirementsInfo Requirements => new(options.Value.RequireAdjustmentApproval);

    /// <summary>
    /// Approval granted by policy (no approver required, e.g. adjustments when
    /// RequireAdjustmentApproval = false). Recorded as approved by the submitter.
    /// </summary>
    public void ApproveByPolicy(WarehouseDocument document)
    {
        EnsureCan(document, DocumentAction.Approve);
        document.Status = DocumentStatus.Approved;
        document.ApprovedAt = Now;
        document.ApprovedBy = currentUser.UserId;
        Stamp(document);
    }

    public sealed record RequirementsInfo(bool AdjustmentApproval);

    public void Reject(WarehouseDocument document, string reason)
    {
        EnsureCan(document, DocumentAction.Reject);
        EnsureNotOwnDocument(document);
        // A rejected stocktake goes straight back to counting; other documents wait for "reopen".
        document.Status = document.Flow == DocumentFlow.Stocktake ? DocumentStatus.InProgress : DocumentStatus.Rejected;
        document.RejectedAt = Now;
        document.RejectedBy = currentUser.UserId;
        document.RejectionReason = reason.Trim();
        Stamp(document);
    }

    public void Reopen(WarehouseDocument document)
    {
        EnsureCan(document, DocumentAction.Reopen);
        document.Status = DocumentStatus.Draft;
        Stamp(document);
    }

    public void Complete(WarehouseDocument document)
    {
        EnsureCan(document, DocumentAction.Complete);
        document.Status = DocumentStatus.Completed;
        document.CompletedAt = Now;
        document.CompletedBy = currentUser.UserId;
        Stamp(document);
    }

    public void Cancel(WarehouseDocument document, string reason)
    {
        EnsureCan(document, DocumentAction.Cancel);
        document.Status = DocumentStatus.Cancelled;
        document.CancelledAt = Now;
        document.CancelledBy = currentUser.UserId;
        document.CancellationReason = reason.Trim();
        Stamp(document);
    }

    private void EnsureNotOwnDocument(WarehouseDocument document)
    {
        if (!options.Value.AllowSelfApproval && document.CreatedBy is not null && document.CreatedBy == currentUser.UserId)
        {
            throw new BusinessRuleException(
                $"Document {document.DocumentNo} must be approved or rejected by someone other than its creator.");
        }
    }
}
