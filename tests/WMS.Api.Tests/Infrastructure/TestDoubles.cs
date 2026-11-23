using WMS.Api.Common;

namespace WMS.Api.Tests.Infrastructure;

public sealed class FixedCurrentUser(int? userId) : ICurrentUser
{
    public int? UserId { get; } = userId;
}

/// <summary>Manually controlled clock.</summary>
public sealed class ManualTimeProvider(DateTimeOffset start) : TimeProvider
{
    private DateTimeOffset now = start;

    public override DateTimeOffset GetUtcNow() => now;

    public void Set(DateTimeOffset value) => now = value;
}
