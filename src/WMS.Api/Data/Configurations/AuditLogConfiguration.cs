using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WMS.Api.Models;

namespace WMS.Api.Data.Configurations;

public sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public const string ImmutableTriggerName = "TR_AuditLogs_Immutable";

    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("AuditLogs", table => table.HasTrigger(ImmutableTriggerName));

        builder.HasKey(log => log.Id);
        builder.Property(log => log.EntityType).HasMaxLength(50).IsRequired();
        builder.Property(log => log.EntityId).HasMaxLength(50).IsRequired();
        builder.Property(log => log.Action).HasMaxLength(50).IsRequired();
        builder.Property(log => log.CreatedAt).HasConversion(UtcDateTimeConverters.Required);

        builder.HasIndex(log => new { log.EntityType, log.EntityId, log.Id });
        builder.HasIndex(log => new { log.UserId, log.CreatedAt });
    }
}
