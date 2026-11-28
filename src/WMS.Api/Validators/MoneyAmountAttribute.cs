using System.ComponentModel.DataAnnotations;

namespace WMS.Api.Validators;

[AttributeUsage(AttributeTargets.Property)]
internal sealed class MoneyAmountAttribute : ValidationAttribute
{
    internal const string Message = "The amount must be between 0 and 9999999999999999.99 with at most 2 decimal places.";

    internal static bool IsValidAmount(decimal amount) =>
        amount is >= 0 and <= 9999999999999999.99m && decimal.Round(amount, 2) == amount;

    public override bool IsValid(object? value) =>
        value is null || (value is decimal amount && IsValidAmount(amount));

    public override string FormatErrorMessage(string name) => Message;
}
