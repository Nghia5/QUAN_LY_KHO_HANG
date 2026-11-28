using FluentValidation;
using WMS.Api.Dtos.GoodsIssues;
using WMS.Api.Dtos.GoodsReceipts;
using WMS.Api.Dtos.StockAdjustments;
using WMS.Api.Dtos.Stocktakes;
using WMS.Api.Dtos.StockTransfers;
using WMS.Api.Validators;

namespace WMS.Api.Tests.Validation;

public sealed class DocumentCollectionValidationTests
{
    [Fact]
    public void Document_collections_reject_null_lists_and_items()
    {
        AssertInvalid(new CreateGoodsReceiptRequestValidator(), new CreateGoodsReceiptRequest { Lines = null! }, "Lines");
        AssertInvalid(new CreateGoodsReceiptRequestValidator(), new CreateGoodsReceiptRequest { Lines = [null!] }, "Lines[0]");
        AssertInvalid(new CreateGoodsIssueRequestValidator(), new CreateGoodsIssueRequest { Lines = [null!] }, "Lines[0]");
        AssertInvalid(new CreateStockTransferRequestValidator(), new CreateStockTransferRequest { Lines = [null!] }, "Lines[0]");
        AssertInvalid(new CreateStockAdjustmentRequestValidator(), new CreateStockAdjustmentRequest { Lines = [null!] }, "Lines[0]");
        AssertInvalid(new StocktakeCountRequestValidator(), new StocktakeCountRequest { Items = null! }, "Items");
        AssertInvalid(new StocktakeCountRequestValidator(), new StocktakeCountRequest { Items = [null!] }, "Items[0]");
        AssertInvalid(new CreateStocktakeRequestValidator(), new CreateStocktakeRequest { ScopeLocationIds = null! }, "ScopeLocationIds");
        AssertInvalid(new UpdateStocktakeRequestValidator(), new UpdateStocktakeRequest { ScopeLocationIds = null! }, "ScopeLocationIds");
    }

    private static void AssertInvalid<T>(IValidator<T> validator, T request, string property) =>
        Assert.Contains(validator.Validate(request).Errors, error => error.PropertyName == property);
}
