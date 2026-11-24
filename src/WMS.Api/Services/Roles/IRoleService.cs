using WMS.Api.Dtos.Roles;

namespace WMS.Api.Services.Roles;

public interface IRoleService
{
    Task<IReadOnlyList<RoleResponse>> GetAllRolesAsync(CancellationToken cancellationToken);

    Task<RoleResponse> GetRoleByIdAsync(int id, CancellationToken cancellationToken);

    Task<RoleResponse> CreateRoleAsync(CreateRoleRequest request, CancellationToken cancellationToken);

    Task<RoleResponse> UpdateRoleAsync(int id, UpdateRoleRequest request, CancellationToken cancellationToken);

    Task<IReadOnlyList<PermissionResponse>> GetAllPermissionsAsync(CancellationToken cancellationToken);
}
