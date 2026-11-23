using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using WMS.Api.Exceptions;

namespace WMS.Api.Middleware;

public sealed class ProductSkuConflictExceptionHandler(
    ILogger<ProductSkuConflictExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is not ProductSkuConflictException)
        {
            return false;
        }

        logger.LogWarning(exception, "A product SKU conflict occurred.");

        httpContext.Response.StatusCode = StatusCodes.Status409Conflict;
        await httpContext.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = StatusCodes.Status409Conflict,
            Title = "Product SKU conflict",
            Detail = "A product with this SKU already exists.",
            Instance = httpContext.Request.Path
        }, cancellationToken);

        return true;
    }
}
