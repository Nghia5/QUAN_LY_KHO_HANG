using FluentValidation;
using WMS.Api.Dtos.Common;

namespace WMS.Api.Validators;

public static class CommonRules
{
    public static void ApplyPagingRules<T>(this AbstractValidator<T> validator, IEnumerable<string> sortableFields)
        where T : PagedRequest
    {
        var fields = sortableFields.ToArray();

        validator.RuleFor(request => request.Page).GreaterThanOrEqualTo(1);
        validator.RuleFor(request => request.PageSize).InclusiveBetween(1, PagedRequest.MaxPageSize);
        validator.RuleFor(request => request.Keyword).MaximumLength(200);
        validator.RuleFor(request => request.SortDir)
            .Must(dir => dir is null
                         || dir.Equals("asc", StringComparison.OrdinalIgnoreCase)
                         || dir.Equals("desc", StringComparison.OrdinalIgnoreCase))
            .WithMessage("SortDir must be 'asc' or 'desc'.");
        validator.RuleFor(request => request.SortBy)
            .Must(sortBy => sortBy is null || fields.Contains(sortBy, StringComparer.OrdinalIgnoreCase))
            .WithMessage($"SortBy must be one of: {string.Join(", ", fields)}.");
    }

    /// <summary>Business code: letters, digits, '-', '_', '.'; stored upper-case.</summary>
    public static IRuleBuilderOptions<T, string> ValidCode<T>(this IRuleBuilder<T, string> rule, int maxLength) =>
        rule.NotEmpty()
            .MaximumLength(maxLength)
            .Matches("^[A-Za-z0-9][A-Za-z0-9._-]*$")
            .WithMessage("'{PropertyName}' may only contain letters, digits, '.', '_' and '-'.");

    public static string NormalizeCode(string code) => code.Trim().ToUpperInvariant();
}
