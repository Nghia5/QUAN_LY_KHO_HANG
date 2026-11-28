using Microsoft.EntityFrameworkCore;
using WMS.Api.Models;
using WMS.Api.Services.Auth;

namespace WMS.Api.Data;

/// <summary>
/// Development-only sample data, run with <c>dotnet run -- --seed-sample-data</c>
/// (after <c>--initialize-user-platform</c> so that roles exist).
/// Creates 30 warehouses (each with a Zone &gt; Rack &gt; Shelf &gt; Bin tree), 1000 products and
/// 500 users, linked together:
/// <list type="bullet">
/// <item>each warehouse has a manager user (Warehouses.ManagerId + WAREHOUSE_MANAGER assignment);</item>
/// <item>every other user is a WAREHOUSE_STAFF or VIEWER assigned to 1–3 warehouses;</item>
/// <item>every product is stocked in 3–8 warehouses at random bins, each opening stock
///       written as an Inventory row plus a Receipt stock movement (ReferenceType OPENING_BALANCE).</item>
/// </list>
/// Also 50 suppliers, each the main supplier (Products.SupplierId) of ~20 products of its category.
/// Idempotent: each part is skipped when its data already exists. Deterministic (fixed random seed).
/// </summary>
public static class SampleDataSeeder
{
    public const string ReferenceType = "OPENING_BALANCE";
    public const string DefaultPassword = "123456";

    private const int WarehouseCount = 30;
    private const int ProductCount = 1000;
    private const int UserCount = 500;

    private static readonly string[] Cities =
    [
        "Hà Nội", "Hồ Chí Minh", "Đà Nẵng", "Hải Phòng", "Cần Thơ", "Bình Dương", "Đồng Nai", "Bắc Ninh",
        "Quảng Ninh", "Nghệ An", "Thanh Hóa", "Khánh Hòa", "Lâm Đồng", "Bà Rịa - Vũng Tàu", "Long An",
        "Thừa Thiên Huế", "Bình Định", "Đắk Lắk", "Hưng Yên", "Vĩnh Phúc", "Thái Nguyên", "Nam Định",
        "Quảng Nam", "An Giang", "Kiên Giang", "Tây Ninh", "Bắc Giang", "Hải Dương", "Phú Thọ", "Ninh Bình"
    ];

    private static readonly (string Category, string Unit, string[] Names, decimal MinPrice, decimal MaxPrice)[] Catalog =
    [
        ("Điện tử", "Cái", ["Tai nghe Bluetooth", "Loa di động", "Sạc dự phòng", "Chuột không dây", "Bàn phím cơ", "Cáp USB-C", "Webcam HD", "Ổ cứng SSD"], 90_000, 3_500_000),
        ("Gia dụng", "Cái", ["Nồi cơm điện", "Ấm siêu tốc", "Quạt đứng", "Máy xay sinh tố", "Bàn ủi hơi nước", "Nồi chiên không dầu", "Máy hút bụi"], 250_000, 4_000_000),
        ("Thực phẩm", "Thùng", ["Mì gói", "Nước suối", "Sữa tươi", "Cà phê hòa tan", "Bánh quy", "Nước ngọt", "Gạo thơm"], 60_000, 650_000),
        ("Thời trang", "Cái", ["Áo thun", "Áo sơ mi", "Quần jean", "Áo khoác", "Váy công sở", "Giày thể thao", "Mũ lưỡi trai"], 80_000, 1_200_000),
        ("Văn phòng phẩm", "Hộp", ["Bút bi", "Giấy A4", "Bìa hồ sơ", "Kẹp giấy", "Sổ tay", "Bút dạ quang", "Băng keo"], 15_000, 180_000),
        ("Mỹ phẩm", "Chai", ["Sữa rửa mặt", "Kem chống nắng", "Dầu gội", "Sữa tắm", "Nước tẩy trang", "Kem dưỡng ẩm"], 70_000, 600_000),
        ("Đồ chơi", "Bộ", ["Xếp hình", "Xe điều khiển", "Búp bê", "Bộ đồ chơi nấu ăn", "Rubik", "Cờ vua"], 50_000, 900_000),
        ("Vật liệu xây dựng", "Bao", ["Xi măng", "Cát xây", "Keo dán gạch", "Bột trét tường", "Sơn nước"], 70_000, 1_500_000),
        ("Phụ tùng ô tô", "Cái", ["Lọc gió", "Má phanh", "Bugi", "Gạt mưa", "Dầu nhớt", "Ắc quy"], 60_000, 2_500_000),
        ("Dụng cụ", "Bộ", ["Máy khoan", "Bộ tua vít", "Cờ lê", "Thước cuộn", "Kìm đa năng", "Máy mài"], 40_000, 2_200_000),
    ];

