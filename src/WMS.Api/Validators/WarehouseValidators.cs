using FluentValidation;
using WMS.Api.Dtos.Warehouses;

namespace WMS.Api.Validators;

public sealed class CreateWarehouseRequestValidator : AbstractValidator<CreateWarehouseRequest>
{
    public CreateWarehouseRequestValidator()
    {
        RuleFor(request => request.Code).ValidCode(20);
        RuleFor(request => request.Name).NotEmpty().MaximumLength(200);
        RuleFor(request => request.Address).MaximumLength(500);
        RuleFor(request => request.Phone).MaximumLength(20).Matches(@"^[0-9+()\-\s]*$");
        RuleFor(request => request.Description).MaximumLength(1000);
        RuleFor(request => request.ManagerId).GreaterThan(0);
    }
}

public sealed class UpdateWarehouseRequestValidator : AbstractValidator<UpdateWarehouseRequest>
{
    public UpdateWarehouseRequestValidator()
    {
        RuleFor(request => request.Name).NotEmpty().MaximumLength(200);
        RuleFor(request => request.Address).MaximumLength(500);
        RuleFor(request => request.Phone).MaximumLength(20).Matches(@"^[0-9+()\-\s]*$");
        RuleFor(request => request.Description).MaximumLength(1000);
        RuleFor(request => request.ManagerId).GreaterThan(0);
    }
}

public sealed class WarehouseQueryValidator : AbstractValidator<WarehouseQuery>
{
    public WarehouseQueryValidator()
    {
        this.ApplyPagingRules(WarehouseQuery.SortableFields);
        RuleFor(query => query.ManagerId).GreaterThan(0);
    }
}
