using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WMS.Api.Models;

namespace WMS.Api.Data.Configurations;

public sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("RefreshTokens", table =>
        {
            table.HasCheckConstraint("CK_RefreshTokens_ExpiresAfterCreation", "[ExpiresAt] > [CreatedAt]");
            table.HasCheckConstraint("CK_RefreshTokens_RevokedAfterCreation",
                "[RevokedAt] IS NULL OR [RevokedAt] >= [CreatedAt]");
        });

        builder.HasKey(token => token.Id);

        builder.Property(token => token.Token)
            .HasMaxLength(256)
            .IsRequired();

        builder.HasIndex(token => token.Token);

        builder.Property(token => token.CreatedByIp)
            .HasMaxLength(50);

        builder.Property(token => token.RevokedByIp)
            .HasMaxLength(50);

        builder.Property(token => token.ReplacedByToken)
            .HasMaxLength(256);

        builder.Property(token => token.CreatedAt)
            .HasDefaultValueSql("SYSUTCDATETIME()")
            .HasConversion(UtcDateTimeConverters.Required);

        builder.Property(token => token.ExpiresAt)
            .HasConversion(UtcDateTimeConverters.Required);

        builder.Property(token => token.RevokedAt)
            .HasConversion(UtcDateTimeConverters.Optional);
    }
}
