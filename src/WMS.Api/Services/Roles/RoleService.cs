using Microsoft.EntityFrameworkCore;
using WMS.Api.Data;
using WMS.Api.Dtos.Roles;
using WMS.Api.Exceptions;
using WMS.Api.Models;

namespace WMS.Api.Services.Roles;

public sealed class RoleService(
    WmsDbContext dbContext,
    TimeProvider timeProvider) : IRoleService
{
    public async Task<IReadOnlyList<RoleResponse>> GetAllRolesAsync(CancellationToken cancellationToken)
    {
        var roles = await dbContext.Roles
            .AsNoTracking()
            .Include(r => r.RolePermissions)
                .ThenInclude(rp => rp.Permission)
            .OrderBy(r => r.Name)
            .ToListAsync(cancellationToken);

        return roles.Select(ToRoleResponse).ToList();
    }

    public async Task<RoleResponse> GetRoleByIdAsync(int id, CancellationToken cancellationToken)
    {
        var role = await dbContext.Roles
            .Include(r => r.RolePermissions)
                .ThenInclude(rp => rp.Permission)
            .SingleOrDefaultAsync(r => r.Id == id, cancellationToken)
            ?? throw new NotFoundException("Role", id);

        return ToRoleResponse(role);
    }

    public async Task<RoleResponse> CreateRoleAsync(CreateRoleRequest request, CancellationToken cancellationToken)
    {
        var code = request.Code.Trim().ToUpperInvariant();
        if (await dbContext.Roles.AnyAsync(r => r.Code == code, cancellationToken))
        {
            throw new ConflictException($"Role code '{code}' already exists.");
        }

        var validPermissions = await dbContext.Permissions
            .Where(p => request.PermissionIds.Contains(p.Id))
            .ToListAsync(cancellationToken);

        var invalidIds = request.PermissionIds.Except(validPermissions.Select(p => p.Id)).ToList();
        if (invalidIds.Count > 0)
        {
            throw new BusinessRuleException($"Permission IDs not found: {string.Join(", ", invalidIds)}.");
        }

        var role = new Role
        {
            Code = code,
            Name = request.Name.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            IsActive = true,
            CreatedAt = timeProvider.GetUtcNow().UtcDateTime,
            RolePermissions = validPermissions.Select(p => new RolePermission
            {
                PermissionId = p.Id
            }).ToList()
        };

        dbContext.Roles.Add(role);
        await dbContext.SaveChangesAsync(cancellationToken);

        return ToRoleResponse(role);
    }

    public async Task<RoleResponse> UpdateRoleAsync(int id, UpdateRoleRequest request, CancellationToken cancellationToken)
    {
        var role = await dbContext.Roles
            .Include(r => r.RolePermissions)
                .ThenInclude(rp => rp.Permission)
            .SingleOrDefaultAsync(r => r.Id == id, cancellationToken)
            ?? throw new NotFoundException("Role", id);

        var validPermissions = await dbContext.Permissions
            .Where(p => request.PermissionIds.Contains(p.Id))
            .ToListAsync(cancellationToken);

        var invalidIds = request.PermissionIds.Except(validPermissions.Select(p => p.Id)).ToList();
        if (invalidIds.Count > 0)
        {
            throw new BusinessRuleException($"Permission IDs not found: {string.Join(", ", invalidIds)}.");
        }

        role.Name = request.Name.Trim();
        role.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        role.IsActive = request.IsActive;

        role.RolePermissions.Clear();
        foreach (var p in validPermissions)
        {
            role.RolePermissions.Add(new RolePermission
            {
                RoleId = role.Id,
                PermissionId = p.Id
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return ToRoleResponse(role);
    }

    public async Task<IReadOnlyList<PermissionResponse>> GetAllPermissionsAsync(CancellationToken cancellationToken) =>
        await dbContext.Permissions
            .AsNoTracking()
            .OrderBy(p => p.Group)
            .ThenBy(p => p.Code)
            .Select(p => new PermissionResponse(p.Id, p.Code, p.Group, p.Description))
            .ToListAsync(cancellationToken);

    private static RoleResponse ToRoleResponse(Role role) =>
        new(role.Id,
            role.Code,
            role.Name,
            role.Description,
            role.IsActive,
            role.RolePermissions
                .Select(rp => new PermissionResponse(rp.Permission.Id, rp.Permission.Code, rp.Permission.Group, rp.Permission.Description))
                .ToList());
}
