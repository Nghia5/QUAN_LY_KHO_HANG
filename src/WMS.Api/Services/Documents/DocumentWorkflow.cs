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
    Receive
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

    public void Reject(WarehouseDocument document, string reason)
    {
        EnsureCan(document, DocumentAction.Reject);
        EnsureNotOwnDocument(document);
        document.Status = DocumentStatus.Rejected;
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
