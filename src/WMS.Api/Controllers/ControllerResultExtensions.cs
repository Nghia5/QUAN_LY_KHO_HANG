using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using WMS.Api.Services;

namespace WMS.Api.Controllers;

public static class ControllerResultExtensions
{
    public static ActionResult ToErrorResult(this ControllerBase controller, ServiceResult result)
    {
        var statusCode = result.Error switch
        {
            ServiceError.NotFound => StatusCodes.Status404NotFound,
            ServiceError.Conflict => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status400BadRequest
        };

        return controller.Problem(detail: result.Message, statusCode: statusCode);
    }
}
