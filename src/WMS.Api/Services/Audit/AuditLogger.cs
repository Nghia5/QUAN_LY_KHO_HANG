using System.Text.Json;
using WMS.Api.Common;
using WMS.Api.Data;
using WMS.Api.Models;

namespace WMS.Api.Services.Audit;

public interface IAuditLogger
{
    /// <summary>
    /// Queues an audit entry on the current DbContext; it is persisted by the caller's next
    /// SaveChanges, i.e. inside the same transaction as the business change.
    /// </summary>
    void Log(string entityType, object entityId, string action, object? data = null);
}

public sealed class AuditLogger(WmsDbContext dbContext, ICurrentUser currentUser, TimeProvider timeProvider) : IAuditLogger
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public void Log(string entityType, object entityId, string action, object? data = null) =>
        dbContext.Set<AuditLog>().Add(new AuditLog
        {
            EntityType = entityType,
            EntityId = Convert.ToString(entityId, System.Globalization.CultureInfo.InvariantCulture) ?? "",
            Action = action,
            UserId = currentUser.UserId,
            CreatedAt = timeProvider.GetUtcNow().UtcDateTime,
            Data = data is null ? null : JsonSerializer.Serialize(data, JsonOptions)
        });
}
