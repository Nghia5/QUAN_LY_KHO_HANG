using FluentValidation;
using WMS.Api.Dtos.Batches;

namespace WMS.Api.Validators;

public sealed class CreateBatchRequestValidator : AbstractValidator<CreateBatchRequest>
{
    public CreateBatchRequestValidator()
    {
        RuleFor(request => request.ProductId).GreaterThan(0);
        RuleFor(request => request.BatchCode).ValidCode(50);
        RuleFor(request => request.Note).MaximumLength(500);
        RuleFor(request => request.ExpiryDate)
            .GreaterThan(request => request.ManufactureDate)
            .When(request => request.ExpiryDate is not null && request.ManufactureDate is not null)
            .WithMessage("'Expiry Date' must be after 'Manufacture Date'.");
    }
}

public sealed class UpdateBatchRequestValidator : AbstractValidator<UpdateBatchRequest>
{
    public UpdateBatchRequestValidator()
    {
        RuleFor(request => request.Note).MaximumLength(500);
        RuleFor(request => request.ExpiryDate)
            .GreaterThan(request => request.ManufactureDate)
            .When(request => request.ExpiryDate is not null && request.ManufactureDate is not null)
            .WithMessage("'Expiry Date' must be after 'Manufacture Date'.");
    }
}

public sealed class BatchQueryValidator : AbstractValidator<BatchQuery>
{
    public BatchQueryValidator()
    {
        this.ApplyPagingRules(BatchQuery.SortableFields);
        RuleFor(query => query.ProductId).GreaterThan(0);
        RuleFor(query => query.WarehouseId).GreaterThan(0);
        RuleFor(query => query.ExpiryStatus).IsInEnum();
        RuleFor(query => query.ExpiringWithinDays).InclusiveBetween(0, 3650);
    }
}

public sealed class ExpiryAlertQueryValidator : AbstractValidator<ExpiryAlertQuery>
{
    public ExpiryAlertQueryValidator()
    {
        this.ApplyPagingRules([]);
        RuleFor(query => query.WarehouseId).GreaterThan(0);
        RuleFor(query => query.ProductId).GreaterThan(0);
        RuleFor(query => query.Days).InclusiveBetween(0, 3650);
        RuleFor(query => query.Status)
            .Must(status => status is null or ExpiryStatus.NearExpiry or ExpiryStatus.Expired)
            .WithMessage("'Status' must be NearExpiry or Expired.");
    }
}

public sealed class PickSuggestionQueryValidator : AbstractValidator<PickSuggestionQuery>
{
    public PickSuggestionQueryValidator()
    {
        RuleFor(query => query.ProductId).GreaterThan(0);
        RuleFor(query => query.WarehouseId).GreaterThan(0);
        RuleFor(query => query.Quantity).GreaterThan(0);
        RuleFor(query => query.Strategy).IsInEnum();
    }
}
