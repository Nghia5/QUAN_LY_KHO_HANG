using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WMS.Api.Authorization;
using WMS.Api.Data;
using WMS.Api.Exceptions;
using WMS.Api.Models;

namespace WMS.Api.Controllers;

[ApiController]
[Authorize(Policy = ActiveAdminRequirement.PolicyName)]
[Route("api/users/{userId:int}/warehouse-roles")]
public sealed class WarehouseRoleAssignmentsController(WmsDbContext dbContext) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<WarehouseRoleResponse>>> List(int userId, CancellationToken cancellationToken) =>
        Ok(await dbContext.Set<WarehouseRoleAssignment>()
            .Where(a => a.UserId == userId)
            .OrderBy(a => a.WarehouseId).ThenBy(a => a.RoleId)
            .Select(a => new WarehouseRoleResponse(a.WarehouseId, a.RoleId, a.Role.Code))
            .ToListAsync(cancellationToken));

    [HttpPut("{warehouseId:int}/{roleId:int}")]
    public async Task<IActionResult> Assign(int userId, int warehouseId, int roleId, CancellationToken cancellationToken)
    {
        if (!await dbContext.Users.AnyAsync(user => user.Id == userId && user.IsActive, cancellationToken))
        {
            throw new NotFoundException("Active user", userId);
        }

        if (!await dbContext.Set<Warehouse>().AnyAsync(warehouse => warehouse.Id == warehouseId, cancellationToken))
        {
            throw new NotFoundException("Warehouse", warehouseId);
        }

        if (!await dbContext.Roles.AnyAsync(role => role.Id == roleId && role.IsActive && role.Code != "ADMIN", cancellationToken))
        {
            throw new NotFoundException("Active warehouse role", roleId);
        }

        var assignments = dbContext.Set<WarehouseRoleAssignment>();
        if (await assignments.FindAsync([userId, warehouseId, roleId], cancellationToken) is null)
        {
            assignments.Add(new WarehouseRoleAssignment { UserId = userId, WarehouseId = warehouseId, RoleId = roleId });
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return NoContent();
    }

    [HttpDelete("{warehouseId:int}/{roleId:int}")]
    public async Task<IActionResult> Remove(int userId, int warehouseId, int roleId, CancellationToken cancellationToken)
    {
        await dbContext.Set<WarehouseRoleAssignment>()
            .Where(a => a.UserId == userId && a.WarehouseId == warehouseId && a.RoleId == roleId)
            .ExecuteDeleteAsync(cancellationToken);
        return NoContent();
    }
}

public sealed record WarehouseRoleResponse(int WarehouseId, int RoleId, string RoleCode);
