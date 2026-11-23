using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WMS.Api.Models;

namespace WMS.Api.Data.Configurations;

public sealed class InventoryConfiguration : IEntityTypeConfiguration<Inventory>
{
    /// <summary>Quantity precision shared by inventory, movements and document lines.</summary>
    public const int QuantityPrecision = 18;
    public const int QuantityScale = 3;

    public void Configure(EntityTypeBuilder<Inventory> builder)
    {
        builder.ToTable("Inventories", table =>
        {
            table.HasCheckConstraint("CK_Inventories_Quantity_NonNegative", "[Quantity] >= 0");
            table.HasCheckConstraint("CK_Inventories_Reserved_NonNegative", "[ReservedQuantity] >= 0");
            table.HasCheckConstraint("CK_Inventories_Reserved_LTE_Quantity", "[ReservedQuantity] <= [Quantity]");
            table.HasCheckConstraint("CK_Inventories_Available_NonNegative", "[AvailableQuantity] >= 0");
            table.HasCheckConstraint("CK_Inventories_MinQuantity_NonNegative", "[MinQuantity] >= 0");
        });

        builder.HasKey(inventory => inventory.Id);

        // Target of InventoryLot's composite FK (lot product = inventory product).
        builder.HasAlternateKey(inventory => new { inventory.Id, inventory.ProductId });

        builder.Property(inventory => inventory.Quantity)
            .HasPrecision(QuantityPrecision, QuantityScale)
            .HasDefaultValue(0m);

        builder.Property(inventory => inventory.ReservedQuantity)
            .HasPrecision(QuantityPrecision, QuantityScale)
            .HasDefaultValue(0m);

        builder.Property(inventory => inventory.AvailableQuantity)
            .HasPrecision(QuantityPrecision, QuantityScale)
            .HasComputedColumnSql("[Quantity] - [ReservedQuantity]", stored: true);

        builder.Property(inventory => inventory.MinQuantity)
            .HasPrecision(QuantityPrecision, QuantityScale)
            .HasDefaultValue(0m);

        builder.Property(inventory => inventory.RowVersion)
            .IsRowVersion();

        builder.Property(inventory => inventory.CreatedAt)
            .HasDefaultValueSql("SYSUTCDATETIME()")
            .HasConversion(UtcDateTimeConverters.Required);

        builder.Property(inventory => inventory.UpdatedAt)
            .HasConversion(UtcDateTimeConverters.Optional);

        // One inventory row per Product/Warehouse/Location.
        builder.HasIndex(inventory => new { inventory.ProductId, inventory.WarehouseId, inventory.LocationId })
            .IsUnique();

        // Listing/filtering by warehouse and by location (W-05).
        builder.HasIndex(inventory => new { inventory.WarehouseId, inventory.ProductId })
            .IncludeProperties(inventory => new { inventory.Quantity, inventory.ReservedQuantity, inventory.AvailableQuantity });
        builder.HasIndex(inventory => new { inventory.LocationId, inventory.WarehouseId });

        builder.HasOne(inventory => inventory.Product)
            .WithMany()
            .HasForeignKey(inventory => inventory.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(inventory => inventory.Warehouse)
            .WithMany()
            .HasForeignKey(inventory => inventory.WarehouseId)
            .OnDelete(DeleteBehavior.Restrict);

        // Composite FK: the location must belong to the same warehouse as the inventory row.
        builder.HasOne(inventory => inventory.Location)
            .WithMany()
            .HasForeignKey(inventory => new { inventory.LocationId, inventory.WarehouseId })
            .HasPrincipalKey(location => new { location.Id, location.WarehouseId })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
