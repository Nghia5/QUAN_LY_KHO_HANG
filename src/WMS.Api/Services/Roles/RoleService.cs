using WMS.Api.Repositories;
using WMS.Api.Dtos.Roles;
using WMS.Api.Exceptions;
using WMS.Api.Models;
using WMS.Api.Services.Users;

namespace WMS.Api.Services.Roles;

public sealed class RoleService(
    IRoleRepository roleRepository,
    IPermissionRepository permissionRepository,
    TimeProvider timeProvider) : IRoleService
{
    public async Task<IReadOnlyList<RoleResponse>> GetAllRolesAsync(CancellationToken cancellationToken)
    {
        var roles = await roleRepository.ListAsync(cancellationToken);

        return roles.Select(ToRoleResponse).ToList();
    }

    public async Task<RoleResponse> GetRoleByIdAsync(int id, CancellationToken cancellationToken)
    {
        var role = await roleRepository.FindAsync(id, cancellationToken)
            ?? throw new NotFoundException("Role", id);

        return ToRoleResponse(role);
    }

    public async Task<RoleResponse> CreateRoleAsync(CreateRoleRequest request, CancellationToken cancellationToken)
    {
        var code = request.Code.Trim().ToUpperInvariant();
        if (await roleRepository.CodeExistsAsync(code, cancellationToken))
        {
            throw new ConflictException($"Role code '{code}' already exists.");
        }

        var validPermissions = await permissionRepository.FindByIdsAsync(request.PermissionIds, cancellationToken);

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
                PermissionId = p.Id,
                Permission = p
            }).ToList()
        };

        roleRepository.Add(role);
        await roleRepository.SaveChangesAsync(cancellationToken);

        return ToRoleResponse(role);
    }

    public async Task<RoleResponse> UpdateRoleAsync(int id, UpdateRoleRequest request, CancellationToken cancellationToken)
    {
        var role = await roleRepository.FindAsync(id, cancellationToken)
            ?? throw new NotFoundException("Role", id);
        AdminAccountGuard.EnsureCanDeactivate(role, request.IsActive);

        var validPermissions = await permissionRepository.FindByIdsAsync(request.PermissionIds, cancellationToken);

        var invalidIds = request.PermissionIds.Except(validPermissions.Select(p => p.Id)).ToList();
        if (invalidIds.Count > 0)
        {
            throw new BusinessRuleException($"Permission IDs not found: {string.Join(", ", invalidIds)}.");
        }

        role.Name = request.Name.Trim();
        role.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        role.IsActive = request.IsActive;

        var permissionIds = validPermissions.Select(p => p.Id).ToHashSet();
        foreach (var assignment in role.RolePermissions.Where(rp => !permissionIds.Contains(rp.PermissionId)).ToList())
        {
            role.RolePermissions.Remove(assignment);
        }

        var assignedIds = role.RolePermissions.Select(rp => rp.PermissionId).ToHashSet();
        foreach (var permission in validPermissions.Where(p => !assignedIds.Contains(p.Id)))
        {
            role.RolePermissions.Add(new RolePermission
            {
                RoleId = role.Id,
                PermissionId = permission.Id,
                Permission = permission
            });
        }

        await roleRepository.SaveChangesAsync(cancellationToken);

        return ToRoleResponse(role);
    }

    public async Task<IReadOnlyList<PermissionResponse>> GetAllPermissionsAsync(CancellationToken cancellationToken) =>
        (await permissionRepository.ListAsync(cancellationToken))
            .Select(p => new PermissionResponse(p.Id, p.Code, p.Group, p.Description))
            .ToList();

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
