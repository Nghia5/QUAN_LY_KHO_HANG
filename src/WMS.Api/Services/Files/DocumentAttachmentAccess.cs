using Microsoft.EntityFrameworkCore;
using WMS.Api.Authorization;
using WMS.Api.Constants;
using WMS.Api.Data;
using WMS.Api.Exceptions;
using WMS.Api.Models.Documents;

namespace WMS.Api.Services.Files;

public sealed class DocumentAttachmentAccess(WmsDbContext db, WarehouseAccess access)
{
    public async Task RequireAsync(
        WarehouseDocumentType type, int id, bool write, CancellationToken cancellationToken)
    {
        var (document, view, create, approve) = type switch
        {
            WarehouseDocumentType.Receipt => (await FindAsync<GoodsReceipt>(id, cancellationToken),
                Permissions.GoodsReceiptView, Permissions.GoodsReceiptCreate, Permissions.GoodsReceiptApprove),
            WarehouseDocumentType.Issue => (await FindAsync<GoodsIssue>(id, cancellationToken),
                Permissions.GoodsIssueView, Permissions.GoodsIssueCreate, Permissions.GoodsIssueApprove),
            WarehouseDocumentType.Transfer => (await FindAsync<StockTransfer>(id, cancellationToken),
                Permissions.StockTransferView, Permissions.StockTransferCreate, Permissions.StockTransferApprove),
            WarehouseDocumentType.Stocktake => (await FindAsync<Stocktake>(id, cancellationToken),
                Permissions.StocktakeView, Permissions.StocktakeCreate, Permissions.StocktakeApprove),
            WarehouseDocumentType.Adjustment => (await FindAsync<StockAdjustment>(id, cancellationToken),
                Permissions.StockAdjustmentView, Permissions.StockAdjustmentCreate, Permissions.StockAdjustmentApprove),
            _ => throw new BusinessRuleException("Unknown warehouse document type.")
        };

        document = document ?? throw new NotFoundException("Warehouse document", id);
        if (await CanAccessAsync(document.WarehouseId, create, approve) ||
            document is StockTransfer transfer && transfer.DestinationWarehouseId != transfer.WarehouseId &&
            await CanAccessAsync(transfer.DestinationWarehouseId, Permissions.StockTransferReceive, Permissions.StockTransferComplete))
        {
            if (write && document.Status is DocumentStatus.Completed or DocumentStatus.Cancelled)
            {
                throw new BusinessRuleException("Attachments cannot be changed after completion or cancellation.");
            }

            return;
        }

        throw new ForbiddenException("The account does not have the required document access.");

        async Task<bool> CanAccessAsync(int warehouseId, string editPermission, string approvalPermission) =>
            await access.HasAsync(view, warehouseId, cancellationToken) &&
            (!write || await access.HasAsync(editPermission, warehouseId, cancellationToken) ||
                await access.HasAsync(approvalPermission, warehouseId, cancellationToken));
    }

    private async Task<WarehouseDocument?> FindAsync<T>(int id, CancellationToken cancellationToken)
        where T : WarehouseDocument =>
        await db.Set<T>().AsNoTracking().SingleOrDefaultAsync(d => d.Id == id, cancellationToken);
}
