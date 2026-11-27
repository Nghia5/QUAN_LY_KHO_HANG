import api from './client';

export function extractItems<T>(data: any): T[] {
  if (!data) return [];
  if (Array.isArray(data)) return data;
  if (Array.isArray(data.items)) return data.items;
  return [];
}
import type {
  LoginRequest, AuthResponse, CurrentUserProfile,
  PagedResult, UserResponse, CreateUserRequest, UpdateUserRequest,
  RoleResponse, PermissionResponse,
  ProductResponse, CreateProductRequest, UpdateProductRequest,
  SupplierResponse, CreateSupplierRequest, UpdateSupplierRequest,
  WarehouseResponse, CreateWarehouseRequest, UpdateWarehouseRequest,
  LocationResponse, LocationTreeNode, CreateLocationRequest,
  InventoryResponse,
  StockMovementResponse, StockReportRow,
  GoodsReceiptSummary, GoodsReceiptDetail, CreateGoodsReceiptRequest,
  GoodsIssueSummary, GoodsIssueDetail, CreateGoodsIssueRequest,
  StockTransferSummary, StockTransferDetail, CreateStockTransferRequest,
  StocktakeSummary, StocktakeDetail, CreateStocktakeRequest, StocktakeCountItem,
  StockAdjustmentSummary, StockAdjustmentDetail, CreateStockAdjustmentRequest,
  BatchResponse, CreateBatchRequest, ExpiryAlertRow,
} from '../types';

/* ── Auth ── */
export const authApi = {
  login: (data: LoginRequest) => api.post<AuthResponse>('/auth/login', data),
  refresh: (refreshToken: string) => api.post<AuthResponse>('/auth/refresh', { refreshToken }),
  revoke: (refreshToken?: string) => api.post('/auth/revoke', { refreshToken }),
  me: () => api.get<CurrentUserProfile>('/auth/me'),
  changePassword: (data: { currentPassword: string; newPassword: string; confirmPassword: string }) =>
    api.put('/users/me/password', data),
};

/* ── Users ── */
export const usersApi = {
  list: (params?: Record<string, unknown>) => api.get<PagedResult<UserResponse>>('/users', { params }),
  get: (id: number) => api.get<UserResponse>(`/users/${id}`),
  create: (data: CreateUserRequest) => api.post('/users', data),
  update: (id: number, data: UpdateUserRequest) => api.put(`/users/${id}`, data),
  resetPassword: (id: number, newPassword: string) => api.post(`/users/${id}/reset-password`, { newPassword }),
  delete: (id: number) => api.delete(`/users/${id}`),
  restore: (id: number) => api.post(`/users/${id}/restore`),
};

/* ── Roles ── */
export const rolesApi = {
  list: () => api.get<RoleResponse[]>('/roles'),
  get: (id: number) => api.get<RoleResponse>(`/roles/${id}`),
  create: (data: { code: string; name: string; description?: string; permissionIds: number[] }) =>
    api.post('/roles', data),
  update: (id: number, data: { name: string; description?: string; isActive: boolean; permissionIds: number[] }) =>
    api.put(`/roles/${id}`, data),
  permissions: () => api.get<PermissionResponse[]>('/roles/permissions'),
};

/* ── Products ── */
export const productsApi = {
  list: (params?: Record<string, unknown>) => api.get<PagedResult<ProductResponse>>('/products', { params }),
  get: (id: number) => api.get<ProductResponse>(`/products/${id}`),
  create: (data: CreateProductRequest) => api.post('/products', data),
  update: (id: number, data: UpdateProductRequest) => api.put(`/products/${id}`, data),
  delete: (id: number) => api.delete(`/products/${id}`),
};

/* ── Suppliers ── */
export const suppliersApi = {
  list: (params?: Record<string, unknown>) => api.get<PagedResult<SupplierResponse>>('/suppliers', { params }),
  get: (id: number) => api.get<SupplierResponse>(`/suppliers/${id}`),
  create: (data: CreateSupplierRequest) => api.post('/suppliers', data),
  update: (id: number, data: UpdateSupplierRequest) => api.put(`/suppliers/${id}`, data),
  delete: (id: number) => api.delete(`/suppliers/${id}`),
};

/* ── Warehouses ── */
export const warehousesApi = {
  list: (params?: Record<string, unknown>) => api.get<PagedResult<WarehouseResponse>>('/warehouses', { params }),
  get: (id: number) => api.get<WarehouseResponse>(`/warehouses/${id}`),
  create: (data: CreateWarehouseRequest) => api.post('/warehouses', data),
  update: (id: number, data: UpdateWarehouseRequest) => api.put(`/warehouses/${id}`, data),
  activate: (id: number) => api.patch(`/warehouses/${id}/activate`),
  deactivate: (id: number) => api.patch(`/warehouses/${id}/deactivate`),
  delete: (id: number) => api.delete(`/warehouses/${id}`),
};

