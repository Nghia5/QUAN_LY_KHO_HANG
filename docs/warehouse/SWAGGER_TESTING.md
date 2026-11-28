# Test API module Kho bằng Swagger

Mục lục:

0. [Cài đặt và lấy token](#0-cài-đặt-và-lấy-token)
1. [Dữ liệu mẫu dùng chung](#1-dữ-liệu-mẫu-dùng-chung)
2. [W-02 Kho (Warehouse)](#2-w-02-kho)
3. [W-03 Vị trí kho (Location)](#3-w-03-vị-trí-kho)
4. [W-04/W-05 Tồn kho (Inventory) + lọc/phân trang](#4-w-04w-05-tồn-kho)
5. [W-06 Sổ biến động kho (Stock Movement) + báo cáo NXT](#5-w-06-sổ-biến-động-kho)
6. [W-07 Phiếu nhập (Goods Receipt) + Nhà cung cấp](#6-w-07-phiếu-nhập)
7. [W-08 Phiếu xuất (Goods Issue)](#7-w-08-phiếu-xuất)
8. [W-09 Chuyển kho (Stock Transfer)](#8-w-09-chuyển-kho)
9. [W-10 Kiểm kê (Stocktake)](#9-w-10-kiểm-kê)
10. [W-11 Điều chỉnh tồn (Stock Adjustment)](#10-w-11-điều-chỉnh-tồn)
11. [W-12 Lô / hạn dùng (Batch)](#11-w-12-lô--hạn-dùng)
12. [Mã lỗi thường gặp](#12-mã-lỗi-thường-gặp)

Quy ước: `{id}` là id lấy từ response của bước trước. Ngày theo dạng `yyyy-MM-dd`.
Enum gửi dạng **tên** (`"Zone"`, `"Damaged"`, `"Fefo"`...).

---

## 0. Cài đặt và lấy token

### Kiểm tra database Stage 7

Cấu hình chuỗi kết nối và JWT cho môi trường Development bằng .NET User Secrets. Xem [hướng dẫn phân quyền](ACCESS_CONTROL.md) để chạy migration, tạo tài khoản mẫu và phân công vai trò theo kho.

1. Từ thư mục gốc, chạy `dotnet run --project src/WMS.Api --launch-profile http`.
2. Mở **http://localhost:5237/swagger**.
3. Gọi `POST /api/auth/login` bằng tài khoản thử nghiệm. Sao chép `accessToken` từ phản hồi.
4. Bấm **Authorize**, dán `accessToken` và xác nhận. Với `stage7.staff`, gọi `GET /api/inventories` với `keyword=S7-SKU-01` để xem tồn mẫu tại `S7-MAIN`.

Dùng `stage7.staff` để lập và gửi phiếu, `stage7.manager` để duyệt và hoàn tất, và `stage7.admin` để quản trị tài khoản và danh mục. Khi đổi tài khoản, chọn **Authorize** → **Logout** rồi dán token mới.


---

## 1. Dữ liệu mẫu dùng chung

Tạo dữ liệu một lần bằng tài khoản có quyền tương ứng. Dùng Admin để quản lý danh mục và tài khoản được phân công để thực hiện nghiệp vụ kho.

1. **Sản phẩm** – `POST /api/products`:
   ```json
   { "sku": "SP-001", "name": "Sữa hộp", "category": "Thực phẩm", "unitOfMeasure": "Hộp", "salePrice": 12000 }
   ```
   → ghi `productId`.
2. **Kho** – `POST /api/warehouses`: `{ "code": "HN-01", "name": "Kho Hà Nội" }` → `warehouseId`.
   Tạo thêm kho thứ hai `{ "code": "HCM-01", "name": "Kho HCM" }` → `warehouse2Id` (dùng cho chuyển kho).
3. **Vị trí** – `POST /api/warehouses/{warehouseId}/locations`, lần lượt:
   ```json
   { "type": "Zone",  "parentId": null,      "segmentCode": "A",  "name": "Khu A" }
   { "type": "Rack",  "parentId": {zoneId},  "segmentCode": "01", "name": "Kệ 01" }
   { "type": "Shelf", "parentId": {rackId},  "segmentCode": "02", "name": "Tầng 02" }
   { "type": "Bin",   "parentId": {shelfId}, "segmentCode": "03", "name": "Ô 03" }
   { "type": "Bin",   "parentId": {shelfId}, "segmentCode": "04", "name": "Ô 04" }
   ```
   → `binId` (mã `A-01-02-03`) và `bin2Id` (`A-01-02-04`). Làm tương tự cho kho HCM-01 để có `hcmBinId`.
4. **Nhà cung cấp** – `POST /api/suppliers`: `{ "code": "NCC01", "name": "Công ty ABC" }` → `supplierId`.
5. **Có hàng trong kho**: làm phần 6 (phiếu nhập) với 100 sản phẩm vào `binId`.


---

## 2. W-02 Kho

| # | Thao tác | Kỳ vọng |
|---|---|---|
| 1 | `POST /api/warehouses` `{ "code": "hn-02", "name": "Kho HN 2", "phone": "0901234567" }` | 201, mã tự viết hoa `HN-02` |
| 2 | Tạo lại cùng mã `HN-02` | **409** trùng mã |
| 3 | `GET /api/warehouses?keyword=HN&isActive=true&page=1&pageSize=10&sortBy=code&sortDir=desc` | danh sách có phân trang (`items`, `totalCount`, `totalPages`) |
| 4 | `PUT /api/warehouses/{id}` đổi `name` | 200 |
| 5 | `PATCH /api/warehouses/{id}/deactivate` rồi `/activate` | `isActive` false → true |
| 6 | `DELETE` kho **chưa có** vị trí/tồn/phiếu | 204 |
| 7 | `DELETE` kho HN-01 (đã có vị trí/tồn) | **409** còn dữ liệu phụ thuộc |
| 8 | Token `{ "userId": 2, "permissions": ["WAREHOUSE_VIEW"] }` gọi `POST` | **403** |
| 9 | Không Authorize, gọi `GET` | **401** |

## 3. W-03 Vị trí kho

Cấu trúc bắt buộc: **Zone → Rack → Shelf → Bin**. Mã đầy đủ ghép từ các `segmentCode`.

| # | Thao tác | Kỳ vọng |
|---|---|---|
| 1 | Tạo 4 cấp như phần 1 | mã Bin là `A-01-02-03` |
| 2 | `GET /api/warehouses/{warehouseId}/locations/tree` | cây lồng nhau Zone/Rack/Shelf/Bin |
| 3 | `GET .../locations?type=Bin&keyword=A-01` | chỉ Bin |
| 4 | Tạo `Bin` có `parentId` là Zone | **422** sai cấp |
| 5 | Tạo `Zone` có `parentId` khác null | **422** |
| 6 | Tạo trùng `segmentCode` dưới cùng cha | **409** |
| 7 | `GET /api/warehouses/{warehouse2Id}/locations/{binId}` (bin của kho khác) | **404** |
| 8 | `PATCH .../{shelfId}/deactivate` khi còn Bin con đang hoạt động | **422** "active child location(s)" |
| 8b | Tạo Bin trống mới → `PATCH .../{binMoiId}/deactivate` → nhập hàng vào bin đó | deactivate 200; phiếu nhập **422** |
| 9 | `DELETE` Bin đang có tồn | **409** |

## 4. W-04/W-05 Tồn kho

`GET /api/inventories` (quyền `INVENTORY_VIEW`). Tham số lọc chung (W-05) dùng cho **mọi** API danh sách: `page`, `pageSize` (tối đa 100), `keyword`, `sortBy`, `sortDir=asc|desc`.

| # | Thao tác | Kỳ vọng |
|---|---|---|
| 1 | `GET /api/inventories?warehouseId={warehouseId}` | các dòng tồn: `quantity`, `reservedQuantity`, `availableQuantity` = quantity − reserved |
| 2 | `?productId=1&locationId={binId}` | đúng 1 dòng |
| 3 | `?stockStatus=LowStock` / `OutOfStock` / `InStock` | lọc theo trạng thái tồn |
| 4 | `?hasStock=true&sortBy=quantity&sortDir=desc` | chỉ dòng còn hàng, giảm dần |
| 5 | `?keyword=SP-001` | tìm theo SKU/tên sản phẩm |
| 6 | `?sortBy=abc` | **400** cột sắp xếp không hợp lệ |
| 7 | `?pageSize=500` | **400** (tối đa 100) |
| 8 | `GET /api/inventories/{id}` | chi tiết 1 dòng |

Không có API sửa số lượng trực tiếp: tồn chỉ thay đổi qua phiếu (nhập/xuất/chuyển/kiểm kê/điều chỉnh).

## 5. W-06 Sổ biến động kho

| # | Thao tác | Kỳ vọng |
|---|---|---|
| 1 | Sau khi hoàn tất 1 phiếu nhập: `GET /api/stock-movements?productId=1` | dòng `Receipt` có `quantityBefore`, `quantityAfter`, `referenceType=GOODS_RECEIPT`, `referenceNo=GR-...` |
| 2 | `?movementType=Issue&from=2026-11-01T00:00:00Z&to=2026-12-31T00:00:00Z` | lọc theo loại + thời gian |
| 3 | `?referenceType=GOODS_ISSUE&referenceId={issueId}` | các biến động của 1 phiếu |
| 4 | `GET /api/stock-movements/report?warehouseId={warehouseId}&from=2026-11-01T00:00:00Z&to=2026-12-01T00:00:00Z` | báo cáo **Nhập – Xuất – Tồn**: tồn đầu, nhập, xuất, tồn cuối theo sản phẩm |
| 5 | Duyệt phiếu xuất có số lượng lớn hơn tồn khả dụng | **422** và tồn kho giữ nguyên |

Biến động **không sửa/xóa được** (chặn ở EF + trigger DB) — không có API PUT/DELETE.

## 6. W-07 Phiếu nhập

Luồng: **Draft → submit → PendingApproval → approve → Approved → complete → Completed** (tồn tăng ở bước complete).

1. (Nhân viên) `POST /api/goods-receipts`:
   ```json
   {
     "warehouseId": 1,
     "supplierId": 1,
     "referenceNo": "HD-0001",
     "receiptDate": "2026-11-24",
     "lines": [
       { "productId": 1, "locationId": 4, "quantity": 100, "unitCost": 9000,
         "batchCode": "LO-001", "manufactureDate": "2026-10-01", "expiryDate": "2027-03-01" }
     ]
   }
   ```
   → 201, `documentNo` dạng `GR-2026-000001`, `status: Draft`, `supplierName` lấy từ danh mục NCC.
2. `POST /api/goods-receipts/{id}/submit` → `PendingApproval`.
3. (Quản lý) `POST .../{id}/approve` → `Approved`. (Nhân viên tự approve → **422**.)
4. (Quản lý) `POST .../{id}/complete` → `Completed`; kiểm tra `GET /api/inventories` tăng 100, `GET /api/stock-movements` có dòng `Receipt`.

Trường hợp lỗi / nhánh khác:

| # | Thao tác | Kỳ vọng |
|---|---|---|
| a | `PUT /api/goods-receipts/{id}` khi Draft | sửa được, các dòng thay toàn bộ |
| b | `PUT` khi đã submit | **422** |
| c | `POST .../{id}/reject` `{ "reason": "Sai số lượng" }` → `POST .../{id}/reopen` → sửa → submit lại | Rejected → Draft |
| d | `POST .../{id}/cancel` `{ "reason": "Hủy đơn" }` | Cancelled (chỉ trước khi complete) |
| e | `complete` lần 2 | **422**, tồn không đổi |
| f | `locationId` là Shelf (không phải Bin) hoặc bin của kho khác | **422** kèm số dòng lỗi |
| g | `expiryDate` ≤ ngày nhập | **422** hàng đã hết hạn |
| h | 2 dòng trùng sản phẩm + vị trí + lô | **422** |
| i | `GET /api/goods-receipts?status=Completed&supplierId=1&fromDate=2026-11-01` | lọc danh sách |

**Nhà cung cấp** (tích hợp module của Người 1):

| # | Thao tác | Kỳ vọng |
|---|---|---|
| j | Tạo phiếu với `"supplierId": 1, "supplierName": "gõ gì cũng được"` | `supplierName` = tên trong danh mục ("Công ty ABC") |
| k | `"supplierId": 9999` | **422** "Supplier 9999 does not exist" |
| l | `DELETE /api/suppliers/1` (ngừng NCC) → `submit` phiếu nháp của NCC đó | **422** "is inactive" |
| m | Phiếu đã **Approved** trước khi ngừng NCC → `complete` | vẫn hoàn tất được |
| n | Không gửi `supplierId`, chỉ `"supplierName": "Khách lẻ"` | tạo được (NCC vãng lai) |

## 7. W-08 Phiếu xuất

Luồng như phiếu nhập. **Approve giữ hàng** (`reservedQuantity` tăng, `availableQuantity` giảm); **complete trừ tồn**.

1. (Nhân viên) `POST /api/goods-issues`:
   ```json
   { "warehouseId": 1, "customerName": "Cửa hàng X", "referenceNo": "DH-01",
     "lines": [ { "productId": 1, "locationId": 4, "quantity": 30 } ] }
   ```
2. `submit` → (Quản lý) `approve` → xem `GET /api/inventories`: `reservedQuantity = 30`, `availableQuantity = 70`.
3. `complete` → `quantity = 70`, `reservedQuantity = 0`; movement `Issue`.

| # | Thao tác | Kỳ vọng |
|---|---|---|
| a | Xuất nhiều hơn `availableQuantity` | **422** không đủ hàng khả dụng (ở submit/approve) |
| b | Hai phiếu cùng xuất 60 trên tồn 100: approve phiếu 1 rồi phiếu 2 | phiếu 2 **422** (hàng đã được giữ) |
| c | `cancel` phiếu đã Approved | hàng giữ được nhả lại (`reservedQuantity` giảm) |
| d | Dòng có `"batchId": {id}` | xuất đúng lô đó; không ghi `batchId` → tự chọn lô hết hạn sớm nhất (FEFO), bỏ qua lô đã hết hạn |
| e | Dòng chỉ định lô đã hết hạn | **422** |

## 8. W-09 Chuyển kho

Luồng: **Draft → submit → approve (giữ hàng) → pick → ship (trừ kho nguồn, TRANSFER_OUT) → receive (cộng kho đích, TRANSFER_IN) → complete**.

1. (Nhân viên) `POST /api/stock-transfers`:
   ```json
   { "warehouseId": 1, "destinationWarehouseId": 2, "reason": "Bổ sung hàng HCM",
     "lines": [ { "productId": 1, "fromLocationId": 4, "toLocationId": {hcmBinId}, "quantity": 10 } ] }
   ```
   Chuyển nội bộ (đổi vị trí cùng kho): `destinationWarehouseId` = `warehouseId`, `toLocationId` = `bin2Id`.
2. `submit` → (Quản lý) `approve` → (Nhân viên) `pick` → `ship` → kiểm tra tồn kho nguồn giảm 10.
3. `receive` → tồn kho đích tăng 10 → (Quản lý) `complete`.

| # | Thao tác | Kỳ vọng |
|---|---|---|
| a | `fromLocationId` = `toLocationId` | **400** |
| b | `toLocationId` không thuộc kho đích | **422** |
| c | `cancel` sau khi đã `ship` | **422** (hàng đang trên đường) |
| d | `cancel` khi Approved/Picked | nhả hàng giữ |
| e | `GET /api/stock-movements?referenceType=STOCK_TRANSFER&referenceId={id}` | cặp `TransferOut` / `TransferIn` |
| f | `GET /api/stock-transfers?isInternal=true` | chỉ phiếu chuyển nội bộ |

## 9. W-10 Kiểm kê

Luồng: **Draft → snapshot (InProgress, chụp tồn hệ thống, khóa vị trí) → counts → submit → approve → adjust (tạo biến động chênh lệch) → complete**.

1. (Nhân viên) `POST /api/stocktakes` `{ "warehouseId": 1, "scopeLocationIds": [4], "note": "Kiểm kê tháng 11" }`
   (`scopeLocationIds` rỗng = cả kho; truyền Shelf/Zone = mọi Bin con).
2. `POST /api/stocktakes/{id}/snapshot` → `InProgress`; mỗi dòng có `systemQuantity`.
3. Thử nhập/xuất vào bin 4 trong lúc này → **422** vị trí đang kiểm kê (bị khóa).
4. `PUT /api/stocktakes/{id}/counts`:
   ```json
   { "items": [ { "productId": 1, "locationId": 4, "countedQuantity": 68, "note": "Thiếu 2 hộp" } ] }
   ```
5. `GET /api/stocktakes/{id}/differences` → `difference = -2`.
6. `submit` (còn dòng chưa đếm → **422**) → (Quản lý) `approve` → `adjust` → tồn = 68, movement `StocktakeOut` → `complete` → hết khóa.

| # | Thao tác | Kỳ vọng |
|---|---|---|
| a | `reject` `{ "reason": "Đếm lại" }` | quay về `InProgress` để đếm lại |
| b | Tạo phiếu kiểm kê thứ 2 trùng vị trí đang kiểm kê → `snapshot` | **422** "already being counted by stocktake ST-..." |
| c | Đếm sản phẩm không có trong snapshot | thêm dòng mới (`isAddedDuringCount: true`) |

## 10. W-11 Điều chỉnh tồn

Luồng: **Draft → submit → approve (áp dụng ngay và Completed)**. Bắt buộc lý do.
`reasonCode`: `Damaged`, `Lost`, `Found`, `Expired`, `DataCorrection`, `Other`.

1. (Nhân viên) `POST /api/stock-adjustments`:
   ```json
   { "warehouseId": 1, "reasonCode": "Damaged", "reason": "Hộp bị móp khi bốc xếp",
     "lines": [ { "productId": 1, "locationId": 4, "adjustmentQuantity": -3 } ] }
   ```
   (`adjustmentQuantity` âm = giảm, dương = tăng.)
2. `submit` → (Quản lý) `approve` → `Completed`; dòng có `beforeQuantity` / `afterQuantity`; movement `AdjustmentOut`.

| # | Thao tác | Kỳ vọng |
|---|---|---|
| a | `reason` ngắn hơn 5 ký tự | **400** |
| b | `adjustmentQuantity: 0` | **400** |
| c | Giảm nhiều hơn tồn khả dụng | **422** (tồn không âm) |
| d | Người tạo tự approve | **422** |
| e | `GET /api/stock-adjustments?reasonCode=Damaged&status=Completed` | lọc |

## 11. W-12 Lô / hạn dùng

| # | Thao tác | Kỳ vọng |
|---|---|---|
| 1 | Nhập phiếu có `batchCode` (phần 6) | lô tự tạo; `GET /api/batches?productId=1` thấy lô + tồn theo lô |
| 2 | `POST /api/batches` `{ "productId": 1, "batchCode": "LO-002", "manufactureDate": "2026-09-01", "expiryDate": "2026-12-10" }` | 201 |
| 3 | Nhập lại `LO-001` nhưng khác `expiryDate` | **422** lô đã tồn tại với ngày khác |
| 4 | `GET /api/batches?expiryStatus=NearExpiry` / `Expired` / `Valid` / `NoExpiry` | lọc theo hạn |
| 5 | `GET /api/batches/expiry-alerts?warehouseId=1&days=30` | danh sách lô sắp/đã hết hạn còn tồn |
| 6 | `GET /api/batches/pick-suggestion?productId=1&warehouseId=1&quantity=50&strategy=Fefo` | gợi ý lấy hàng theo lô hết hạn sớm nhất; `strategy=Fifo` = lô cũ nhất |
| 7 | `...&includeExpired=true` | có cả lô đã hết hạn |
| 8 | `PUT /api/batches/{id}` đổi `expiryDate` | 200 |
| 9 | `DELETE` lô còn tồn hoặc đã dùng trong phiếu | **409** |

---

## 12. Mã lỗi thường gặp

| HTTP | Ý nghĩa |
|---|---|
| 400 | Dữ liệu gửi lên sai định dạng / thiếu trường (xem `errors`) |
| 401 | Chưa Authorize hoặc token hết hạn (60 phút) |
| 403 | Token thiếu quyền cần thiết |
| 404 | Không tìm thấy (hoặc vị trí không thuộc kho trong URL) |
| 409 | Trùng mã / còn dữ liệu phụ thuộc nên không xóa được / dữ liệu vừa bị người khác sửa |
| 422 | Vi phạm quy tắc nghiệp vụ (sai trạng thái, không đủ hàng, tự duyệt, vị trí đang kiểm kê...) |

> Lưu ý: API `/api/products` và `/api/suppliers` (module Người 1) hiện **chưa yêu cầu token**.
