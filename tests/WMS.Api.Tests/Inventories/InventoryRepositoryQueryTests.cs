using FluentValidation.TestHelper;
using WMS.Api.Dtos.Inventories;
using WMS.Api.Repositories;
using WMS.Api.Validators;

namespace WMS.Api.Tests.Inventories;

public sealed class InventoryRepositoryQueryTests(InventoryQueryFixture fixture) : IClassFixture<InventoryQueryFixture>
{
    private const int TotalRows = 20;

    private async Task<Dtos.Common.PagedResult<InventoryResponse>> SearchAsync(InventoryQuery query)
    {
        await using var context = fixture.Database.CreateContext();
        return await new InventoryRepository(context).SearchAsync(query, CancellationToken.None);
    }

    [Fact]
    public async Task Paging_returns_requested_page_and_exact_total_count()
    {
        var page3 = await SearchAsync(new InventoryQuery { Page = 3, PageSize = 7 });

        Assert.Equal(TotalRows, page3.TotalCount);
        Assert.Equal(3, page3.TotalPages);
        Assert.Equal(6, page3.Items.Count); // 7 + 7 + 6

        var all = new List<int>();
        for (var page = 1; page <= 3; page++)
        {
            all.AddRange((await SearchAsync(new InventoryQuery { Page = page, PageSize = 7 })).Items.Select(i => i.Id));
        }

        Assert.Equal(TotalRows, all.Distinct().Count()); // stable order: no duplicates or gaps across pages
    }

    [Fact]
    public async Task Page_beyond_last_returns_empty_items_with_total()
    {
        var result = await SearchAsync(new InventoryQuery { Page = 99, PageSize = 10 });

        Assert.Empty(result.Items);
        Assert.Equal(TotalRows, result.TotalCount);
    }

    [Fact]
    public async Task Query_is_paged_in_sql_and_never_loads_the_whole_table()
    {
        fixture.Database.Sql.Clear();

        await SearchAsync(new InventoryQuery { Page = 2, PageSize = 5, WarehouseId = fixture.Warehouse1Id });

        var commands = fixture.Database.Sql.Commands;
        Assert.Equal(2, commands.Count);
        Assert.Contains(commands, sql => sql.Contains("COUNT(*)"));
        var pageSql = Assert.Single(commands, sql => sql.Contains("OFFSET"));
        Assert.Contains("FETCH NEXT", pageSql);
        Assert.DoesNotContain("[SalePrice]", pageSql); // projection: only the needed columns
    }

    [Fact]
    public async Task Filter_by_warehouse()
    {
        var result = await SearchAsync(new InventoryQuery { WarehouseId = fixture.Warehouse1Id, PageSize = 100 });

        Assert.Equal(15, result.TotalCount);
        Assert.All(result.Items, item => Assert.Equal("WH1", item.WarehouseCode));
    }

    [Fact]
    public async Task Filter_by_location()
    {
        var result = await SearchAsync(new InventoryQuery { LocationId = fixture.BinB2Id, PageSize = 100 });

        Assert.Equal(5, result.TotalCount);
        Assert.All(result.Items, item => Assert.Equal("X-01-01-B2", item.LocationCode));
    }

    [Fact]
    public async Task Filter_by_product()
    {
        var result = await SearchAsync(new InventoryQuery { ProductId = fixture.ProductIds[1], PageSize = 100 });

        Assert.Equal(3, result.TotalCount);
        Assert.All(result.Items, item => Assert.Equal("SKU-P01", item.ProductSku));
    }

    [Fact]
    public async Task Filter_by_category()
    {
        var result = await SearchAsync(new InventoryQuery { Category = "Food", PageSize = 100 });

        Assert.Equal(11, result.TotalCount); // B1: 5, B2: 3, C1: 3
    }

    [Fact]
    public async Task Filter_low_stock()
    {
        var result = await SearchAsync(new InventoryQuery { StockStatus = StockStatus.LowStock, PageSize = 100 });

        Assert.Equal(2, result.TotalCount);
        Assert.Equal(["SKU-P01", "SKU-P02"], result.Items.Select(item => item.ProductSku).Order());
        Assert.All(result.Items, item =>
        {
            Assert.Equal(StockStatus.LowStock, item.StockStatus);
            Assert.InRange(item.AvailableQuantity, 0.001m, item.MinQuantity);
        });
    }