/* ── Locations ── */
export const locationsApi = {
  list: (warehouseId: number, params?: Record<string, unknown>) =>
    api.get<PagedResult<LocationResponse>>(`/warehouses/${warehouseId}/locations`, { params }),
  tree: (warehouseId: number) => api.get<LocationTreeNode[]>(`/warehouses/${warehouseId}/locations/tree`),
  get: (warehouseId: number, id: number) => api.get<LocationResponse>(`/warehouses/${warehouseId}/locations/${id}`),
  create: (warehouseId: number, data: CreateLocationRequest) =>
    api.post(`/warehouses/${warehouseId}/locations`, data),
  update: (warehouseId: number, id: number, data: { name: string; description?: string }) =>
    api.put(`/warehouses/${warehouseId}/locations/${id}`, data),
  activate: (warehouseId: number, id: number) => api.patch(`/warehouses/${warehouseId}/locations/${id}/activate`),
  deactivate: (warehouseId: number, id: number) => api.patch(`/warehouses/${warehouseId}/locations/${id}/deactivate`),
  delete: (warehouseId: number, id: number) => api.delete(`/warehouses/${warehouseId}/locations/${id}`),
};

/* ── Inventory ── */
export const inventoryApi = {
  list: (params?: Record<string, unknown>) => api.get<PagedResult<InventoryResponse>>('/inventories', { params }),
  get: (id: number) => api.get<InventoryResponse>(`/inventories/${id}`),
};

/* ── Stock Movements ── */
export const stockMovementsApi = {
  list: (params?: Record<string, unknown>) => api.get<PagedResult<StockMovementResponse>>('/stock-movements', { params }),
  get: (id: number) => api.get<StockMovementResponse>(`/stock-movements/${id}`),
  report: (params: Record<string, unknown>) => api.get<PagedResult<StockReportRow>>('/stock-movements/report', { params }),
};

/* ── Goods Receipts ── */
export const goodsReceiptsApi = {
  list: (params?: Record<string, unknown>) => api.get<PagedResult<GoodsReceiptSummary>>('/goods-receipts', { params }),
  get: (id: number) => api.get<GoodsReceiptDetail>(`/goods-receipts/${id}`),
  create: (data: CreateGoodsReceiptRequest) => api.post('/goods-receipts', data),
  update: (id: number, data: Omit<CreateGoodsReceiptRequest, 'warehouseId'>) => api.put(`/goods-receipts/${id}`, data),
  delete: (id: number) => api.delete(`/goods-receipts/${id}`),
  submit: (id: number) => api.post(`/goods-receipts/${id}/submit`),
  approve: (id: number) => api.post(`/goods-receipts/${id}/approve`),
  reject: (id: number, reason: string) => api.post(`/goods-receipts/${id}/reject`, { reason }),
  reopen: (id: number) => api.post(`/goods-receipts/${id}/reopen`),
  complete: (id: number) => api.post(`/goods-receipts/${id}/complete`),
  cancel: (id: number, reason: string) => api.post(`/goods-receipts/${id}/cancel`, { reason }),
};

/* ── Goods Issues ── */
export const goodsIssuesApi = {
  list: (params?: Record<string, unknown>) => api.get<PagedResult<GoodsIssueSummary>>('/goods-issues', { params }),
  get: (id: number) => api.get<GoodsIssueDetail>(`/goods-issues/${id}`),
  create: (data: CreateGoodsIssueRequest) => api.post('/goods-issues', data),
  update: (id: number, data: Omit<CreateGoodsIssueRequest, 'warehouseId'>) => api.put(`/goods-issues/${id}`, data),
  delete: (id: number) => api.delete(`/goods-issues/${id}`),
  submit: (id: number) => api.post(`/goods-issues/${id}/submit`),
  approve: (id: number) => api.post(`/goods-issues/${id}/approve`),
  reject: (id: number, reason: string) => api.post(`/goods-issues/${id}/reject`, { reason }),
  reopen: (id: number) => api.post(`/goods-issues/${id}/reopen`),
  complete: (id: number) => api.post(`/goods-issues/${id}/complete`),
  cancel: (id: number, reason: string) => api.post(`/goods-issues/${id}/cancel`, { reason }),
};

