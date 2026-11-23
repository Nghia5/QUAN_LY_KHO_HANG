using System.Security.Claims;

namespace WMS.Api.Common;

public interface ICurrentUser
{
    /// <summary>Id of the authenticated user, or null when it cannot be resolved as an integer.</summary>
    int? UserId { get; }
}

public sealed class HttpContextCurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    public int? UserId
    {
        get
        {
            var user = httpContextAccessor.HttpContext?.User;
            var value = user?.FindFirstValue(ClaimTypes.NameIdentifier) ?? user?.FindFirstValue("sub");
            return int.TryParse(value, out var id) ? id : null;
        }
    }
}
