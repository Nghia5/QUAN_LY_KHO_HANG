using Microsoft.EntityFrameworkCore;
using WMS.Api.Common;
using WMS.Api.Data;
using WMS.Api.Exceptions;
using WMS.Api.Models;
using WMS.Api.Models.Documents;
using WMS.Api.Services.Audit;
using WMS.Api.Services.Files;

namespace WMS.Api.Services.Documents;

/// <summary>Statuses in which a document may still change stock.</summary>
public static class DocumentStatuses
{
    public static readonly DocumentStatus[] Open =
    [
        DocumentStatus.Draft, DocumentStatus.PendingApproval, DocumentStatus.Approved, DocumentStatus.InProgress,
        DocumentStatus.Rejected, DocumentStatus.Picked, DocumentStatus.InTransit, DocumentStatus.Received,
        DocumentStatus.Adjusted
    ];

    /// <summary>Statuses before approval: cancelling them needs no approve permission.</summary>
    public static readonly DocumentStatus[] BeforeApproval =
        [DocumentStatus.Draft, DocumentStatus.PendingApproval, DocumentStatus.Rejected, DocumentStatus.InProgress];
}

/// <summary>
/// Shared workflow actions for documents (submit, approve, reject, reopen, cancel, complete, delete).
/// Every action runs in one transaction: status change (row lock + RowVersion check) → document
/// specific hook (reserve / post stock...) → audit entry → commit. Any failure rolls back everything.
/// </summary>
public abstract class DocumentServiceBase<TDocument, TDetail>(
    WmsDbContext dbContext,
    DocumentWorkflow workflow,
    IAuditLogger audit,
    ICurrentUser currentUser)
    where TDocument : WarehouseDocument
{
    protected WmsDbContext DbContext => dbContext;

    protected DocumentWorkflow Workflow => workflow;

    protected IAuditLogger Audit => audit;

    /// <summary>Audit entity type, e.g. "GoodsIssue".</summary>
    protected abstract string AuditEntity { get; }

    /// <summary>Human readable name for messages, e.g. "Goods issue".</summary>
    protected abstract string DisplayName { get; }

    /// <summary>Permission required to cancel a document that is already approved.</summary>
    protected abstract string ApprovePermission { get; }

    /// <summary>Tracked document including lines.</summary>
    protected abstract Task<TDocument?> LoadAsync(int id, CancellationToken cancellationToken);

    public abstract Task<TDetail> GetAsync(int id, CancellationToken cancellationToken);

    /// <summary>Validation before Draft → PendingApproval.</summary>
    protected virtual Task BeforeSubmitAsync(TDocument document, CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>Runs after the status became Approved, inside the transaction (e.g. reserve stock).</summary>
    /// <returns>Extra data stored in the APPROVED audit entry.</returns>
    protected virtual Task<object?> OnApprovedAsync(TDocument document, CancellationToken cancellationToken) =>
        Task.FromResult<object?>(null);

    /// <summary>Runs after the status became Cancelled, inside the transaction (e.g. release reservations).</summary>
    protected virtual Task OnCancelledAsync(TDocument document, DocumentStatus previousStatus, CancellationToken cancellationToken) =>
        Task.CompletedTask;

    /// <summary>Validation before posting (master data may have changed since approval).</summary>
    protected virtual Task BeforeCompleteAsync(TDocument document, CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>Posts stock for the document; returns extra data for the COMPLETED audit entry.</summary>
    protected abstract Task<object?> OnCompletingAsync(TDocument document, CancellationToken cancellationToken);

    public async Task<TDetail> SubmitAsync(int id, CancellationToken cancellationToken) =>
        await RunAsync(id, "SUBMITTED", async document =>
        {
            await BeforeSubmitAsync(document, cancellationToken);
            workflow.Submit(document);
            await SaveAsync(cancellationToken);
            return await OnSubmittedAsync(document, cancellationToken);
        }, cancellationToken);

    /// <summary>Runs after Draft → PendingApproval inside the transaction (e.g. auto-approval by policy).</summary>
    protected virtual Task<object?> OnSubmittedAsync(TDocument document, CancellationToken cancellationToken) =>
        Task.FromResult<object?>(null);

    public async Task<TDetail> ApproveAsync(int id, CancellationToken cancellationToken) =>
        await RunAsync(id, "APPROVED", async document =>
        {
            workflow.Approve(document);
            await SaveAsync(cancellationToken); // a concurrent approve of the same document fails here
            return await OnApprovedAsync(document, cancellationToken);
        }, cancellationToken);

    public async Task<TDetail> RejectAsync(int id, string reason, CancellationToken cancellationToken) =>
        await RunAsync(id, "REJECTED", async document =>
        {
            workflow.Reject(document, reason);
            await SaveAsync(cancellationToken);
            return new { Reason = reason };
        }, cancellationToken);

    public async Task<TDetail> ReopenAsync(int id, CancellationToken cancellationToken) =>
        await RunAsync(id, "REOPENED", async document =>
        {
            workflow.Reopen(document);
            await SaveAsync(cancellationToken);
            return null;
        }, cancellationToken);

    public async Task<TDetail> CancelAsync(int id, string reason, CancellationToken cancellationToken) =>
        await RunAsync(id, "CANCELLED", async document =>
        {
            var previous = document.Status;
            if (!DocumentStatuses.BeforeApproval.Contains(previous) &&
                !await currentUser.HasWarehousePermissionAsync(ApprovePermission, document.WarehouseId, cancellationToken))
            {
                throw new ForbiddenException($"Cancelling an approved {DisplayName.ToLowerInvariant()} requires {ApprovePermission}.");
            }

            workflow.Cancel(document, reason);
            await SaveAsync(cancellationToken);
            await OnCancelledAsync(document, previous, cancellationToken);
            return new { Reason = reason, PreviousStatus = previous.ToString() };
        }, cancellationToken);

    public async Task<TDetail> CompleteAsync(int id, CancellationToken cancellationToken) =>
        await RunAsync(id, "COMPLETED", async document =>
        {
            workflow.Complete(document);
            await BeforeCompleteAsync(document, cancellationToken);
            // Saving the status first takes the row lock and checks RowVersion, so a second
            // concurrent "complete" fails here instead of posting stock twice.
            await SaveAsync(cancellationToken);
            return await OnCompletingAsync(document, cancellationToken);
        }, cancellationToken);

    public async Task DeleteAsync(int id, CancellationToken cancellationToken)
    {
        var document = await FindAsync(id, cancellationToken);
        workflow.EnsureCan(document, DocumentAction.Delete);

        var type = DocumentAttachmentAccess.TypeOf(document);
        await using var transaction = await DocumentAttachmentAccess.BeginWriteAsync(dbContext, type, id, cancellationToken);
        if (await dbContext.Set<UploadedFile>().AnyAsync(
            file => file.DocumentType == type && file.DocumentId == id, cancellationToken))
        {
            throw new ConflictException("Remove the document attachments before deleting the document.");
        }

        audit.Log(AuditEntity, document.Id, "DELETED", new { document.DocumentNo });
        dbContext.Remove(document);
        await SaveAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    protected async Task<TDocument> FindAsync(int id, CancellationToken cancellationToken) =>
        await LoadAsync(id, cancellationToken) ?? throw new NotFoundException(DisplayName, id);

    protected async Task SaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException($"The {DisplayName.ToLowerInvariant()} was changed by someone else. Reload it and try again.");
        }
    }

    /// <summary>Loads the document, runs the action and the audit entry in one transaction.</summary>
    protected async Task<TDetail> RunAsync(
        int id,
        string auditAction,
        Func<TDocument, Task<object?>> action,
        CancellationToken cancellationToken)
    {
        await using (var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken))
        {
            var document = await FindAsync(id, cancellationToken);
            var data = await action(document);

            audit.Log(AuditEntity, document.Id, auditAction, new
            {
                document.DocumentNo,
                Status = document.Status.ToString(),
                Details = data
            });
            await SaveAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        return await GetAsync(id, cancellationToken);
    }
}
