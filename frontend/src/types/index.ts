export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export interface PagedRequest {
  page?: number;
  pageSize?: number;
  keyword?: string;
  sortBy?: string;
  sortDir?: 'asc' | 'desc';
}

/* ── Auth ── */
export interface LoginRequest {
  usernameOrEmail: string;
  password: string;
}

export interface AuthResponse {
  accessToken: string;
  refreshToken: string;
  expiresAt: string;
  user: AuthUserSummary;
}

export interface AuthUserSummary {
  id: number;
  username: string;
  email: string;
  fullName: string;
  avatarUrl?: string;
  roles: string[];
}

export interface CurrentUserProfile {
  id: number;
  username: string;
  email: string;
  fullName: string;
  phoneNumber?: string;
  avatarUrl?: string;
  isActive: boolean;
  roles: string[];
  permissions: string[];
}

/* ── Users ── */
export interface UserResponse {
  id: number;
  username: string;
  email: string;
  fullName: string;
  phoneNumber?: string;
  avatarUrl?: string;
  isActive: boolean;
  createdAt: string;
  updatedAt?: string;
  deletedAt?: string;
  roles: string[];
  warehouseRoles?: UserWarehouseRole[];
}

export interface UserWarehouseRole {
  warehouseId: number;
  warehouseCode: string;
  roleId: number;
  roleCode: string;
  roleName: string;
}

export interface UserDetailResponse extends UserResponse {
  deletedBy?: number;
  permissions: string[];
}

export interface CreateUserRequest {
  username: string;
  email: string;
  password: string;
  fullName: string;
  phoneNumber?: string;
  avatarUrl?: string;
  roleIds: number[];
}

export interface UpdateUserRequest {
  fullName: string;
  email: string;
  phoneNumber?: string;
  avatarUrl?: string;
  roleIds: number[];
}

/* ── Roles ── */
export interface RoleSummary {
  id: number;
  code: string;
  name: string;
}

export interface PermissionResponse {
  id: number;
  code: string;
  group: string;
  description?: string;
}

export interface RoleResponse {
  id: number;
  code: string;
  name: string;
  description?: string;
  isActive: boolean;
  permissions: PermissionResponse[];
}

/* ── Products ── */
export interface ProductResponse {
  id: number;
  sku: string;
  name: string;
  description?: string;
  category: string;
  unitOfMeasure: string;
  salePrice: number;
  isActive: boolean;
}

export interface CreateProductRequest {
  sku: string;
  name: string;
  description?: string;
  category: string;
  unitOfMeasure: string;
  salePrice: number;
  isActive?: boolean;
}

export interface UpdateProductRequest extends CreateProductRequest {}

/* ── Suppliers ── */
export interface SupplierResponse {
  id: number;
  code: string;
  name: string;
  phone?: string;
  email?: string;
  address?: string;
  isActive: boolean;
  createdAt: string;
  updatedAt?: string;
}

export interface CreateSupplierRequest {
  code: string;
  name: string;
  phone?: string;
  email?: string;
  address?: string;
}

export interface UpdateSupplierRequest extends CreateSupplierRequest {
  isActive: boolean;
}

/* ── Warehouses ── */
export interface WarehouseResponse {
  id: number;
  code: string;
  name: string;
  address?: string;
  phone?: string;
  description?: string;
  managerId?: number;
  isActive: boolean;
  createdAt: string;
  updatedAt?: string;
}

export interface CreateWarehouseRequest {
  code: string;
  name: string;
  address?: string;
  phone?: string;
  description?: string;
  managerId?: number;
}

export interface UpdateWarehouseRequest {
  name: string;
  address?: string;
  phone?: string;
  description?: string;
  managerId?: number;
}

/* ── Locations ── */
export type LocationType = 'Zone' | 'Rack' | 'Shelf' | 'Bin';

