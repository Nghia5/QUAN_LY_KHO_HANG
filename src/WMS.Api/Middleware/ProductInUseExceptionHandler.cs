using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using WMS.Api.Exceptions;

namespace WMS.Api.Middleware;

public sealed class ProductInUseExceptionHandler(
    ILogger<ProductInUseExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is not ProductInUseException)
        {
            return false;
        }

        logger.LogWarning(exception, "A product referenced by other records could not be deleted.");

        httpContext.Response.StatusCode = StatusCodes.Status409Conflict;
        await httpContext.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = StatusCodes.Status409Conflict,
            Title = "Product is in use",
            Detail = "This product is referenced by other records and cannot be deleted.",
            Instance = httpContext.Request.Path
        }, cancellationToken);

        return true;
    }
}
