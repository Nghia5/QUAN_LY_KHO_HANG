using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WMS.Api.Authorization;
using WMS.Api.Dtos.Common;
using WMS.Api.Dtos.Users;
using WMS.Api.Services.Users;

namespace WMS.Api.Controllers;

[ApiController]
[Authorize(Policy = ActiveAdminRequirement.PolicyName)]
[Route("api/users")]
public sealed class UsersController(IUserService userService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<UserResponse>>> Search(
        [FromQuery] UserQuery query, CancellationToken cancellationToken) =>
        Ok(await userService.SearchAsync(query, cancellationToken));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<UserDetailResponse>> GetById(int id, CancellationToken cancellationToken) =>
        Ok(await userService.GetByIdAsync(id, cancellationToken));

    [HttpPost]
    public async Task<ActionResult<UserResponse>> Create(CreateUserRequest request, CancellationToken cancellationToken)
    {
        var user = await userService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = user.Id }, user);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<UserResponse>> Update(int id, UpdateUserRequest request, CancellationToken cancellationToken) =>
        Ok(await userService.UpdateAsync(id, request, cancellationToken));

    [HttpPost("{id:int}/reset-password")]
    public async Task<IActionResult> ResetPassword(int id, AdminResetPasswordRequest request, CancellationToken cancellationToken)
    {
        await userService.AdminResetPasswordAsync(id, request, cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    public async Task<ActionResult<UserResponse>> SoftDelete(int id, CancellationToken cancellationToken) =>
        Ok(await userService.SoftDeleteAsync(id, cancellationToken));

    [HttpPost("{id:int}/restore")]
    public async Task<ActionResult<UserResponse>> Restore(int id, CancellationToken cancellationToken) =>
        Ok(await userService.RestoreAsync(id, cancellationToken));
}
