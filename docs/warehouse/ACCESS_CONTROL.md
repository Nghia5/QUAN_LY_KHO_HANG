# Phân quyền vận hành kho

## Khởi tạo

1. Chạy `dotnet ef database update --project src/WMS.Api` trên cơ sở dữ liệu đích.
2. Cấu hình secrets `BootstrapAdmin:Username`, `BootstrapAdmin:Email` và `BootstrapAdmin:Password`. Có thể truyền từ môi trường bằng `BootstrapAdmin__Username`, `BootstrapAdmin__Email` và `BootstrapAdmin__Password`.
3. Chạy `dotnet run --project src/WMS.Api --no-launch-profile -- --initialize-user-platform`. Lệnh này đăng ký các mã quyền, tạo các vai trò mặc định còn thiếu và tạo Admin đầu tiên nếu chưa có tài khoản nào được gán vai trò `ADMIN`. Chạy lại sau khi cập nhật để bổ sung mã quyền mới vào các vai trò mặc định tương ứng. Các quyền đã tồn tại giữ nguyên cấu hình cấp phát.
4. Xóa thông tin khởi tạo khỏi môi trường. Để chạy API, cấu hình JWT bằng `Jwt:Key` (ít nhất 32 byte), `Jwt:Issuer` và `Jwt:Audience`.
5. Đăng nhập với Admin. Dùng `PUT /api/users/{userId}/warehouse-roles/{warehouseId}/{roleId}` để gán vai trò tại từng kho. Các vai trò toàn hệ thống không cấp quyền tác nghiệp kho.

## Vai trò mặc định

| Vai trò | Phạm vi và trách nhiệm |
|---|---|
| `ADMIN` | Quản trị tài khoản, vai trò, phân công theo kho và danh mục lô dùng chung. |
| `WAREHOUSE_MANAGER` | Xem và vận hành kho được giao, duyệt hoặc từ chối phiếu, ghi nhận thay đổi tồn. |
| `WAREHOUSE_STAFF` | Lập và gửi phiếu, soạn, giao, nhận và kiểm đếm tại kho được giao. |
| `VIEWER` | Xem chứng từ, tồn và lịch sử biến động tại kho được giao. |

## Quyền theo bước nghiệp vụ

- Phiếu nhập và xuất có quyền `*_COMPLETE` riêng cho bước ghi nhận tồn kho.
- Chuyển kho dùng `STOCK_TRANSFER_PICK` và `STOCK_TRANSFER_SHIP` tại kho nguồn. `STOCK_TRANSFER_RECEIVE` và `STOCK_TRANSFER_COMPLETE` kiểm tra kho đích.
- Kiểm kê có quyền `STOCKTAKE_COUNT`, `STOCKTAKE_ADJUST` và `STOCKTAKE_COMPLETE` riêng.
- Người lập xử lý các thao tác trên phiếu nháp của mình. Luồng duyệt tiếp tục kiểm tra quy tắc tự duyệt trong cấu hình kho.

## Danh mục và tệp chứng từ

- `PRODUCT_VIEW` và `SUPPLIER_VIEW` cho phép tra cứu danh mục dùng chung. Có thể cấp quyền đọc qua một vai trò tại kho hoặc một vai trò toàn hệ thống. `PRODUCT_MANAGE` và `SUPPLIER_MANAGE` chỉ cấp qua vai trò toàn hệ thống. Admin có quyền quản lý cả hai danh mục.
- Tải tệp bằng `POST /api/files/document` với form-data `File`, `DocumentType` và `DocumentId`. Các loại phiếu hỗ trợ là `Receipt`, `Issue`, `Transfer`, `Stocktake` và `Adjustment`.
- Xem danh sách tệp bằng `GET /api/files/document/{documentType}/{documentId}`. Danh sách và tải xuống kế thừa quyền xem phiếu theo kho. Tải lên hoặc xóa cần quyền thao tác phiếu. Chỉ người tải lên được xóa tệp của mình và phiếu đã hoàn tất hoặc hủy giữ nguyên tệp đính kèm.
- Avatar và tệp cũ chưa gắn phiếu vẫn chỉ truy cập được từ tài khoản đã tải lên.
