using WMS.Api.Dtos.Common;
using WMS.Api.Dtos.Users;

namespace WMS.Api.Services.Users;

public interface IUserService
{
    Task<PagedResult<UserResponse>> SearchAsync(UserQuery query, CancellationToken cancellationToken);

    Task<UserDetailResponse> GetByIdAsync(int id, CancellationToken cancellationToken);

    Task<UserResponse> CreateAsync(CreateUserRequest request, CancellationToken cancellationToken);

    Task<UserResponse> UpdateAsync(int id, UpdateUserRequest request, CancellationToken cancellationToken);

    Task ChangePasswordAsync(int id, ChangePasswordRequest request, CancellationToken cancellationToken);

    Task AdminResetPasswordAsync(int id, AdminResetPasswordRequest request, CancellationToken cancellationToken);

    Task<UserResponse> SoftDeleteAsync(int id, CancellationToken cancellationToken);

    Task<UserResponse> RestoreAsync(int id, CancellationToken cancellationToken);
}
