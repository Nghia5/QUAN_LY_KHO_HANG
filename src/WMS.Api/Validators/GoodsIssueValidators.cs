using FluentValidation;
using WMS.Api.Dtos.GoodsIssues;

namespace WMS.Api.Validators;

public sealed class GoodsIssueLineRequestValidator : AbstractValidator<GoodsIssueLineRequest>
{
    public GoodsIssueLineRequestValidator()
    {
        RuleFor(line => line.ProductId).GreaterThan(0);
        RuleFor(line => line.LocationId).GreaterThan(0);
        RuleFor(line => line.Quantity)
            .GreaterThan(0)
            .LessThan(GoodsReceiptLineRequestValidator.MaxQuantity)
            .Must(quantity => decimal.Round(quantity, 3) == quantity)
            .WithMessage("'Quantity' supports at most 3 decimal places.");
        RuleFor(line => line.BatchId).GreaterThan(0);
        RuleFor(line => line.Note).MaximumLength(500);
    }
}

public sealed class GoodsIssuePayloadValidator : AbstractValidator<IGoodsIssuePayload>
{
    public GoodsIssuePayloadValidator()
    {
        RuleFor(request => request.CustomerId).GreaterThan(0);
        RuleFor(request => request.CustomerName).MaximumLength(200);
        RuleFor(request => request.ReferenceNo).MaximumLength(50);
        RuleFor(request => request.Note).MaximumLength(1000);
        RuleFor(request => request.Lines)
            .NotEmpty().WithMessage("An issue needs at least one line.")
            .Must(lines => lines.Count <= GoodsReceiptPayloadValidator.MaxLines)
            .WithMessage($"An issue can have at most {GoodsReceiptPayloadValidator.MaxLines} lines.");
        RuleForEach(request => request.Lines).SetValidator(new GoodsIssueLineRequestValidator());
    }
}

public sealed class CreateGoodsIssueRequestValidator : AbstractValidator<CreateGoodsIssueRequest>
{
    public CreateGoodsIssueRequestValidator()
    {
        RuleFor(request => request.WarehouseId).GreaterThan(0);
        Include(new GoodsIssuePayloadValidator());
    }
}

public sealed class UpdateGoodsIssueRequestValidator : AbstractValidator<UpdateGoodsIssueRequest>
{
    public UpdateGoodsIssueRequestValidator() => Include(new GoodsIssuePayloadValidator());
}

public sealed class GoodsIssueQueryValidator : AbstractValidator<GoodsIssueQuery>
{
    public GoodsIssueQueryValidator()
    {
        this.ApplyPagingRules(GoodsIssueQuery.SortableFields);
        RuleFor(query => query.WarehouseId).GreaterThan(0);
        RuleFor(query => query.CustomerId).GreaterThan(0);
        RuleFor(query => query.ProductId).GreaterThan(0);
        RuleFor(query => query.Status).IsInEnum();
        RuleFor(query => query.ToDate)
            .GreaterThanOrEqualTo(query => query.FromDate)
            .When(query => query.FromDate is not null && query.ToDate is not null);
    }
}
