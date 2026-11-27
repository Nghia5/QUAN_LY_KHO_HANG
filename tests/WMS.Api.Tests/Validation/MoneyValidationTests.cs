using System.ComponentModel.DataAnnotations;
using System.Globalization;
using WMS.Api.Dtos;
using WMS.Api.Dtos.GoodsReceipts;
using WMS.Api.Validators;

namespace WMS.Api.Tests.Validation;

public sealed class MoneyValidationTests
{
    [Theory]
    [InlineData("-0.01", false)]
    [InlineData("1.001", false)]
    [InlineData("10000000000000000", false)]
    [InlineData("9999999999999999.99", true)]
    [InlineData("0", true)]
    public void Product_price_and_receipt_cost_follow_sql_precision(string input, bool valid)
    {
        var amount = decimal.Parse(input, CultureInfo.InvariantCulture);
        var create = new CreateProductRequest { Sku = "SKU", Name = "Name", Category = "Test", UnitOfMeasure = "PCS", SalePrice = amount };
        var update = new UpdateProductRequest { Sku = "SKU", Name = "Name", Category = "Test", UnitOfMeasure = "PCS", SalePrice = amount, IsActive = true };
        var line = new GoodsReceiptLineRequest { ProductId = 1, LocationId = 1, Quantity = 1, UnitCost = amount };

        Assert.Equal(valid, Validator.TryValidateObject(create, new ValidationContext(create), null, true));
        Assert.Equal(valid, Validator.TryValidateObject(update, new ValidationContext(update), null, true));
        Assert.Equal(valid, new GoodsReceiptLineRequestValidator().Validate(line).IsValid);
    }
}
