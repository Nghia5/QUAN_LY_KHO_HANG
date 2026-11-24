namespace WMS.Api.Services;

public enum ServiceError
{
    None = 0,
    Validation,
    NotFound,
    Conflict
}

public class ServiceResult
{
    public ServiceError Error { get; init; } = ServiceError.None;
    public string? Message { get; init; }
    public bool Success => Error == ServiceError.None;

    public static ServiceResult Ok() => new();
    public static ServiceResult Fail(ServiceError error, string message) =>
        new() { Error = error, Message = message };
}

public class ServiceResult<T> : ServiceResult
{
    public T? Value { get; init; }

    public static ServiceResult<T> Ok(T value) => new() { Value = value };
    public new static ServiceResult<T> Fail(ServiceError error, string message) =>
        new() { Error = error, Message = message };
    public static ServiceResult<T> NotFound(string message) => Fail(ServiceError.NotFound, message);
    public static ServiceResult<T> Conflict(string message) => Fail(ServiceError.Conflict, message);
    public static ServiceResult<T> Invalid(string message) => Fail(ServiceError.Validation, message);
}

internal static class TextHelper
{
    public static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
