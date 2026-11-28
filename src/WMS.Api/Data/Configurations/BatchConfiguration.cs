using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WMS.Api.Models;

namespace WMS.Api.Data.Configurations;

public sealed class BatchConfiguration : IEntityTypeConfiguration<Batch>
{
    public void Configure(EntityTypeBuilder<Batch> builder)
    {
        builder.ToTable("Batches", table => table.HasCheckConstraint(
            "CK_Batches_Expiry_After_Manufacture",
            "[ExpiryDate] IS NULL OR [ManufactureDate] IS NULL OR [ExpiryDate] > [ManufactureDate]"));

        builder.HasKey(batch => batch.Id);

        // Target of the lot's composite FK: a lot's batch must be of the lot's product.
        builder.HasAlternateKey(batch => new { batch.Id, batch.ProductId });

        builder.Property(batch => batch.BatchCode).HasMaxLength(50).IsRequired();
        builder.HasIndex(batch => new { batch.ProductId, batch.BatchCode }).IsUnique();
        builder.HasIndex(batch => batch.ExpiryDate);

        builder.Property(batch => batch.Note).HasMaxLength(500);
        builder.Property(batch => batch.CreatedAt)
            .HasDefaultValueSql("SYSUTCDATETIME()")
            .HasConversion(UtcDateTimeConverters.Required);
        builder.Property(batch => batch.UpdatedAt).HasConversion(UtcDateTimeConverters.Optional);

        builder.HasOne(batch => batch.Product)
            .WithMany()
            .HasForeignKey(batch => batch.ProductId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class InventoryLotConfiguration : IEntityTypeConfiguration<InventoryLot>
{
    public void Configure(EntityTypeBuilder<InventoryLot> builder)
    {
        builder.ToTable("InventoryLots", table =>
            table.HasCheckConstraint("CK_InventoryLots_Quantity_NonNegative", "[Quantity] >= 0"));

        builder.HasKey(lot => lot.Id);

        builder.Property(lot => lot.Quantity)
            .HasPrecision(InventoryConfiguration.QuantityPrecision, InventoryConfiguration.QuantityScale);
        builder.Property(lot => lot.UpdatedAt).HasConversion(UtcDateTimeConverters.Optional);

        builder.HasIndex(lot => new { lot.InventoryId, lot.BatchId }).IsUnique();
        builder.HasIndex(lot => lot.BatchId).IncludeProperties(lot => lot.Quantity);

        builder.HasOne(lot => lot.Inventory)
            .WithMany()
            .HasForeignKey(lot => new { lot.InventoryId, lot.ProductId })
            .HasPrincipalKey(inventory => new { inventory.Id, inventory.ProductId })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(lot => lot.Batch)
            .WithMany(batch => batch.Lots)
            .HasForeignKey(lot => new { lot.BatchId, lot.ProductId })
            .HasPrincipalKey(batch => new { batch.Id, batch.ProductId })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
