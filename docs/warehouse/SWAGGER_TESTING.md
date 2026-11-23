# Test API module Kho bằng Swagger

## Cài đặt một lần trên mỗi máy

Chạy trong `src/WMS.Api` (PowerShell):

```powershell
# 1. Chuỗi kết nối SQL Server của máy bạn (không commit, lưu trong user-secrets)
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=localhost\MSSQLSERVER01;Database=WMS;Trusted_Connection=True;TrustServerCertificate=True"

# 2. Tạo khóa ký JWT cho môi trường dev (tạo appsettings.Development.json - đã gitignore)
dotnet user-jwts create

# 3. Tạo/cập nhật database
dotnet ef database update
```

> Nếu `dotnet user-jwts create` báo thiếu `appsettings.Development.json`, tạo file đó với nội dung `{}` rồi chạy lại.

## Mỗi lần test

1. Chạy API: `dotnet run --launch-profile http` (trong `src/WMS.Api`).
2. Mở **http://localhost:5237/swagger**.
3. Lấy token: mở `POST /api/dev/token` → **Try it out** → body:
   - Toàn quyền: `{}`
   - Chỉ một số quyền (để thử lỗi 403): `{ "userId": 2, "permissions": ["INVENTORY_VIEW"] }`
   → **Execute** → copy giá trị `token`.
4. Bấm nút **Authorize** (góc trên phải) → dán token (không gõ chữ `Bearer`) → **Authorize** → **Close**.
5. Gọi các API bình thường bằng **Try it out → Execute**.

`/api/dev/token` chỉ hoạt động khi `ASPNETCORE_ENVIRONMENT=Development`; ở môi trường khác trả 404.
Khi module User/Login của Người 3 hoàn thành, dùng token đăng nhập thật thay cho endpoint này.

## Mã lỗi thường gặp

| HTTP | Ý nghĩa |
|---|---|
| 400 | Dữ liệu gửi lên sai định dạng (xem `errors`) |
| 401 | Chưa Authorize hoặc token hết hạn (8 giờ) |
| 403 | Token thiếu quyền cần thiết |
| 404 | Không tìm thấy (hoặc vị trí không thuộc kho trong URL) |
| 409 | Trùng mã / còn dữ liệu phụ thuộc nên không xóa được |
| 422 | Vi phạm quy tắc nghiệp vụ (ví dụ: Bin đặt dưới Zone) |
