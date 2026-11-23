using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using WMS.Api.Exceptions;

namespace WMS.Api.Middleware;

public sealed class AppExceptionHandler(ILogger<AppExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is not AppException appException)
        {
            return false;
        }

        logger.LogInformation("Business error {Status}: {Message}", appException.StatusCode, appException.Message);

        httpContext.Response.StatusCode = appException.StatusCode;
        await httpContext.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = appException.StatusCode,
            Title = appException.Title,
            Detail = appException.Message,
            Instance = httpContext.Request.Path
        }, cancellationToken);

        return true;
    }
}
