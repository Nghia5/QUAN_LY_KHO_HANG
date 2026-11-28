using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace WMS.Api.Data.Configurations;

/// <summary>All timestamps are stored as UTC; mark values read back from SQL Server as UTC.</summary>
public static class UtcDateTimeConverters
{
    public static readonly ValueConverter<DateTime, DateTime> Required = new(
        value => value,
        value => DateTime.SpecifyKind(value, DateTimeKind.Utc));

    public static readonly ValueConverter<DateTime?, DateTime?> Optional = new(
        value => value,
        value => value.HasValue ? DateTime.SpecifyKind(value.Value, DateTimeKind.Utc) : value);
}