/* ── Stock Transfers ── */
export const stockTransfersApi = {
  list: (params?: Record<string, unknown>) => api.get<PagedResult<StockTransferSummary>>('/stock-transfers', { params }),
  get: (id: number) => api.get<StockTransferDetail>(`/stock-transfers/${id}`),
  create: (data: CreateStockTransferRequest) => api.post('/stock-transfers', data),
  update: (id: number, data: Omit<CreateStockTransferRequest, 'warehouseId' | 'destinationWarehouseId'>) =>
    api.put(`/stock-transfers/${id}`, data),
  delete: (id: number) => api.delete(`/stock-transfers/${id}`),
  submit: (id: number) => api.post(`/stock-transfers/${id}/submit`),
  approve: (id: number) => api.post(`/stock-transfers/${id}/approve`),
  reject: (id: number, reason: string) => api.post(`/stock-transfers/${id}/reject`, { reason }),
  reopen: (id: number) => api.post(`/stock-transfers/${id}/reopen`),
  pick: (id: number) => api.post(`/stock-transfers/${id}/pick`),
  ship: (id: number) => api.post(`/stock-transfers/${id}/ship`),
  receive: (id: number) => api.post(`/stock-transfers/${id}/receive`),
  complete: (id: number) => api.post(`/stock-transfers/${id}/complete`),
  cancel: (id: number, reason: string) => api.post(`/stock-transfers/${id}/cancel`, { reason }),
};

/* ── Stocktakes ── */
export const stocktakesApi = {
  list: (params?: Record<string, unknown>) => api.get<PagedResult<StocktakeSummary>>('/stocktakes', { params }),
  get: (id: number) => api.get<StocktakeDetail>(`/stocktakes/${id}`),
  create: (data: CreateStocktakeRequest) => api.post('/stocktakes', data),
  update: (id: number, data: { scopeLocationIds?: number[]; note?: string }) => api.put(`/stocktakes/${id}`, data),
  delete: (id: number) => api.delete(`/stocktakes/${id}`),
  snapshot: (id: number) => api.post(`/stocktakes/${id}/snapshot`),
  counts: (id: number, items: StocktakeCountItem[]) => api.put(`/stocktakes/${id}/counts`, { items }),
  submit: (id: number) => api.post(`/stocktakes/${id}/submit`),
  approve: (id: number) => api.post(`/stocktakes/${id}/approve`),
  reject: (id: number, reason: string) => api.post(`/stocktakes/${id}/reject`, { reason }),
  adjust: (id: number) => api.post(`/stocktakes/${id}/adjust`),
  complete: (id: number) => api.post(`/stocktakes/${id}/complete`),
  cancel: (id: number, reason: string) => api.post(`/stocktakes/${id}/cancel`, { reason }),
  differences: (id: number) => api.get(`/stocktakes/${id}/differences`),
};

/* ── Stock Adjustments ── */
export const stockAdjustmentsApi = {
  list: (params?: Record<string, unknown>) => api.get<PagedResult<StockAdjustmentSummary>>('/stock-adjustments', { params }),
  get: (id: number) => api.get<StockAdjustmentDetail>(`/stock-adjustments/${id}`),
  create: (data: CreateStockAdjustmentRequest) => api.post('/stock-adjustments', data),
  update: (id: number, data: Omit<CreateStockAdjustmentRequest, 'warehouseId'>) =>
    api.put(`/stock-adjustments/${id}`, data),
  delete: (id: number) => api.delete(`/stock-adjustments/${id}`),
  submit: (id: number) => api.post(`/stock-adjustments/${id}/submit`),
  approve: (id: number) => api.post(`/stock-adjustments/${id}/approve`),
  reject: (id: number, reason: string) => api.post(`/stock-adjustments/${id}/reject`, { reason }),
  reopen: (id: number) => api.post(`/stock-adjustments/${id}/reopen`),
  cancel: (id: number, reason: string) => api.post(`/stock-adjustments/${id}/cancel`, { reason }),
};

/* ── Batches ── */
export const batchesApi = {
  list: (params?: Record<string, unknown>) => api.get<PagedResult<BatchResponse>>('/batches', { params }),
  get: (id: number) => api.get('/batches/' + id),
  create: (data: CreateBatchRequest) => api.post('/batches', data),
  update: (id: number, data: { manufactureDate?: string; expiryDate?: string; note?: string }) =>
    api.put(`/batches/${id}`, data),
  delete: (id: number) => api.delete(`/batches/${id}`),
  expiryAlerts: (params?: Record<string, unknown>) => api.get<PagedResult<ExpiryAlertRow>>('/batches/expiry-alerts', { params }),
  pickSuggestion: (params: Record<string, unknown>) => api.get('/batches/pick-suggestion', { params }),
};
