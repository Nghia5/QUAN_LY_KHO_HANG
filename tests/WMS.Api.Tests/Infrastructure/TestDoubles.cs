using WMS.Api.Common;

namespace WMS.Api.Tests.Infrastructure;

public sealed class FixedCurrentUser(int? userId, params string[] permissions) : ICurrentUser
{
    public int? UserId { get; set; } = userId;

    public HashSet<string> Permissions { get; } = [.. permissions];

    public bool HasPermission(string permission) => Permissions.Contains(permission);
}

/// <summary>Manually controlled clock.</summary>
public sealed class ManualTimeProvider(DateTimeOffset start) : TimeProvider
{
    private DateTimeOffset now = start;

    public override DateTimeOffset GetUtcNow() => now;

    public void Set(DateTimeOffset value) => now = value;
}