export interface LocationResponse {
  id: number;
  warehouseId: number;
  parentId?: number;
  type: LocationType;
  segmentCode: string;
  code: string;
  name: string;
  description?: string;
  isActive: boolean;
  createdAt: string;
  updatedAt?: string;
}

export interface LocationTreeNode {
  id: number;
  type: LocationType;
  code: string;
  name: string;
  isActive: boolean;
  children: LocationTreeNode[];
}

export interface CreateLocationRequest {
  type: number;
  parentId?: number;
  segmentCode: string;
  name: string;
  description?: string;
}

/* ── Inventory ── */
export type StockStatus = 'InStock' | 'LowStock' | 'OutOfStock';

export interface InventoryResponse {
  id: number;
  productId: number;
  productSku: string;
  productName: string;
  unitOfMeasure: string;
  warehouseId: number;
  warehouseCode: string;
  warehouseName: string;
  locationId: number;
  locationCode: string;
  quantity: number;
  reservedQuantity: number;
  availableQuantity: number;
  minQuantity: number;
  stockStatus: number;
  updatedAt?: string;
}

/* ── Stock Movements ── */
export interface StockMovementResponse {
  id: number;
  inventoryId: number;
  productId: number;
  productSku: string;
  productName: string;
  warehouseId: number;
  warehouseCode: string;
  locationId: number;
  locationCode: string;
  movementType: number;
  quantity: number;
  quantityBefore: number;
  quantityAfter: number;
  referenceType: string;
  referenceId?: number;
  referenceNo?: string;
  note?: string;
  createdBy?: number;
  createdAt: string;
}

export interface StockReportRow {
  productId: number;
  productSku: string;
  productName: string;
  unitOfMeasure: string;
  warehouseId: number;
  warehouseCode: string;
  openingQuantity: number;
  inQuantity: number;
  outQuantity: number;
  closingQuantity: number;
}

/* ── Document shared types ── */
export type DocumentStatus = 'Draft' | 'Submitted' | 'Approved' | 'Rejected' | 'Completed' | 'Cancelled';

export interface DocumentWorkflowInfo {
  createdBy?: number;
  createdAt: string;
  submittedBy?: number;
  submittedAt?: string;
  approvedBy?: number;
  approvedAt?: string;
  rejectedBy?: number;
  rejectedAt?: string;
  rejectionReason?: string;
  completedBy?: number;
  completedAt?: string;
  cancelledBy?: number;
  cancelledAt?: string;
  cancellationReason?: string;
}

export interface DocumentAction {
  action: string;
  label: string;
}

/* ── Goods Receipts ── */
export interface GoodsReceiptLineRequest {
  productId: number;
  locationId: number;
  quantity: number;
  unitCost?: number;
  batchCode?: string;
  manufactureDate?: string;
  expiryDate?: string;
  note?: string;
}

export interface CreateGoodsReceiptRequest {
  warehouseId: number;
  supplierId?: number;
  supplierName?: string;
  referenceNo?: string;
  receiptDate?: string;
  note?: string;
  lines: GoodsReceiptLineRequest[];
}

export interface GoodsReceiptSummary {
  id: number;
  documentNo: string;
  warehouseId: number;
  warehouseCode: string;
  status: DocumentStatus;
  supplierId?: number;
  supplierName?: string;
  referenceNo?: string;
  receiptDate: string;
  lineCount: number;
  totalQuantity: number;
  createdBy?: number;
  createdAt: string;
}

export interface GoodsReceiptLineResponse {
  id: number;
  productId: number;
  productSku: string;
  productName: string;
  unitOfMeasure: string;
  locationId: number;
  locationCode: string;
  quantity: number;
  unitCost?: number;
  batchCode?: string;
  manufactureDate?: string;
  expiryDate?: string;
  note?: string;
}

