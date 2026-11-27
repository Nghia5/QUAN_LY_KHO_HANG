using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WMS.Api.Models.Documents;

namespace WMS.Api.Data.Configurations;

public sealed class StockAdjustmentConfiguration : IEntityTypeConfiguration<StockAdjustment>
{
    public void Configure(EntityTypeBuilder<StockAdjustment> builder)
    {
        builder.ConfigureDocument("StockAdjustments", table =>
            table.HasCheckConstraint("CK_StockAdjustments_Reason_Required", "LEN(LTRIM(RTRIM([Reason]))) >= 5"));

        builder.Property(adjustment => adjustment.ReasonCode).HasConversion<string>().HasMaxLength(20);
        builder.Property(adjustment => adjustment.Reason).HasMaxLength(500).IsRequired();

        builder.HasMany(adjustment => adjustment.Lines)
            .WithOne()
            .HasForeignKey(line => new { line.StockAdjustmentId, line.WarehouseId })
            .HasPrincipalKey(adjustment => new { adjustment.Id, adjustment.WarehouseId })
            .OnDelete(DeleteBehavior.Cascade); // only Draft adjustments can be deleted
    }
}

public sealed class StockAdjustmentLineConfiguration : IEntityTypeConfiguration<StockAdjustmentLine>
{
    public void Configure(EntityTypeBuilder<StockAdjustmentLine> builder)
    {
        builder.ToTable("StockAdjustmentLines", table =>
        {
            table.HasCheckConstraint("CK_StockAdjustmentLines_Quantity_NonZero", "[AdjustmentQuantity] <> 0");
            table.HasCheckConstraint(
                "CK_StockAdjustmentLines_Balance",
                "([BeforeQuantity] IS NULL AND [AfterQuantity] IS NULL) OR " +
                "([BeforeQuantity] >= 0 AND [AfterQuantity] >= 0 AND [AfterQuantity] = [BeforeQuantity] + [AdjustmentQuantity])");
        });

        builder.HasKey(line => line.Id);

        var precision = InventoryConfiguration.QuantityPrecision;
        var scale = InventoryConfiguration.QuantityScale;
        builder.Property(line => line.AdjustmentQuantity).HasPrecision(precision, scale);
        builder.Property(line => line.BeforeQuantity).HasPrecision(precision, scale);
        builder.Property(line => line.AfterQuantity).HasPrecision(precision, scale);
        builder.Property(line => line.Note).HasMaxLength(500);

        builder.HasIndex(line => line.ProductId);

        builder.HasOne(line => line.Product)
            .WithMany()
            .HasForeignKey(line => line.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(line => line.Location)
            .WithMany()
            .HasForeignKey(line => new { line.LocationId, line.WarehouseId })
            .HasPrincipalKey(location => new { location.Id, location.WarehouseId })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
