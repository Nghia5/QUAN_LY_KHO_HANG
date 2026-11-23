using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WMS.Api.Models.Documents;

namespace WMS.Api.Data.Configurations;

public sealed class StocktakeConfiguration : IEntityTypeConfiguration<Stocktake>
{
    public void Configure(EntityTypeBuilder<Stocktake> builder)
    {
        builder.ConfigureDocument("Stocktakes");

        builder.PrimitiveCollection(stocktake => stocktake.ScopeLocationIds).HasMaxLength(2000);
        builder.Property(stocktake => stocktake.SnapshotAt).HasConversion(UtcDateTimeConverters.Optional);
        builder.Property(stocktake => stocktake.AdjustedAt).HasConversion(UtcDateTimeConverters.Optional);

        builder.HasMany(stocktake => stocktake.Lines)
            .WithOne()
            .HasForeignKey(line => new { line.StocktakeId, line.WarehouseId })
            .HasPrincipalKey(stocktake => new { stocktake.Id, stocktake.WarehouseId })
            .OnDelete(DeleteBehavior.Cascade); // only Draft stocktakes (no lines yet) can be deleted
    }
}

public sealed class StocktakeLineConfiguration : IEntityTypeConfiguration<StocktakeLine>
{
    public void Configure(EntityTypeBuilder<StocktakeLine> builder)
    {
        builder.ToTable("StocktakeLines", table =>
        {
            table.HasCheckConstraint("CK_StocktakeLines_System_NonNegative", "[SystemQuantity] >= 0");
            table.HasCheckConstraint("CK_StocktakeLines_Counted_NonNegative", "[CountedQuantity] IS NULL OR [CountedQuantity] >= 0");
        });

        builder.HasKey(line => line.Id);

        var precision = InventoryConfiguration.QuantityPrecision;
        var scale = InventoryConfiguration.QuantityScale;
        builder.Property(line => line.SystemQuantity).HasPrecision(precision, scale);
        builder.Property(line => line.CountedQuantity).HasPrecision(precision, scale);
        builder.Property(line => line.Difference)
            .HasPrecision(precision, scale)
            .HasComputedColumnSql("[CountedQuantity] - [SystemQuantity]", stored: true);
        builder.Property(line => line.CountedAt).HasConversion(UtcDateTimeConverters.Optional);
        builder.Property(line => line.Note).HasMaxLength(500);

        // One line per product and bin in a stocktake; also used by the location lock lookup.
        builder.HasIndex(line => new { line.StocktakeId, line.ProductId, line.LocationId }).IsUnique();
        builder.HasIndex(line => line.LocationId);

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
