using System.Linq.Expressions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using WMS.Api.Common;
using WMS.Api.Data;
using WMS.Api.Dtos.Common;
using WMS.Api.Dtos.Roles;
using WMS.Api.Dtos.Users;
using WMS.Api.Exceptions;
using WMS.Api.Models;

namespace WMS.Api.Repositories;

public interface IUserRepository
{
    /// <summary>Server-side paged search with keyword, role and status filtering.</summary>
    Task<PagedResult<UserResponse>> SearchAsync(UserQuery query, CancellationToken cancellationToken);

    /// <summary>Returns full user detail including roles and flattened permissions.</summary>
    Task<UserDetailResponse?> GetDetailByIdAsync(int id, CancellationToken cancellationToken);

    /// <summary>Returns a tracked entity for mutation (update / soft-delete / restore).</summary>
    Task<User?> FindAsync(int id, CancellationToken cancellationToken);

    Task<User?> FindByIdentifierAsync(string identifier, CancellationToken cancellationToken);
    Task<User?> GetWithPermissionsAsync(int id, CancellationToken cancellationToken);
    Task<bool> IsActiveAdminAsync(int userId, CancellationToken cancellationToken);
    Task<IReadOnlyList<string>> GetUserPermissionsAsync(int userId, CancellationToken cancellationToken);
    Task<IReadOnlyList<string>> GetUserRoleNamesAsync(int userId, CancellationToken cancellationToken);

    /// <summary>Checks whether the given username is already taken.</summary>
    Task<bool> UsernameExistsAsync(string username, CancellationToken cancellationToken);

    /// <summary>Checks whether the given email is already taken, optionally excluding one user.</summary>
    Task<bool> EmailExistsAsync(string email, int? excludeUserId, CancellationToken cancellationToken);

    /// <summary>Stages a new user entity for insert; call SaveChangesAsync to persist.</summary>
    void Add(User user);

    /// <summary>
    /// Persists all pending changes.
    /// Converts SqlException 2601/2627 (unique-key) → <see cref="ConflictException"/>.
    /// </summary>
    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public sealed class UserRepository(WmsDbContext dbContext) : IUserRepository
{
    private static readonly IReadOnlyDictionary<string, Expression<Func<User, object?>>> SortMap =
        QueryableExtensions.SortMap<User>(
            ("id",        u => u.Id),
            ("username",  u => u.Username),
            ("fullName",  u => u.FullName),
            ("email",     u => u.Email),
            ("createdAt", u => u.CreatedAt),
            ("isActive",  u => u.IsActive));

    public async Task<PagedResult<UserResponse>> SearchAsync(
        UserQuery query,
        CancellationToken cancellationToken)
    {
        var users = dbContext.Set<User>().AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var keyword = query.Keyword.Trim();
            users = users.Where(u =>
                u.Username.Contains(keyword) ||
                u.FullName.Contains(keyword) ||
                u.Email.Contains(keyword));
        }

        if (query.WarehouseId is int warehouseId)
        {
            users = users.Where(u => u.WarehouseRoleAssignments.Any(a =>
                a.WarehouseId == warehouseId && (query.RoleId == null || a.RoleId == query.RoleId)));
        }
        else if (query.RoleId is int roleId)
        {
            users = users.Where(u => u.UserRoles.Any(ur => ur.RoleId == roleId) ||
                u.WarehouseRoleAssignments.Any(a => a.RoleId == roleId));
        }

        if (query.IsActive.HasValue)
        {
            users = users.Where(u => u.IsActive == query.IsActive.Value);
        }

        return await users
            .ApplySort(query, SortMap, "id", u => u.Id)
            .Select(u => new UserResponse(
                u.Id,
                u.Username,
                u.Email,
                u.FullName,
                u.PhoneNumber,
                u.AvatarUrl,
                u.IsActive,
                u.CreatedAt,
                u.UpdatedAt,
                u.DeletedAt,
                u.UserRoles.Select(ur => ur.Role.Name).ToList())
            {
                WarehouseRoles = u.WarehouseRoleAssignments
                    .OrderBy(a => a.WarehouseId).ThenBy(a => a.RoleId)
                    .Select(a => new UserWarehouseRoleResponse(a.WarehouseId, a.Warehouse.Code, a.RoleId, a.Role.Code, a.Role.Name))
                    .ToList()
            })
            .ToPagedResultAsync(query, cancellationToken);
    }

