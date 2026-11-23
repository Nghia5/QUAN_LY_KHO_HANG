using FluentValidation;
using WMS.Api.Dtos.Stocktakes;

namespace WMS.Api.Validators;

public sealed class CreateStocktakeRequestValidator : AbstractValidator<CreateStocktakeRequest>
{
    public CreateStocktakeRequestValidator()
    {
        RuleFor(request => request.WarehouseId).GreaterThan(0);
        RuleFor(request => request.ScopeLocationIds).Must(ids => ids.Count <= 100).WithMessage("At most 100 scope locations.");
        RuleForEach(request => request.ScopeLocationIds).GreaterThan(0);
        RuleFor(request => request.Note).MaximumLength(1000);
    }
}

public sealed class UpdateStocktakeRequestValidator : AbstractValidator<UpdateStocktakeRequest>
{
    public UpdateStocktakeRequestValidator()
    {
        RuleFor(request => request.ScopeLocationIds).Must(ids => ids.Count <= 100).WithMessage("At most 100 scope locations.");
        RuleForEach(request => request.ScopeLocationIds).GreaterThan(0);
        RuleFor(request => request.Note).MaximumLength(1000);
    }
}

public sealed class StocktakeCountItemValidator : AbstractValidator<StocktakeCountItem>
{
    public StocktakeCountItemValidator()
    {
        RuleFor(item => item.ProductId).GreaterThan(0);
        RuleFor(item => item.LocationId).GreaterThan(0);
        RuleFor(item => item.CountedQuantity)
            .GreaterThanOrEqualTo(0)
            .LessThan(GoodsReceiptLineRequestValidator.MaxQuantity)
            .Must(quantity => decimal.Round(quantity, 3) == quantity)
            .WithMessage("'Counted Quantity' supports at most 3 decimal places.");
        RuleFor(item => item.Note).MaximumLength(500);
    }
}

public sealed class StocktakeCountRequestValidator : AbstractValidator<StocktakeCountRequest>
{
    public StocktakeCountRequestValidator()
    {
        RuleFor(request => request.Items)
            .NotEmpty()
            .Must(items => items.Count <= 1000).WithMessage("At most 1000 items per request.")
            .Must(items => items.Select(i => (i.ProductId, i.LocationId)).Distinct().Count() == items.Count)
            .WithMessage("Each product and location may appear only once per request.");
        RuleForEach(request => request.Items).SetValidator(new StocktakeCountItemValidator());
    }
}

public sealed class StocktakeQueryValidator : AbstractValidator<StocktakeQuery>
{
    public StocktakeQueryValidator()
    {
        this.ApplyPagingRules(StocktakeQuery.SortableFields);
        RuleFor(query => query.WarehouseId).GreaterThan(0);
        RuleFor(query => query.Status).IsInEnum();
    }
}