    [Fact]
    public async Task Filter_out_of_stock_includes_fully_reserved_rows()
    {
        var result = await SearchAsync(new InventoryQuery { StockStatus = StockStatus.OutOfStock, PageSize = 100 });

        Assert.Equal(10, result.TotalCount); // B2 (qty 0) + C1 (qty 100, all reserved)
        Assert.All(result.Items, item => Assert.Equal(0m, item.AvailableQuantity));
    }

    [Fact]
    public async Task Filter_in_stock()
    {
        var result = await SearchAsync(new InventoryQuery { StockStatus = StockStatus.InStock, PageSize = 100 });

        Assert.Equal(8, result.TotalCount);
    }

    [Fact]
    public async Task Filter_has_stock_excludes_zero_quantity()
    {
        var result = await SearchAsync(new InventoryQuery { HasStock = true, PageSize = 100 });

        Assert.Equal(15, result.TotalCount);
        Assert.All(result.Items, item => Assert.True(item.Quantity > 0));
    }

    [Fact]
    public async Task Filters_combine_and_total_matches_filtered_rows()
    {
        var result = await SearchAsync(new InventoryQuery
        {
            WarehouseId = fixture.Warehouse1Id,
            StockStatus = StockStatus.OutOfStock,
            Category = "Food",
            Page = 1,
            PageSize = 2
        });

        Assert.Equal(3, result.TotalCount); // B2: P01, P03, P05
        Assert.Equal(2, result.Items.Count);
        Assert.Equal(2, result.TotalPages);
    }

    [Theory]
    [InlineData("SKU-P07", 1)]   // product SKU
    [InlineData("Product 03", 3)] // product name
    [InlineData("C1", 5)]        // location code
    [InlineData("WH2", 5)]       // warehouse code
    [InlineData("nothing", 0)]
    public async Task Keyword_matches_sku_name_location_and_warehouse(string keyword, int expected)
    {
        var result = await SearchAsync(new InventoryQuery { Keyword = keyword, PageSize = 100 });

        Assert.Equal(expected, result.TotalCount);
    }

    [Fact]
    public async Task Sort_by_available_quantity_descending()
    {
        var result = await SearchAsync(new InventoryQuery
        {
            SortBy = "availableQuantity", SortDir = "desc", PageSize = 100
        });

        var available = result.Items.Select(item => item.AvailableQuantity).ToList();
        Assert.Equal(available.OrderDescending(), available);
        Assert.Equal("SKU-P10", result.Items[0].ProductSku);
    }

    [Fact]
    public async Task Sort_by_location_code_ascending_is_case_insensitive_key()
    {
        var result = await SearchAsync(new InventoryQuery { SortBy = "LOCATIONCODE", PageSize = 100 });

        var codes = result.Items.Select(item => item.LocationCode).ToList();
        Assert.Equal(codes.Order(StringComparer.Ordinal), codes);
    }

    [Fact]
    public async Task Default_sort_is_product_sku()
    {
        var result = await SearchAsync(new InventoryQuery { PageSize = 3 });

        Assert.All(result.Items, item => Assert.Equal("SKU-P01", item.ProductSku));
    }

    [Theory]
    [InlineData("quantity; DROP TABLE Inventories")]
    [InlineData("RowVersion")]
    [InlineData("Product.SalePrice")]
    public void Sort_field_outside_whitelist_is_rejected(string sortBy)
    {
        var result = new InventoryQueryValidator().TestValidate(new InventoryQuery { SortBy = sortBy });

        result.ShouldHaveValidationErrorFor(query => query.SortBy);
    }

    [Theory]
    [InlineData(0, 20)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    public void Invalid_paging_is_rejected(int page, int pageSize)
    {
        var result = new InventoryQueryValidator().TestValidate(new InventoryQuery { Page = page, PageSize = pageSize });

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task Get_by_id_returns_projection_with_status()
    {
        var first = (await SearchAsync(new InventoryQuery { ProductId = fixture.ProductIds[1], LocationId = fixture.BinB1Id })).Items.Single();

        await using var context = fixture.Database.CreateContext();
        var item = await new InventoryRepository(context).GetByIdAsync(first.Id, CancellationToken.None);

        Assert.NotNull(item);
        Assert.Equal(10m, item.Quantity);
        Assert.Equal(StockStatus.LowStock, item.StockStatus);
        Assert.Equal("WH1", item.WarehouseCode);
        Assert.Null(await new InventoryRepository(context).GetByIdAsync(int.MaxValue, CancellationToken.None));
    }
}