    public async Task<UserDetailResponse?> GetDetailByIdAsync(int id, CancellationToken cancellationToken)
    {
        var user = await GetWithPermissionsAsync(id, cancellationToken);

        if (user is null)
        {
            return null;
        }

        var roles = user.UserRoles
            .Select(ur => new RoleSummaryResponse(ur.Role.Id, ur.Role.Code, ur.Role.Name))
            .ToList();

        var permissions = user.UserRoles
            .Where(ur => ur.Role.IsActive)
            .SelectMany(ur => ur.Role.RolePermissions)
            .Select(rp => rp.Permission.Code)
            .Distinct()
            .OrderBy(c => c)
            .ToList();

        var warehouseRoles = await dbContext.Set<WarehouseRoleAssignment>()
            .Where(a => a.UserId == id)
            .OrderBy(a => a.WarehouseId).ThenBy(a => a.RoleId)
            .Select(a => new UserWarehouseRoleResponse(a.WarehouseId, a.Warehouse.Code, a.RoleId, a.Role.Code, a.Role.Name))
            .ToListAsync(cancellationToken);

        return new UserDetailResponse(
            user.Id,
            user.Username,
            user.Email,
            user.FullName,
            user.PhoneNumber,
            user.AvatarUrl,
            user.IsActive,
            user.CreatedAt,
            user.UpdatedAt,
            user.DeletedAt,
            user.DeletedBy,
            roles,
            permissions)
        {
            WarehouseRoles = warehouseRoles
        };
    }

    public Task<User?> FindAsync(int id, CancellationToken cancellationToken) =>
        dbContext.Set<User>()
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .SingleOrDefaultAsync(u => u.Id == id, cancellationToken);

    public Task<bool> UsernameExistsAsync(string username, CancellationToken cancellationToken) =>
        dbContext.Set<User>()
            .AnyAsync(u => u.Username == username, cancellationToken);

    public Task<bool> EmailExistsAsync(string email, int? excludeUserId, CancellationToken cancellationToken) =>
        excludeUserId.HasValue
            ? dbContext.Set<User>().AnyAsync(u => u.Email == email && u.Id != excludeUserId.Value, cancellationToken)
            : dbContext.Set<User>().AnyAsync(u => u.Email == email, cancellationToken);

    public void Add(User user) => dbContext.Set<User>().Add(user);

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (exception.GetBaseException() is SqlException { Number: 2601 or 2627 } sqlEx)
        {
            // Distinguish username vs email from the constraint name when available
            var msg = sqlEx.Message.Contains("Email", StringComparison.OrdinalIgnoreCase)
                ? "A user with this email address already exists."
                : "A user with this username already exists.";

            throw new ConflictException(msg);
        }
    }

    public Task<User?> FindByIdentifierAsync(string identifier, CancellationToken cancellationToken) =>
        dbContext.Users.AsNoTracking()
            .SingleOrDefaultAsync(u => u.Username == identifier || u.Email == identifier, cancellationToken);

    public Task<User?> GetWithPermissionsAsync(int id, CancellationToken cancellationToken) =>
        dbContext.Set<User>()
            .AsNoTracking()
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
                    .ThenInclude(r => r.RolePermissions)
                        .ThenInclude(rp => rp.Permission)
            .SingleOrDefaultAsync(u => u.Id == id, cancellationToken);

    public Task<bool> IsActiveAdminAsync(int userId, CancellationToken cancellationToken) =>
        dbContext.Users.AnyAsync(user => user.Id == userId && user.IsActive &&
            user.UserRoles.Any(assignment => assignment.Role.IsActive && assignment.Role.Code == "ADMIN"),
            cancellationToken);

    public async Task<IReadOnlyList<string>> GetUserPermissionsAsync(int userId, CancellationToken cancellationToken) =>
        await dbContext.UserRoles
            .Where(ur => ur.UserId == userId && ur.Role.IsActive)
            .SelectMany(ur => ur.Role.RolePermissions)
            .Select(rp => rp.Permission.Code)
            .Distinct()
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<string>> GetUserRoleNamesAsync(int userId, CancellationToken cancellationToken) =>
        await dbContext.UserRoles
            .Where(ur => ur.UserId == userId && ur.Role.IsActive)
            .Select(ur => ur.Role.Name)
            .Distinct()
            .ToListAsync(cancellationToken);
}
