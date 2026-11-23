using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WMS.Api.Models;

namespace WMS.Api.Data.Configurations;

public sealed class WarehouseLocationConfiguration : IEntityTypeConfiguration<WarehouseLocation>
{
    public void Configure(EntityTypeBuilder<WarehouseLocation> builder)
    {
        builder.ToTable("WarehouseLocations", table =>
        {
            table.HasCheckConstraint("CK_WarehouseLocations_Type", "[Type] IN ('Zone', 'Rack', 'Shelf', 'Bin')");
            // Zones are roots; every other level must have a parent.
            table.HasCheckConstraint(
                "CK_WarehouseLocations_Parent",
                "([Type] = 'Zone' AND [ParentId] IS NULL) OR ([Type] <> 'Zone' AND [ParentId] IS NOT NULL)");
        });

        builder.HasKey(location => location.Id);

        // (Id, WarehouseId) is the target of composite FKs that force children
        // (and inventory rows) to belong to the same warehouse as the location.
        builder.HasAlternateKey(location => new { location.Id, location.WarehouseId });

        builder.Property(location => location.Type)
            .HasConversion<string>()
            .HasMaxLength(10);

        builder.Property(location => location.SegmentCode)
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(location => location.Code)
            .HasMaxLength(50)
            .IsRequired();

        builder.HasIndex(location => new { location.WarehouseId, location.Code })
            .IsUnique();

        builder.HasIndex(location => new { location.WarehouseId, location.Type, location.IsActive });

        builder.Property(location => location.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(location => location.Description)
            .HasMaxLength(500);

        builder.Property(location => location.IsActive)
            .HasDefaultValue(true);

        builder.Property(location => location.CreatedAt)
            .HasDefaultValueSql("SYSUTCDATETIME()")
            .HasConversion(UtcDateTimeConverters.Required);

        builder.Property(location => location.UpdatedAt)
            .HasConversion(UtcDateTimeConverters.Optional);

        builder.HasOne(location => location.Warehouse)
            .WithMany()
            .HasForeignKey(location => location.WarehouseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(location => location.Parent)
            .WithMany(location => location.Children)
            .HasForeignKey(location => new { location.ParentId, location.WarehouseId })
            .HasPrincipalKey(location => new { location.Id, location.WarehouseId })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
