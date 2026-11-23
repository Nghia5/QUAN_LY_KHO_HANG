using FluentValidation;
using WMS.Api.Dtos.StockMovements;

namespace WMS.Api.Validators;

public sealed class StockMovementQueryValidator : AbstractValidator<StockMovementQuery>
{
    public StockMovementQueryValidator()
    {
        this.ApplyPagingRules(StockMovementQuery.SortableFields);
        RuleFor(query => query.WarehouseId).GreaterThan(0);
        RuleFor(query => query.LocationId).GreaterThan(0);
        RuleFor(query => query.ProductId).GreaterThan(0);
        RuleFor(query => query.ReferenceId).GreaterThan(0);
        RuleFor(query => query.MovementType).IsInEnum();
        RuleFor(query => query.ReferenceType).MaximumLength(30);
        RuleFor(query => query.To)
            .GreaterThan(query => query.From)
            .When(query => query.From is not null && query.To is not null)
            .WithMessage("'To' must be after 'From'.");
    }
}

public sealed class StockReportQueryValidator : AbstractValidator<StockReportQuery>
{
    public StockReportQueryValidator()
    {
        this.ApplyPagingRules(StockReportQuery.SortableFields);
        RuleFor(query => query.WarehouseId).GreaterThan(0);
        RuleFor(query => query.ProductId).GreaterThan(0);
        RuleFor(query => query.From).NotEmpty();
        RuleFor(query => query.To).NotEmpty().GreaterThan(query => query.From).WithMessage("'To' must be after 'From'.");
        RuleFor(query => query)
            .Must(query => query.To - query.From <= TimeSpan.FromDays(366))
            .WithName("To")
            .WithMessage("The report period cannot exceed 366 days.");
    }
}
