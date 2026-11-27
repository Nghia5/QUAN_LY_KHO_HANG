using FluentValidation;
using WMS.Api.Dtos.Locations;
using WMS.Api.Models;

namespace WMS.Api.Validators;

public sealed class CreateLocationRequestValidator : AbstractValidator<CreateLocationRequest>
{
    public CreateLocationRequestValidator()
    {
        RuleFor(request => request.Type).IsInEnum();
        RuleFor(request => request.SegmentCode)
            .ValidCode(10)
            .Must(code => !code.Contains('-'))
            .WithMessage("'Segment Code' must not contain '-' (it is the level separator).");
        RuleFor(request => request.Name).NotEmpty().MaximumLength(200);
        RuleFor(request => request.Description).MaximumLength(500);

        RuleFor(request => request.ParentId)
            .Null()
            .When(request => request.Type == LocationType.Zone)
            .WithMessage("A Zone cannot have a parent.");
        RuleFor(request => request.ParentId)
            .NotNull()
            .GreaterThan(0)
            .When(request => request.Type != LocationType.Zone)
            .WithMessage("Rack, Shelf and Bin require a parent location.");
    }
}

public sealed class UpdateLocationRequestValidator : AbstractValidator<UpdateLocationRequest>
{
    public UpdateLocationRequestValidator()
    {
        RuleFor(request => request.Name).NotEmpty().MaximumLength(200);
        RuleFor(request => request.Description).MaximumLength(500);
    }
}

public sealed class LocationQueryValidator : AbstractValidator<LocationQuery>
{
    public LocationQueryValidator()
    {
        this.ApplyPagingRules(LocationQuery.SortableFields);
        RuleFor(query => query.Type).IsInEnum();
        RuleFor(query => query.ParentId).GreaterThan(0);
    }
}
