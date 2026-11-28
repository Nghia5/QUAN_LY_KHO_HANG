using FluentValidation;
using WMS.Api.Dtos.Inventories;

namespace WMS.Api.Validators;

public sealed class InventoryQueryValidator : AbstractValidator<InventoryQuery>
{
    public InventoryQueryValidator()
    {
        this.ApplyPagingRules(InventoryQuery.SortableFields);
        RuleFor(query => query.WarehouseId).GreaterThan(0);
        RuleFor(query => query.LocationId).GreaterThan(0);
        RuleFor(query => query.ProductId).GreaterThan(0);
        RuleFor(query => query.Category).MaximumLength(100);
        RuleFor(query => query.StockStatus).IsInEnum();
    }
}
