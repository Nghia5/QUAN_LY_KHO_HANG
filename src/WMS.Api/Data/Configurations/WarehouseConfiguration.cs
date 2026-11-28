using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WMS.Api.Models;

namespace WMS.Api.Data.Configurations;

public sealed class WarehouseConfiguration : IEntityTypeConfiguration<Warehouse>
{
    public void Configure(EntityTypeBuilder<Warehouse> builder)
    {
        builder.ToTable("Warehouses");

        builder.HasKey(warehouse => warehouse.Id);

        builder.Property(warehouse => warehouse.Code)
            .HasMaxLength(20)
            .IsRequired();

        builder.HasIndex(warehouse => warehouse.Code)
            .IsUnique();

        builder.Property(warehouse => warehouse.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.HasIndex(warehouse => warehouse.Name);

        builder.Property(warehouse => warehouse.Address)
            .HasMaxLength(500);

        builder.Property(warehouse => warehouse.Phone)
            .HasMaxLength(20);

        builder.Property(warehouse => warehouse.Description)
            .HasMaxLength(1000);

        builder.HasIndex(warehouse => warehouse.ManagerId);

        builder.Property(warehouse => warehouse.IsActive)
            .HasDefaultValue(true);

        builder.Property(warehouse => warehouse.CreatedAt)
            .HasDefaultValueSql("SYSUTCDATETIME()")
            .HasConversion(UtcDateTimeConverters.Required);

        builder.Property(warehouse => warehouse.UpdatedAt)
            .HasConversion(UtcDateTimeConverters.Optional);
    }
}
