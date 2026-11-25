using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WMS.Api.Authorization;
using WMS.Api.Dtos.Roles;
using WMS.Api.Services.Roles;

namespace WMS.Api.Controllers;

[ApiController]
[Authorize(Policy = ActiveAdminRequirement.PolicyName)]
[Route("api/roles")]
public sealed class RolesController(IRoleService roleService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<RoleResponse>>> GetAll(CancellationToken cancellationToken) =>
        Ok(await roleService.GetAllRolesAsync(cancellationToken));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<RoleResponse>> GetById(int id, CancellationToken cancellationToken) =>
        Ok(await roleService.GetRoleByIdAsync(id, cancellationToken));

    [HttpPost]
    public async Task<ActionResult<RoleResponse>> Create(CreateRoleRequest request, CancellationToken cancellationToken)
    {
        var role = await roleService.CreateRoleAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = role.Id }, role);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<RoleResponse>> Update(int id, UpdateRoleRequest request, CancellationToken cancellationToken) =>
        Ok(await roleService.UpdateRoleAsync(id, request, cancellationToken));

    [HttpGet("permissions")]
    public async Task<ActionResult<IReadOnlyList<PermissionResponse>>> GetPermissions(CancellationToken cancellationToken) =>
        Ok(await roleService.GetAllPermissionsAsync(cancellationToken));
}