    private static readonly string[] Variants = ["Loại A", "Loại B", "Cao cấp", "Tiêu chuẩn", "Mini", "Pro", "Plus", "Eco"];
    private static readonly string[] Brands = ["Sao Mai", "Hòa Phát", "Việt Tiến", "Thiên Long", "Phú Quý", "An Khang", "Minh Long", "Đại Việt"];
    private static readonly string[] LastNames = ["Nguyễn", "Trần", "Lê", "Phạm", "Hoàng", "Huỳnh", "Phan", "Vũ", "Võ", "Đặng", "Bùi", "Đỗ", "Hồ", "Ngô", "Dương", "Lý"];
    private static readonly string[] MiddleNames = ["Văn", "Thị", "Minh", "Đức", "Thanh", "Ngọc", "Hữu", "Quốc", "Gia", "Thu", "Hoài", "Anh"];
    private static readonly string[] FirstNames = ["An", "Bình", "Châu", "Dũng", "Giang", "Hà", "Hải", "Hùng", "Khánh", "Linh", "Long", "Mai", "Nam", "Nga", "Phong", "Phúc", "Quân", "Sơn", "Tâm", "Thảo", "Trang", "Tuấn", "Vy", "Yến"];

    public static async Task SeedAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<WmsDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        var log = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(SampleDataSeeder));

        if (await db.Set<Warehouse>().AnyAsync(w => w.Code == WarehouseCode(1), cancellationToken))
        {
            log.LogWarning("Sample warehouses/users/products already present (warehouse {Code} exists); skipping them.", WarehouseCode(1));
        }
        else
        {
            await SeedCoreAsync(db, hasher, log, cancellationToken);
        }

        await SeedSuppliersAsync(db, log, cancellationToken);
    }

    /// <summary>
    /// 50 suppliers (NCC-001..NCC-050), 5 per product category. The sample products of each category
    /// are spread round-robin over that category's 5 suppliers (~20 products per supplier).
    /// Idempotent: skipped when NCC-001 exists.
    /// </summary>
    private static async Task SeedSuppliersAsync(WmsDbContext db, ILogger log, CancellationToken cancellationToken)
    {
        if (await db.Set<SupplierModel>().AnyAsync(s => s.Code == SupplierCode(1), cancellationToken))
        {
            log.LogWarning("Sample suppliers already present ({Code} exists); skipping them.", SupplierCode(1));
            return;
        }

        string[] companyTypes = ["Công ty TNHH", "Công ty Cổ phần", "Công ty TNHH MTV", "Tập đoàn", "Doanh nghiệp tư nhân"];
        var random = new Random(20261128);
        var now = DateTime.UtcNow;
        db.ChangeTracker.AutoDetectChangesEnabled = true; // the core seeding turns it off; product edits below need it
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var suppliersByCategory = new Dictionary<string, List<SupplierModel>>();
        var index = 0;
        foreach (var (category, _, _, _, _) in Catalog)
        {
            var list = new List<SupplierModel>();
            for (var k = 0; k < 5; k++)
            {
                index++;
                var city = Cities[(index - 1) % Cities.Length];
                list.Add(new SupplierModel
                {
                    Code = SupplierCode(index),
                    Name = $"{companyTypes[k]} {category} {Brands[(index - 1) % Brands.Length]} {city}",
                    Phone = $"028{random.Next(1_000_000, 9_999_999)}",
                    Email = $"ncc{index:D3}@supplier.local",
                    Address = $"Số {random.Next(1, 300)} đường {random.Next(1, 50)}, {city}",
                    IsActive = true,
                    CreatedAt = now
                });
            }

            suppliersByCategory[category] = list;
            db.Set<SupplierModel>().AddRange(list);
        }

        await db.SaveChangesAsync(cancellationToken);

        var products = await db.Products
            .Where(p => p.Sku.StartsWith("PRD-") && p.SupplierId == null)
            .OrderBy(p => p.Id)
            .ToListAsync(cancellationToken);
        var counters = new Dictionary<string, int>();
        foreach (var product in products)
        {
            if (!suppliersByCategory.TryGetValue(product.Category, out var list))
            {
                continue;
            }

            var n = counters.GetValueOrDefault(product.Category);
            product.SupplierId = list[n % list.Count].Id;
            counters[product.Category] = n + 1;
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        log.LogInformation("Seeded {Suppliers} suppliers and linked {Products} products to them.",
            index, counters.Values.Sum());
    }

    private static string SupplierCode(int index) => $"NCC-{index:D3}";

    private static async Task SeedCoreAsync(WmsDbContext db, IPasswordHasher hasher, ILogger log, CancellationToken cancellationToken)
    {

        var roles = await db.Roles.ToDictionaryAsync(r => r.Code, cancellationToken);
        foreach (var code in new[] { "WAREHOUSE_MANAGER", "WAREHOUSE_STAFF", "VIEWER" })
        {
            if (!roles.ContainsKey(code))
            {
                throw new InvalidOperationException($"Role {code} missing. Run with --initialize-user-platform first.");
            }
        }

        var random = new Random(20261127);
        var now = DateTime.UtcNow;
        db.ChangeTracker.AutoDetectChangesEnabled = false;
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        // ---- Users -------------------------------------------------------------------------
        // One PBKDF2 hash shared by all sample accounts (100k iterations × 500 would be slow).
        var passwordHash = hasher.HashPassword(DefaultPassword);
        var users = new List<User>(UserCount);
        for (var i = 1; i <= UserCount; i++)
        {
            var isManager = i <= WarehouseCount;
            var username = isManager ? $"manager{i:D2}" : $"user{i:D3}";
            users.Add(new User
            {
                Username = username,
                Email = $"{username}@wms.local",
                PasswordHash = passwordHash,
                FullName = $"{Pick(random, LastNames)} {Pick(random, MiddleNames)} {Pick(random, FirstNames)}",
                PhoneNumber = $"09{random.Next(10_000_000, 99_999_999)}",
                IsActive = true,
                CreatedAt = now
            });
        }

        db.Users.AddRange(users);
        await db.SaveChangesAsync(cancellationToken);

        // ---- Warehouses (manager = managerNN) ------------------------------------------------
        var warehouses = new List<Warehouse>(WarehouseCount);
        for (var i = 1; i <= WarehouseCount; i++)
        {
            var city = Cities[i - 1];
            warehouses.Add(new Warehouse
            {
                Code = WarehouseCode(i),
                Name = $"Kho {city}",
                Address = $"Số {random.Next(1, 500)}, KCN {city}",
                Phone = $"02{random.Next(10_000_000, 99_999_999)}",
                Description = $"Kho mẫu số {i} tại {city}",
                ManagerId = users[i - 1].Id,
                IsActive = true,
                CreatedAt = now
            });
        }

        db.Set<Warehouse>().AddRange(warehouses);
        await db.SaveChangesAsync(cancellationToken);

        // ---- Locations: 2 zones × 2 racks × 2 shelves × 3 bins = 24 bins per warehouse ----------
        var binsByWarehouse = new Dictionary<int, List<WarehouseLocation>>();
        foreach (var warehouse in warehouses)
        {
            var bins = new List<WarehouseLocation>();
            foreach (var zoneCode in new[] { "A", "B" })
            {
                var zone = Location(warehouse.Id, null, LocationType.Zone, zoneCode, zoneCode, $"Khu {zoneCode}", now);
                for (var r = 1; r <= 2; r++)
                {
                    var rack = Location(warehouse.Id, zone, LocationType.Rack, $"{r:D2}", $"{zone.Code}-{r:D2}", $"Kệ {r:D2}", now);
                    zone.Children.Add(rack);
                    for (var s = 1; s <= 2; s++)
                    {
                        var shelf = Location(warehouse.Id, rack, LocationType.Shelf, $"{s:D2}", $"{rack.Code}-{s:D2}", $"Tầng {s:D2}", now);
                        rack.Children.Add(shelf);
                        for (var b = 1; b <= 3; b++)
                        {
                            var bin = Location(warehouse.Id, shelf, LocationType.Bin, $"{b:D2}", $"{shelf.Code}-{b:D2}", $"Ô {b:D2}", now);
                            shelf.Children.Add(bin);
                            bins.Add(bin);
                        }
                    }
                }

                db.Set<WarehouseLocation>().Add(zone);
            }

            binsByWarehouse[warehouse.Id] = bins;
        }

        db.ChangeTracker.DetectChanges();
        await db.SaveChangesAsync(cancellationToken);

        // ---- Role assignments ------------------------------------------------------------------
        var manager = roles["WAREHOUSE_MANAGER"];
        var staff = roles["WAREHOUSE_STAFF"];
        var viewer = roles["VIEWER"];
        for (var i = 0; i < users.Count; i++)
        {
            var user = users[i];
            Role role;
            int[] warehouseIds;
            if (i < WarehouseCount)
            {
                role = manager;
                warehouseIds = [warehouses[i].Id];
            }
            else
            {
                role = i < 430 ? staff : viewer; // 400 staff, 70 viewers
                warehouseIds = warehouses.OrderBy(_ => random.Next()).Take(random.Next(1, 4)).Select(w => w.Id).ToArray();
            }

            db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = role.Id, AssignedAt = now });
            foreach (var warehouseId in warehouseIds)
            {
                db.Set<WarehouseRoleAssignment>().Add(new WarehouseRoleAssignment
                {
                    UserId = user.Id, WarehouseId = warehouseId, RoleId = role.Id
                });
            }
        }

        await db.SaveChangesAsync(cancellationToken);

        // ---- Products ----------------------------------------------------------------------------
        var products = new List<Product>(ProductCount);
        for (var i = 1; i <= ProductCount; i++)
        {
            var (category, unit, names, minPrice, maxPrice) = Catalog[(i - 1) % Catalog.Length];
            var price = Math.Round((minPrice + (decimal)random.NextDouble() * (maxPrice - minPrice)) / 1000m) * 1000m;
            products.Add(new Product
            {
                Sku = $"PRD-{i:D4}",
                Name = $"{Pick(random, names)} {Pick(random, Brands)} {Pick(random, Variants)} #{i:D4}",
                Description = $"Sản phẩm mẫu thuộc nhóm {category}",
                Category = category,
                UnitOfMeasure = unit,
                SalePrice = price,
                IsActive = true
            });
        }

        db.Products.AddRange(products);
        await db.SaveChangesAsync(cancellationToken);

        // ---- Opening stock: each product in 3–8 warehouses, one random bin each --------------------
        var inventories = new List<Inventory>();
        foreach (var product in products)
        {
            foreach (var warehouse in warehouses.OrderBy(_ => random.Next()).Take(random.Next(3, 9)))
            {
                var bins = binsByWarehouse[warehouse.Id];
                inventories.Add(new Inventory
                {
                    ProductId = product.Id,
                    WarehouseId = warehouse.Id,
                    LocationId = bins[random.Next(bins.Count)].Id,
                    Quantity = random.Next(10, 1001),
                    ReservedQuantity = 0,
                    MinQuantity = random.Next(5, 51),
                    CreatedAt = now
                });
            }
        }

        db.Set<Inventory>().AddRange(inventories);
        await db.SaveChangesAsync(cancellationToken);

        var managerByWarehouse = warehouses.ToDictionary(w => w.Id, w => w.ManagerId);
        var sequence = 0;
        foreach (var inventory in inventories)
        {
            sequence++;
            db.Set<StockMovement>().Add(new StockMovement
            {
                InventoryId = inventory.Id,
                ProductId = inventory.ProductId,
                WarehouseId = inventory.WarehouseId,
                LocationId = inventory.LocationId,
                MovementType = MovementType.Receipt,
                Quantity = inventory.Quantity,
                QuantityBefore = 0,
                QuantityAfter = inventory.Quantity,
                ReferenceType = ReferenceType,
                ReferenceNo = $"OB-{sequence:D6}",
                Note = "Tồn kho đầu kỳ (dữ liệu mẫu)",
                CreatedBy = managerByWarehouse[inventory.WarehouseId],
                CreatedAt = now
            });
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        log.LogInformation(
            "Seeded {Warehouses} warehouses, {Locations} locations, {Users} users, {Products} products, {Inventories} inventory rows. Password for all sample users: {Password}",
            warehouses.Count, warehouses.Count * 38, users.Count, products.Count, inventories.Count, DefaultPassword);
    }

    private static string WarehouseCode(int index) => $"WH-{index:D3}";

    private static T Pick<T>(Random random, T[] items) => items[random.Next(items.Length)];

    private static WarehouseLocation Location(int warehouseId, WarehouseLocation? parent, LocationType type,
        string segment, string code, string name, DateTime now) => new()
    {
        WarehouseId = warehouseId,
        Parent = parent,
        Type = type,
        SegmentCode = segment,
        Code = code,
        Name = name,
        IsActive = true,
        CreatedAt = now
    };
}
