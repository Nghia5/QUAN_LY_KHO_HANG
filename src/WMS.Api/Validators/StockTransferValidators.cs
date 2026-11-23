using FluentValidation;
using WMS.Api.Dtos.StockTransfers;

namespace WMS.Api.Validators;

public sealed class StockTransferLineRequestValidator : AbstractValidator<StockTransferLineRequest>
{
    public StockTransferLineRequestValidator()
    {
        RuleFor(line => line.ProductId).GreaterThan(0);
        RuleFor(line => line.FromLocationId).GreaterThan(0);
        RuleFor(line => line.ToLocationId)
            .GreaterThan(0)
            .NotEqual(line => line.FromLocationId)
            .WithMessage("'To Location' must differ from 'From Location'.");
        RuleFor(line => line.Quantity)
            .GreaterThan(0)
            .LessThan(GoodsReceiptLineRequestValidator.MaxQuantity)
            .Must(quantity => decimal.Round(quantity, 3) == quantity)
            .WithMessage("'Quantity' supports at most 3 decimal places.");
        RuleFor(line => line.Note).MaximumLength(500);
    }
}

public sealed class StockTransferPayloadValidator : AbstractValidator<IStockTransferPayload>
{
    public StockTransferPayloadValidator()
    {
        RuleFor(request => request.Reason).MaximumLength(500);
        RuleFor(request => request.Note).MaximumLength(1000);
        RuleFor(request => request.Lines)
            .NotEmpty().WithMessage("A transfer needs at least one line.")
            .Must(lines => lines.Count <= GoodsReceiptPayloadValidator.MaxLines)
            .WithMessage($"A transfer can have at most {GoodsReceiptPayloadValidator.MaxLines} lines.");
        RuleForEach(request => request.Lines).SetValidator(new StockTransferLineRequestValidator());
    }
}

public sealed class CreateStockTransferRequestValidator : AbstractValidator<CreateStockTransferRequest>
{
    public CreateStockTransferRequestValidator()
    {
        RuleFor(request => request.WarehouseId).GreaterThan(0);
        RuleFor(request => request.DestinationWarehouseId).GreaterThan(0);
        Include(new StockTransferPayloadValidator());
    }
}

public sealed class UpdateStockTransferRequestValidator : AbstractValidator<UpdateStockTransferRequest>
{
    public UpdateStockTransferRequestValidator() => Include(new StockTransferPayloadValidator());
}

public sealed class StockTransferQueryValidator : AbstractValidator<StockTransferQuery>
{
    public StockTransferQueryValidator()
    {
        this.ApplyPagingRules(StockTransferQuery.SortableFields);
        RuleFor(query => query.WarehouseId).GreaterThan(0);
        RuleFor(query => query.SourceWarehouseId).GreaterThan(0);
        RuleFor(query => query.DestinationWarehouseId).GreaterThan(0);
        RuleFor(query => query.ProductId).GreaterThan(0);
        RuleFor(query => query.Status).IsInEnum();
    }
}
