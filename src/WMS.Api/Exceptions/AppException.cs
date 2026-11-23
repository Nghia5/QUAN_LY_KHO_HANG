namespace WMS.Api.Exceptions;

/// <summary>Base type for expected business errors mapped to HTTP problem responses.</summary>
public abstract class AppException(string title, string message, int statusCode) : Exception(message)
{
    public string Title { get; } = title;

    public int StatusCode { get; } = statusCode;
}

public sealed class NotFoundException(string resource, object key)
    : AppException("Not found", $"{resource} '{key}' was not found.", StatusCodes.Status404NotFound);

public sealed class ConflictException(string message)
    : AppException("Conflict", message, StatusCodes.Status409Conflict);

/// <summary>Request is well-formed but violates a business rule.</summary>
public sealed class BusinessRuleException(string message)
    : AppException("Business rule violation", message, StatusCodes.Status422UnprocessableEntity);
