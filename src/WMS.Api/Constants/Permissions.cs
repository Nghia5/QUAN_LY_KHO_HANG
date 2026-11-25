using System.Reflection;

namespace WMS.Api.Constants;

/// <summary>
/// Permission codes across all modules (Warehouse, User, Platform). Each code is also the name
/// of an authorization policy that requires a <c>permission</c> claim with the same value.
/// </summary>
public static class Permissions
{
    public const string ClaimType = "permission";

    // User & Platform permissions
    public const string UserView = "USER_VIEW";
    public const string UserCreate = "USER_CREATE";
    public const string UserUpdate = "USER_UPDATE";
    public const string UserDelete = "USER_DELETE";
    public const string RoleManage = "ROLE_MANAGE";
    public const string FileUpload = "FILE_UPLOAD";
    public const string ProductView = "PRODUCT_VIEW";
    public const string ProductManage = "PRODUCT_MANAGE";
    public const string SupplierView = "SUPPLIER_VIEW";
    public const string SupplierManage = "SUPPLIER_MANAGE";

    // Warehouse & Location permissions
    public const string WarehouseView = "WAREHOUSE_VIEW";
    public const string WarehouseCreate = "WAREHOUSE_CREATE";
    public const string WarehouseUpdate = "WAREHOUSE_UPDATE";
    public const string WarehouseDelete = "WAREHOUSE_DELETE";

    public const string LocationView = "LOCATION_VIEW";
    public const string LocationCreate = "LOCATION_CREATE";
    public const string LocationUpdate = "LOCATION_UPDATE";
    public const string LocationDelete = "LOCATION_DELETE";

    // Inventory & Movements
    public const string InventoryView = "INVENTORY_VIEW";
    public const string StockMovementView = "STOCK_MOVEMENT_VIEW";

    // Documents
    public const string GoodsReceiptView = "GOODS_RECEIPT_VIEW";
    public const string GoodsReceiptCreate = "GOODS_RECEIPT_CREATE";
    public const string GoodsReceiptApprove = "GOODS_RECEIPT_APPROVE";

    public const string GoodsIssueView = "GOODS_ISSUE_VIEW";
    public const string GoodsIssueCreate = "GOODS_ISSUE_CREATE";
    public const string GoodsIssueApprove = "GOODS_ISSUE_APPROVE";

    public const string StockTransferView = "STOCK_TRANSFER_VIEW";
    public const string StockTransferCreate = "STOCK_TRANSFER_CREATE";
    public const string StockTransferApprove = "STOCK_TRANSFER_APPROVE";

    public const string StocktakeView = "STOCKTAKE_VIEW";
    public const string StocktakeCreate = "STOCKTAKE_CREATE";
    public const string StocktakeApprove = "STOCKTAKE_APPROVE";

    public const string StockAdjustmentView = "STOCK_ADJUSTMENT_VIEW";
    public const string StockAdjustmentCreate = "STOCK_ADJUSTMENT_CREATE";
    public const string StockAdjustmentApprove = "STOCK_ADJUSTMENT_APPROVE";

    public const string BatchView = "BATCH_VIEW";
    public const string BatchManage = "BATCH_MANAGE";

    public const string GoodsReceiptComplete = "GOODS_RECEIPT_COMPLETE";
    public const string GoodsIssueComplete = "GOODS_ISSUE_COMPLETE";
    public const string StockTransferPick = "STOCK_TRANSFER_PICK";
    public const string StockTransferShip = "STOCK_TRANSFER_SHIP";
    public const string StockTransferReceive = "STOCK_TRANSFER_RECEIVE";
    public const string StockTransferComplete = "STOCK_TRANSFER_COMPLETE";
    public const string StocktakeCount = "STOCKTAKE_COUNT";
    public const string StocktakeAdjust = "STOCKTAKE_ADJUST";
    public const string StocktakeComplete = "STOCKTAKE_COMPLETE";

    public static IReadOnlyList<string> All { get; } = typeof(Permissions)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(field => field.IsLiteral && field.Name != nameof(ClaimType))
        .Select(field => (string)field.GetRawConstantValue()!)
        .ToArray();
}
