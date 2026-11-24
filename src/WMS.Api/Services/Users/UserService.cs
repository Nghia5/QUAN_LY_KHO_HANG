using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using WMS.Api.Common;
using WMS.Api.Data;
using WMS.Api.Dtos.Common;
using WMS.Api.Dtos.Roles;
using WMS.Api.Dtos.Users;
using WMS.Api.Exceptions;
using WMS.Api.Models;
using WMS.Api.Services.Auth;

namespace WMS.Api.Services.Users;

public sealed class UserService(
    WmsDbContext dbContext,
    IPasswordHasher passwordHasher,
    ITokenService tokenService,
    ICurrentUser currentUser,
    TimeProvider timeProvider) : IUserService
{
    private static readonly Dictionary<string, Expression<Func<User, object?>>> SortFields =
        QueryableExtensions.SortMap<User>(
            ("id", u => u.Id),
            ("username", u => u.Username),
            ("fullName", u => u.FullName),
            ("email", u => u.Email),
            ("createdAt", u => u.CreatedAt),
            ("isActive", u => u.IsActive));

    public async Task<PagedResult<UserResponse>> SearchAsync(UserQuery query, CancellationToken cancellationToken)
    {
        var queryable = dbContext.Users.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var keyword = query.Keyword.Trim();
            queryable = queryable.Where(u =>
                u.Username.Contains(keyword) ||
                u.FullName.Contains(keyword) ||
                u.Email.Contains(keyword));
        }

        if (query.RoleId.HasValue)
        {
            queryable = queryable.Where(u => u.UserRoles.Any(ur => ur.RoleId == query.RoleId.Value));
        }

        if (query.IsActive.HasValue)
        {
            queryable = queryable.Where(u => u.IsActive == query.IsActive.Value);
        }

        var sorted = queryable.ApplySort(query, SortFields, defaultSort: "id", tieBreaker: u => u.Id);

        var projected = sorted.Select(u => new UserResponse(
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
            u.UserRoles.Select(ur => ur.Role.Name).ToList()));

        return await projected.ToPagedResultAsync(query, cancellationToken);
    }

    public async Task<UserDetailResponse> GetByIdAsync(int id, CancellationToken cancellationToken)
    {
        var user = await dbContext.Users
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
                    .ThenInclude(r => r.RolePermissions)
                        .ThenInclude(rp => rp.Permission)
            .SingleOrDefaultAsync(u => u.Id == id, cancellationToken)
            ?? throw new NotFoundException("User", id);

        var roles = user.UserRoles
            .Select(ur => new RoleSummaryResponse(ur.Role.Id, ur.Role.Code, ur.Role.Name))
            .ToList();

        var permissions = user.UserRoles
            .Where(ur => ur.Role.IsActive)
            .SelectMany(ur => ur.Role.RolePermissions)
            .Select(rp => rp.Permission.Code)
            .Distinct()
            .ToList();

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
            permissions);
    }

    public async Task<UserResponse> CreateAsync(CreateUserRequest request, CancellationToken cancellationToken)
    {
        var username = request.Username.Trim();
        var email = request.Email.Trim().ToLowerInvariant();

        if (await dbContext.Users.AnyAsync(u => u.Username == username, cancellationToken))
        {
            throw new ConflictException($"Username '{username}' is already in use.");
        }

        if (await dbContext.Users.AnyAsync(u => u.Email == email, cancellationToken))
        {
            throw new ConflictException($"Email '{email}' is already in use.");
        }

        // Validate roles exist
        var existingRoleIds = await dbContext.Roles
            .Where(r => request.RoleIds.Contains(r.Id))
            .Select(r => r.Id)
            .ToListAsync(cancellationToken);

        var invalidRoles = request.RoleIds.Except(existingRoleIds).ToList();
        if (invalidRoles.Count > 0)
        {
            throw new BusinessRuleException($"Role IDs not found: {string.Join(", ", invalidRoles)}.");
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var user = new User
        {
            Username = username,
            Email = email,
            PasswordHash = passwordHasher.HashPassword(request.Password),
            FullName = request.FullName.Trim(),
            PhoneNumber = TrimToNull(request.PhoneNumber),
            AvatarUrl = TrimToNull(request.AvatarUrl),
            IsActive = true,
            CreatedAt = now,
            UserRoles = existingRoleIds.Select(roleId => new UserRole
            {
                RoleId = roleId,
                AssignedAt = now
            }).ToList()
        };

        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync(cancellationToken);

        var roleNames = await dbContext.Roles
            .Where(r => existingRoleIds.Contains(r.Id))
            .Select(r => r.Name)
            .ToListAsync(cancellationToken);

        return new UserResponse(
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
            roleNames);
    }

    public async Task<UserResponse> UpdateAsync(int id, UpdateUserRequest request, CancellationToken cancellationToken)
    {
        var user = await dbContext.Users
            .Include(u => u.UserRoles)
            .SingleOrDefaultAsync(u => u.Id == id, cancellationToken)
            ?? throw new NotFoundException("User", id);

        var email = request.Email.Trim().ToLowerInvariant();
        if (email != user.Email && await dbContext.Users.AnyAsync(u => u.Email == email && u.Id != id, cancellationToken))
        {
            throw new ConflictException($"Email '{email}' is already in use by another user.");
        }

        var existingRoleIds = await dbContext.Roles
            .Where(r => request.RoleIds.Contains(r.Id))
            .Select(r => r.Id)
            .ToListAsync(cancellationToken);

        var invalidRoles = request.RoleIds.Except(existingRoleIds).ToList();
        if (invalidRoles.Count > 0)
        {
            throw new BusinessRuleException($"Role IDs not found: {string.Join(", ", invalidRoles)}.");
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;

        user.FullName = request.FullName.Trim();
        user.Email = email;
        user.PhoneNumber = TrimToNull(request.PhoneNumber);
        user.AvatarUrl = TrimToNull(request.AvatarUrl);
        user.UpdatedAt = now;

        // Synchronize roles
        user.UserRoles.Clear();
        foreach (var roleId in existingRoleIds)
        {
            user.UserRoles.Add(new UserRole
            {
                UserId = user.Id,
                RoleId = roleId,
                AssignedAt = now
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        var roleNames = await dbContext.Roles
            .Where(r => existingRoleIds.Contains(r.Id))
            .Select(r => r.Name)
            .ToListAsync(cancellationToken);

        return new UserResponse(
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
            roleNames);
    }

    public async Task ChangePasswordAsync(int id, ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        var user = await dbContext.Users.FindAsync([id], cancellationToken)
            ?? throw new NotFoundException("User", id);

        if (!passwordHasher.VerifyPassword(request.CurrentPassword, user.PasswordHash))
        {
            throw new BusinessRuleException("Current password is incorrect.");
        }

        user.PasswordHash = passwordHasher.HashPassword(request.NewPassword);
        user.UpdatedAt = timeProvider.GetUtcNow().UtcDateTime;

        // Invalidate active sessions on password change
        await tokenService.RevokeAllUserTokensAsync(id, null, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task AdminResetPasswordAsync(int id, AdminResetPasswordRequest request, CancellationToken cancellationToken)
    {
        var user = await dbContext.Users.FindAsync([id], cancellationToken)
            ?? throw new NotFoundException("User", id);

        user.PasswordHash = passwordHasher.HashPassword(request.NewPassword);
        user.UpdatedAt = timeProvider.GetUtcNow().UtcDateTime;

        await tokenService.RevokeAllUserTokensAsync(id, null, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<UserResponse> SoftDeleteAsync(int id, CancellationToken cancellationToken)
    {
        var user = await dbContext.Users
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .SingleOrDefaultAsync(u => u.Id == id, cancellationToken)
            ?? throw new NotFoundException("User", id);

        if (!user.IsActive)
        {
            // Already soft-deleted
            return ToUserResponse(user);
        }

        var deletedByUserId = currentUser.UserId ?? 0;
        user.SoftDelete(deletedByUserId);

        // Revoke all tokens immediately so user cannot perform any further authenticated actions
        await tokenService.RevokeAllUserTokensAsync(id, null, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return ToUserResponse(user);
    }

    public async Task<UserResponse> RestoreAsync(int id, CancellationToken cancellationToken)
    {
        var user = await dbContext.Users
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .SingleOrDefaultAsync(u => u.Id == id, cancellationToken)
            ?? throw new NotFoundException("User", id);

        if (!user.IsActive)
        {
            user.Restore();
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return ToUserResponse(user);
    }

    private static UserResponse ToUserResponse(User user) =>
        new(user.Id,
            user.Username,
            user.Email,
            user.FullName,
            user.PhoneNumber,
            user.AvatarUrl,
            user.IsActive,
            user.CreatedAt,
            user.UpdatedAt,
            user.DeletedAt,
            user.UserRoles.Select(ur => ur.Role.Name).ToList());

    private static string? TrimToNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
