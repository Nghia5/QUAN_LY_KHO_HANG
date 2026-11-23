using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WMS.Api.Models.Documents;

namespace WMS.Api.Data.Configurations;

public sealed class StockTransferConfiguration : IEntityTypeConfiguration<StockTransfer>
{
    public void Configure(EntityTypeBuilder<StockTransfer> builder)
    {
        builder.ConfigureDocument("StockTransfers");

        builder.Property(transfer => transfer.Reason).HasMaxLength(500);
        builder.Property(transfer => transfer.PickedAt).HasConversion(UtcDateTimeConverters.Optional);
        builder.Property(transfer => transfer.ShippedAt).HasConversion(UtcDateTimeConverters.Optional);
        builder.Property(transfer => transfer.ReceivedAt).HasConversion(UtcDateTimeConverters.Optional);

        builder.HasIndex(transfer => new { transfer.DestinationWarehouseId, transfer.Status });

        // Lines reference (Id, source, destination): both warehouses of a line always equal the header's.
        builder.HasAlternateKey(transfer => new { transfer.Id, transfer.WarehouseId, transfer.DestinationWarehouseId });

        builder.HasOne(transfer => transfer.DestinationWarehouse)
            .WithMany()
            .HasForeignKey(transfer => transfer.DestinationWarehouseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(transfer => transfer.Lines)
            .WithOne()
            .HasForeignKey(line => new { line.StockTransferId, line.WarehouseId, line.DestinationWarehouseId })
            .HasPrincipalKey(transfer => new { transfer.Id, transfer.WarehouseId, transfer.DestinationWarehouseId })
            .OnDelete(DeleteBehavior.Cascade); // only Draft transfers can be deleted
    }
}

public sealed class StockTransferLineConfiguration : IEntityTypeConfiguration<StockTransferLine>
{
    public void Configure(EntityTypeBuilder<StockTransferLine> builder)
    {
        builder.ToTable("StockTransferLines", table =>
        {
            table.HasCheckConstraint("CK_StockTransferLines_Quantity_Positive", "[Quantity] > 0");
            table.HasCheckConstraint("CK_StockTransferLines_DifferentLocations", "[FromLocationId] <> [ToLocationId]");
        });

        builder.HasKey(line => line.Id);

        builder.Property(line => line.Quantity)
            .HasPrecision(InventoryConfiguration.QuantityPrecision, InventoryConfiguration.QuantityScale);
        builder.Property(line => line.Note).HasMaxLength(500);

        builder.HasIndex(line => line.ProductId);

        builder.OwnsMany(line => line.ShippedLots, lots =>
        {
            lots.ToJson();
            lots.Property(lot => lot.Quantity)
                .HasPrecision(InventoryConfiguration.QuantityPrecision, InventoryConfiguration.QuantityScale);
        });

        builder.HasOne(line => line.Product)
            .WithMany()
            .HasForeignKey(line => line.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        // Source bin must be in the source warehouse, destination bin in the destination warehouse.
        builder.HasOne(line => line.FromLocation)
            .WithMany()
            .HasForeignKey(line => new { line.FromLocationId, line.WarehouseId })
            .HasPrincipalKey(location => new { location.Id, location.WarehouseId })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(line => line.ToLocation)
            .WithMany()
            .HasForeignKey(line => new { line.ToLocationId, line.DestinationWarehouseId })
            .HasPrincipalKey(location => new { location.Id, location.WarehouseId })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
