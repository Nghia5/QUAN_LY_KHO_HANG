# Common Query contract (đề xuất từ module Kho)

Module Kho đang dùng `WMS.Api.Dtos.Common.PagedRequest` / `PagedResult<T>`. Người 3 có thể dựa vào đây để
làm Common Query DTO chung; các module khác chỉ cần kế thừa `PagedRequest`.

## Request (query string)

| Tham số | Kiểu | Mặc định | Quy tắc |
|---|---|---|---|
| `page` | int | 1 | `>= 1` |
| `pageSize` | int | 20 | `1..100` |
| `keyword` | string? | — | tối đa 200 ký tự, tìm `LIKE %keyword%` trên các cột do từng module quy định |
| `sortBy` | string? | tùy module | **phải thuộc whitelist** của module (không phân biệt hoa thường), sai ⇒ 400 |
| `sortDir` | `asc` \| `desc` | `asc` | sai ⇒ 400 |

Filter riêng của từng module là các property thêm vào record kế thừa, ví dụ `InventoryQuery`:
`warehouseId`, `locationId`, `productId`, `category`, `stockStatus` (`InStock|LowStock|OutOfStock`), `hasStock`.

## Response

```json
{ "items": [ ... ], "page": 2, "pageSize": 20, "totalCount": 135, "totalPages": 7 }
```

## Cách triển khai trong module

```csharp
public sealed record InventoryQuery : PagedRequest
{
    public static readonly string[] SortableFields = ["productSku", "quantity", ...];
    public int? WarehouseId { get; init; }
}

// Validator
this.ApplyPagingRules(InventoryQuery.SortableFields);

// Repository: filter -> ApplySort(whitelist, default, tie-breaker) -> Select(projection) -> ToPagedResultAsync
```

`ToPagedResultAsync` chạy đúng 2 câu SQL: `COUNT(*)` và `OFFSET/FETCH` — không tải toàn bộ bảng.

## Chạy test truy vấn

Test chạy trên SQL Server thật (tạo DB `WMS_Test_<guid>` rồi tự xóa):

```powershell
dotnet test                                   # mặc định dùng (localdb)\MSSQLLocalDB
$env:WMS_TEST_SQLSERVER = 'Server=localhost\MSSQLSERVER01;Trusted_Connection=True;TrustServerCertificate=True'
dotnet test                                   # dùng instance khác
```