export interface GoodsReceiptDetail {
  id: number;
  documentNo: string;
  warehouseId: number;
  warehouseCode: string;
  warehouseName: string;
  status: DocumentStatus;
  supplierId?: number;
  supplierName?: string;
  referenceNo?: string;
  receiptDate: string;
  note?: string;
  totalQuantity: number;
  lines: GoodsReceiptLineResponse[];
  workflow: DocumentWorkflowInfo;
  availableActions: DocumentAction[];
}

/* ── Goods Issues ── */
export interface GoodsIssueLineRequest {
  productId: number;
  locationId: number;
  quantity: number;
  batchId?: number;
  note?: string;
}

export interface CreateGoodsIssueRequest {
  warehouseId: number;
  customerId?: number;
  customerName?: string;
  referenceNo?: string;
  issueDate?: string;
  note?: string;
  lines: GoodsIssueLineRequest[];
}

export interface GoodsIssueSummary {
  id: number;
  documentNo: string;
  warehouseId: number;
  warehouseCode: string;
  status: DocumentStatus;
  customerId?: number;
  customerName?: string;
  referenceNo?: string;
  issueDate: string;
  lineCount: number;
  totalQuantity: number;
  createdBy?: number;
  createdAt: string;
}

export interface GoodsIssueDetail {
  id: number;
  documentNo: string;
  warehouseId: number;
  warehouseCode: string;
  warehouseName: string;
  status: DocumentStatus;
  customerId?: number;
  customerName?: string;
  referenceNo?: string;
  issueDate: string;
  note?: string;
  totalQuantity: number;
  lines: GoodsIssueLineResponse[];
  workflow: DocumentWorkflowInfo;
  availableActions: DocumentAction[];
}

export interface GoodsIssueLineResponse {
  id: number;
  productId: number;
  productSku: string;
  productName: string;
  unitOfMeasure: string;
  locationId: number;
  locationCode: string;
  quantity: number;
  availableQuantity: number;
  batchId?: number;
  batchCode?: string;
  note?: string;
}

/* ── Stock Transfers ── */
export interface StockTransferLineRequest {
  productId: number;
  fromLocationId: number;
  toLocationId: number;
  quantity: number;
  note?: string;
}

export interface CreateStockTransferRequest {
  warehouseId: number;
  destinationWarehouseId: number;
  reason?: string;
  note?: string;
  lines: StockTransferLineRequest[];
}

export interface StockTransferSummary {
  id: number;
  documentNo: string;
  sourceWarehouseId: number;
  sourceWarehouseCode: string;
  destinationWarehouseId: number;
  destinationWarehouseCode: string;
  isInternal: boolean;
  status: DocumentStatus;
  lineCount: number;
  totalQuantity: number;
  createdBy?: number;
  createdAt: string;
}

export interface StockTransferDetail {
  id: number;
  documentNo: string;
  sourceWarehouseId: number;
  sourceWarehouseCode: string;
  destinationWarehouseId: number;
  destinationWarehouseCode: string;
  isInternal: boolean;
  status: DocumentStatus;
  reason?: string;
  note?: string;
  totalQuantity: number;
  lines: StockTransferLineResponse[];
  workflow: DocumentWorkflowInfo;
  progress: TransferProgressInfo;
  availableActions: DocumentAction[];
}

export interface StockTransferLineResponse {
  id: number;
  productId: number;
  productSku: string;
  productName: string;
  unitOfMeasure: string;
  fromLocationId: number;
  fromLocationCode: string;
  toLocationId: number;
  toLocationCode: string;
  quantity: number;
  availableAtSource: number;
  note?: string;
}

export interface TransferProgressInfo {
  pickedBy?: number;
  pickedAt?: string;
  shippedBy?: number;
  shippedAt?: string;
  receivedBy?: number;
  receivedAt?: string;
}

/* ── Stocktakes ── */
export interface CreateStocktakeRequest {
  warehouseId: number;
  scopeLocationIds?: number[];
  note?: string;
}

