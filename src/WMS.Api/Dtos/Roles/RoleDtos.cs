namespace WMS.Api.Dtos.Roles;

/// <summary>Summary representation of a role.</summary>
public sealed record RoleSummaryResponse(
    int Id,
    string Code,
    string Name);

/// <summary>Granular permission information.</summary>
public sealed record PermissionResponse(
    int Id,
    string Code,
    string Group,
    string? Description);

/// <summary>Detailed representation of a role including its permissions.</summary>
public sealed record RoleResponse(
    int Id,
    string Code,
    string Name,
    string? Description,
    bool IsActive,
    IReadOnlyList<PermissionResponse> Permissions);

/// <summary>Request payload for creating a new role.</summary>
public sealed record CreateRoleRequest
{
    public required string Code { get; init; }

    public required string Name { get; init; }

    public string? Description { get; init; }

    public IReadOnlyList<int> PermissionIds { get; init; } = [];
}

/// <summary>Request payload for updating an existing role. Code is immutable.</summary>
public sealed record UpdateRoleRequest
{
    public required string Name { get; init; }

    public string? Description { get; init; }

    public bool IsActive { get; init; } = true;

    public IReadOnlyList<int> PermissionIds { get; init; } = [];
}
