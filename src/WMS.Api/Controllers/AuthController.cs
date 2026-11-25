using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using WMS.Api.Dtos.Auth;
using WMS.Api.Common;
using WMS.Api.Dtos.Users;
using WMS.Api.Exceptions;
using WMS.Api.Services.Users;
using WMS.Api.Services.Auth;

namespace WMS.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/auth")]
public sealed class AuthController(IAuthService authService, IUserService userService, ICurrentUser currentUser) : ControllerBase
{
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request, CancellationToken cancellationToken) =>
        Ok(await authService.LoginAsync(request, HttpContext.Connection.RemoteIpAddress?.ToString(), cancellationToken));

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> Refresh(RefreshTokenRequest request, CancellationToken cancellationToken) =>
        Ok(await authService.RefreshTokenAsync(request, HttpContext.Connection.RemoteIpAddress?.ToString(), cancellationToken));

    [HttpPost("revoke")]
    public async Task<IActionResult> Revoke(
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] RevokeTokenRequest? request,
        CancellationToken cancellationToken)
    {
        await authService.RevokeTokenAsync(
            request ?? new RevokeTokenRequest(), HttpContext.Connection.RemoteIpAddress?.ToString(), cancellationToken);
        return NoContent();
    }

    [HttpGet("me")]
    public async Task<ActionResult<CurrentUserProfileResponse>> Me(CancellationToken cancellationToken) =>
        Ok(await authService.GetCurrentUserProfileAsync(cancellationToken));

    [HttpPut("~/api/users/me/password")]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedException();
        await userService.ChangePasswordAsync(userId, request, cancellationToken);
        return NoContent();
    }
}