export interface StocktakeCountItem {
  productId: number;
  locationId: number;
  countedQuantity: number;
  note?: string;
}

export interface StocktakeSummary {
  id: number;
  documentNo: string;
  warehouseId: number;
  warehouseCode: string;
  status: DocumentStatus;
  lineCount: number;
  countedLines: number;
  linesWithDifference: number;
  createdBy?: number;
  createdAt: string;
}

export interface StocktakeDetail {
  id: number;
  documentNo: string;
  warehouseId: number;
  warehouseCode: string;
  status: DocumentStatus;
  scopeLocationIds: number[];
  note?: string;
  snapshotBy?: number;
  snapshotAt?: string;
  adjustedBy?: number;
  adjustedAt?: string;
  totals: StocktakeTotals;
  lines: StocktakeLineResponse[];
  workflow: DocumentWorkflowInfo;
  availableActions: DocumentAction[];
}

export interface StocktakeTotals {
  lineCount: number;
  countedLines: number;
  uncountedLines: number;
  linesWithDifference: number;
  totalSurplus: number;
  totalShortage: number;
}

export interface StocktakeLineResponse {
  id: number;
  productId: number;
  productSku: string;
  productName: string;
  unitOfMeasure: string;
  locationId: number;
  locationCode: string;
  systemQuantity: number;
  countedQuantity?: number;
  difference?: number;
  isAddedDuringCount: boolean;
  countedBy?: number;
  countedAt?: string;
  note?: string;
}

/* ── Stock Adjustments ── */
export type AdjustmentReason = 'Damage' | 'Theft' | 'Correction' | 'Return' | 'Other';

export interface StockAdjustmentLineRequest {
  productId: number;
  locationId: number;
  adjustmentQuantity: number;
  note?: string;
}

export interface CreateStockAdjustmentRequest {
  warehouseId: number;
  reasonCode: number;
  reason: string;
  note?: string;
  lines: StockAdjustmentLineRequest[];
}

export interface StockAdjustmentSummary {
  id: number;
  documentNo: string;
  warehouseId: number;
  warehouseCode: string;
  status: DocumentStatus;
  reasonCode: number;
  reason: string;
  lineCount: number;
  totalIncrease: number;
  totalDecrease: number;
  createdBy?: number;
  createdAt: string;
}

export interface StockAdjustmentDetail {
  id: number;
  documentNo: string;
  warehouseId: number;
  warehouseCode: string;
  status: DocumentStatus;
  reasonCode: number;
  reason: string;
  note?: string;
  requiresApproval: boolean;
  lines: StockAdjustmentLineResponse[];
  workflow: DocumentWorkflowInfo;
  availableActions: DocumentAction[];
}

export interface StockAdjustmentLineResponse {
  id: number;
  productId: number;
  productSku: string;
  productName: string;
  unitOfMeasure: string;
  locationId: number;
  locationCode: string;
  adjustmentQuantity: number;
  currentQuantity: number;
  currentReservedQuantity: number;
  expectedAfterQuantity: number;
  beforeQuantity?: number;
  afterQuantity?: number;
  note?: string;
}

/* ── Batches ── */
export interface BatchResponse {
  id: number;
  productId: number;
  productSku: string;
  productName: string;
  batchCode: string;
  manufactureDate?: string;
  expiryDate?: string;
  daysToExpiry?: number;
  expiryStatus: number;
  quantity: number;
  note?: string;
  createdAt: string;
}

export interface CreateBatchRequest {
  productId: number;
  batchCode: string;
  manufactureDate?: string;
  expiryDate?: string;
  note?: string;
}

export interface ExpiryAlertRow {
  batchId: number;
  batchCode: string;
  productId: number;
  productSku: string;
  productName: string;
  warehouseId: number;
  warehouseCode: string;
  locationId: number;
  locationCode: string;
  quantity: number;
  expiryDate: string;
  daysToExpiry: number;
  status: number;
}
