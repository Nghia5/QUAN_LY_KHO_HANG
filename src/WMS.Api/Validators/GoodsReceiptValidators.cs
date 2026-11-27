using FluentValidation;
using WMS.Api.Dtos.Documents;
using WMS.Api.Dtos.GoodsReceipts;

namespace WMS.Api.Validators;

public sealed class GoodsReceiptLineRequestValidator : AbstractValidator<GoodsReceiptLineRequest>
{
    public const int MaxQuantity = 1_000_000_000;

    public GoodsReceiptLineRequestValidator()
    {
        RuleFor(line => line.ProductId).GreaterThan(0);
        RuleFor(line => line.LocationId).GreaterThan(0);
        RuleFor(line => line.Quantity)
            .GreaterThan(0)
            .LessThan(MaxQuantity)
            .Must(quantity => decimal.Round(quantity, 3) == quantity)
            .WithMessage("'Quantity' supports at most 3 decimal places.");
        RuleFor(line => line.UnitCost).GreaterThanOrEqualTo(0);
        RuleFor(line => line.BatchCode).MaximumLength(50);
        RuleFor(line => line.Note).MaximumLength(500);

        RuleFor(line => line.BatchCode)
            .NotEmpty()
            .When(line => line.ManufactureDate is not null || line.ExpiryDate is not null)
            .WithMessage("'Batch Code' is required when a manufacture or expiry date is given.");
        RuleFor(line => line.ExpiryDate)
            .GreaterThan(line => line.ManufactureDate)
            .When(line => line.ExpiryDate is not null && line.ManufactureDate is not null)
            .WithMessage("'Expiry Date' must be after 'Manufacture Date'.");
    }
}

/// <summary>Rules shared by create and update (header fields + lines).</summary>
public sealed class GoodsReceiptPayloadValidator : AbstractValidator<IGoodsReceiptPayload>
{
    public const int MaxLines = 500;

    public GoodsReceiptPayloadValidator()
    {
        RuleFor(request => request.SupplierId).GreaterThan(0);
        RuleFor(request => request.SupplierName).MaximumLength(200);
        RuleFor(request => request.ReferenceNo).MaximumLength(50);
        RuleFor(request => request.Note).MaximumLength(1000);
        RuleFor(request => request.Lines)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("A receipt needs at least one line.")
            .Must(lines => lines.Count <= MaxLines).WithMessage($"A receipt can have at most {MaxLines} lines.");
        RuleForEach(request => request.Lines)
            .Cascade(CascadeMode.Stop).NotNull().SetValidator(new GoodsReceiptLineRequestValidator());
    }
}

public sealed class CreateGoodsReceiptRequestValidator : AbstractValidator<CreateGoodsReceiptRequest>
{
    public CreateGoodsReceiptRequestValidator()
    {
        RuleFor(request => request.WarehouseId).GreaterThan(0);
        Include(new GoodsReceiptPayloadValidator());
    }
}

public sealed class UpdateGoodsReceiptRequestValidator : AbstractValidator<UpdateGoodsReceiptRequest>
{
    public UpdateGoodsReceiptRequestValidator() => Include(new GoodsReceiptPayloadValidator());
}

public sealed class GoodsReceiptQueryValidator : AbstractValidator<GoodsReceiptQuery>
{
    public GoodsReceiptQueryValidator()
    {
        this.ApplyPagingRules(GoodsReceiptQuery.SortableFields);
        RuleFor(query => query.WarehouseId).GreaterThan(0);
        RuleFor(query => query.SupplierId).GreaterThan(0);
        RuleFor(query => query.ProductId).GreaterThan(0);
        RuleFor(query => query.Status).IsInEnum();
        RuleFor(query => query.ToDate)
            .GreaterThanOrEqualTo(query => query.FromDate)
            .When(query => query.FromDate is not null && query.ToDate is not null);
    }
}

public sealed class DocumentReasonRequestValidator : AbstractValidator<DocumentReasonRequest>
{
    public DocumentReasonRequestValidator() => RuleFor(request => request.Reason).NotEmpty().MaximumLength(500);
}
