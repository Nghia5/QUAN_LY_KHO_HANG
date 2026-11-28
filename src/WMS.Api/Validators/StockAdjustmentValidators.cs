using FluentValidation;
using WMS.Api.Dtos.StockAdjustments;

namespace WMS.Api.Validators;

public sealed class StockAdjustmentLineRequestValidator : AbstractValidator<StockAdjustmentLineRequest>
{
    public StockAdjustmentLineRequestValidator()
    {
        RuleFor(line => line.ProductId).GreaterThan(0);
        RuleFor(line => line.LocationId).GreaterThan(0);
        RuleFor(line => line.AdjustmentQuantity)
            .NotEqual(0).WithMessage("'Adjustment Quantity' must not be 0.")
            .InclusiveBetween(-GoodsReceiptLineRequestValidator.MaxQuantity, GoodsReceiptLineRequestValidator.MaxQuantity)
            .Must(quantity => decimal.Round(quantity, 3) == quantity)
            .WithMessage("'Adjustment Quantity' supports at most 3 decimal places.");
        RuleFor(line => line.Note).MaximumLength(500);
    }
}

public sealed class StockAdjustmentPayloadValidator : AbstractValidator<IStockAdjustmentPayload>
{
    public StockAdjustmentPayloadValidator()
    {
        RuleFor(request => request.ReasonCode).IsInEnum();
        RuleFor(request => request.Reason)
            .NotEmpty().WithMessage("A reason is required for every stock adjustment.")
            .Must(reason => reason is not null && reason.Trim().Length >= 5).WithMessage("'Reason' must have at least 5 characters.")
            .MaximumLength(500);
        RuleFor(request => request.Note).MaximumLength(1000);
        RuleFor(request => request.Lines)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("An adjustment needs at least one line.")
            .Must(lines => lines.Count <= GoodsReceiptPayloadValidator.MaxLines)
            .WithMessage($"An adjustment can have at most {GoodsReceiptPayloadValidator.MaxLines} lines.");
        RuleForEach(request => request.Lines)
            .Cascade(CascadeMode.Stop).NotNull().SetValidator(new StockAdjustmentLineRequestValidator());
    }
}

public sealed class CreateStockAdjustmentRequestValidator : AbstractValidator<CreateStockAdjustmentRequest>
{
    public CreateStockAdjustmentRequestValidator()
    {
        RuleFor(request => request.WarehouseId).GreaterThan(0);
        Include(new StockAdjustmentPayloadValidator());
    }
}

public sealed class UpdateStockAdjustmentRequestValidator : AbstractValidator<UpdateStockAdjustmentRequest>
{
    public UpdateStockAdjustmentRequestValidator() => Include(new StockAdjustmentPayloadValidator());
}

public sealed class StockAdjustmentQueryValidator : AbstractValidator<StockAdjustmentQuery>
{
    public StockAdjustmentQueryValidator()
    {
        this.ApplyPagingRules(StockAdjustmentQuery.SortableFields);
        RuleFor(query => query.WarehouseId).GreaterThan(0);
        RuleFor(query => query.ProductId).GreaterThan(0);
        RuleFor(query => query.Status).IsInEnum();
        RuleFor(query => query.ReasonCode).IsInEnum();
    }
}
