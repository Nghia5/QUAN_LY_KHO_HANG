using WMS.Api.Common;
using WMS.Api.Repositories;
using WMS.Api.Dtos.Common;
using WMS.Api.Dtos.Users;
using WMS.Api.Exceptions;
using WMS.Api.Models;
using WMS.Api.Services.Auth;

namespace WMS.Api.Services.Users;

public sealed class UserService(
    IUserRepository userRepository,
    IRoleRepository roleRepository,
    IPasswordHasher passwordHasher,
    ITokenService tokenService,
    ICurrentUser currentUser,
    TimeProvider timeProvider,
    AdminAccountGuard adminGuard) : IUserService
{
    public Task<PagedResult<UserResponse>> SearchAsync(UserQuery query, CancellationToken cancellationToken) =>
        userRepository.SearchAsync(query, cancellationToken);

    public async Task<UserDetailResponse> GetByIdAsync(int id, CancellationToken cancellationToken) =>
        await userRepository.GetDetailByIdAsync(id, cancellationToken)
        ?? throw new NotFoundException("User", id);

    public async Task<UserResponse> CreateAsync(CreateUserRequest request, CancellationToken cancellationToken)
    {
        var username = request.Username.Trim();
        var email = request.Email.Trim().ToLowerInvariant();

        if (await userRepository.UsernameExistsAsync(username, cancellationToken))
        {
            throw new ConflictException($"Username '{username}' is already in use.");
        }

        if (await userRepository.EmailExistsAsync(email, null, cancellationToken))
        {
            throw new ConflictException($"Email '{email}' is already in use.");
        }

        // Validate roles exist
        var roles = await roleRepository.FindByIdsAsync(request.RoleIds, cancellationToken);
        var existingRoleIds = roles.Select(r => r.Id).ToHashSet();

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

        userRepository.Add(user);
        await userRepository.SaveChangesAsync(cancellationToken);

        var roleNames = roles.Select(r => r.Name).ToList();

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
        await using var transaction = await adminGuard.BeginAsync(cancellationToken);
        var user = await userRepository.FindAsync(id, cancellationToken)
            ?? throw new NotFoundException("User", id);

        var email = request.Email.Trim().ToLowerInvariant();
        if (email != user.Email && await userRepository.EmailExistsAsync(email, id, cancellationToken))
        {
            throw new ConflictException($"Email '{email}' is already in use by another user.");
        }

        var roles = await roleRepository.FindByIdsAsync(request.RoleIds, cancellationToken);
        var existingRoleIds = roles.Select(r => r.Id).ToHashSet();

        var invalidRoles = request.RoleIds.Except(existingRoleIds).ToList();
        if (invalidRoles.Count > 0)
        {
            throw new BusinessRuleException($"Role IDs not found: {string.Join(", ", invalidRoles)}.");
        }

        await adminGuard.EnsureCanRemoveAsync(user, existingRoleIds, cancellationToken);
        var now = timeProvider.GetUtcNow().UtcDateTime;

        user.FullName = request.FullName.Trim();
        user.Email = email;
        user.PhoneNumber = TrimToNull(request.PhoneNumber);
        user.AvatarUrl = TrimToNull(request.AvatarUrl);
        user.UpdatedAt = now;

        // Synchronize roles
        foreach (var assignment in user.UserRoles.Where(ur => !existingRoleIds.Contains(ur.RoleId)).ToList())
        {
            user.UserRoles.Remove(assignment);
        }

        var assignedRoles = user.UserRoles.ToDictionary(ur => ur.RoleId);
        foreach (var roleId in existingRoleIds)
        {
            if (assignedRoles.TryGetValue(roleId, out var assignment))
            {
                assignment.AssignedAt = now;
                continue;
            }

            user.UserRoles.Add(new UserRole
            {
                UserId = user.Id,
                RoleId = roleId,
                AssignedAt = now
            });
        }

        await userRepository.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var roleNames = roles.Select(r => r.Name).ToList();

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
        var user = await userRepository.FindAsync(id, cancellationToken)
            ?? throw new NotFoundException("User", id);

        if (!passwordHasher.VerifyPassword(request.CurrentPassword, user.PasswordHash))
        {
            throw new BusinessRuleException("Current password is incorrect.");
        }

        user.PasswordHash = passwordHasher.HashPassword(request.NewPassword);
        user.UpdatedAt = timeProvider.GetUtcNow().UtcDateTime;

        // Invalidate active sessions on password change
        await tokenService.RevokeAllUserTokensAsync(id, null, cancellationToken);
    }

    public async Task AdminResetPasswordAsync(int id, AdminResetPasswordRequest request, CancellationToken cancellationToken)
    {
        var user = await userRepository.FindAsync(id, cancellationToken)
            ?? throw new NotFoundException("User", id);

        user.PasswordHash = passwordHasher.HashPassword(request.NewPassword);
        user.UpdatedAt = timeProvider.GetUtcNow().UtcDateTime;

        await tokenService.RevokeAllUserTokensAsync(id, null, cancellationToken);
    }

    public async Task<UserResponse> SoftDeleteAsync(int id, CancellationToken cancellationToken)
    {
        await using var transaction = await adminGuard.BeginAsync(cancellationToken);
        var user = await userRepository.FindAsync(id, cancellationToken)
            ?? throw new NotFoundException("User", id);

        if (!user.IsActive)
        {
            // Already soft-deleted
            return ToUserResponse(user);
        }

        await adminGuard.EnsureCanRemoveAsync(user, null, cancellationToken);
        var deletedByUserId = currentUser.UserId ?? 0;
        user.SoftDelete(deletedByUserId);

        // Revoke all tokens immediately so user cannot perform any further authenticated actions
        await tokenService.RevokeAllUserTokensAsync(id, null, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return ToUserResponse(user);
    }

    public async Task<UserResponse> RestoreAsync(int id, CancellationToken cancellationToken)
    {
        var user = await userRepository.FindAsync(id, cancellationToken)
            ?? throw new NotFoundException("User", id);

        if (!user.IsActive)
        {
            user.Restore();
            await userRepository.SaveChangesAsync(cancellationToken);
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
