using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WMS.Api.Models.Documents;

namespace WMS.Api.Data.Configurations;

public sealed class GoodsReceiptConfiguration : IEntityTypeConfiguration<GoodsReceipt>
{
    public void Configure(EntityTypeBuilder<GoodsReceipt> builder)
    {
        builder.ConfigureDocument("GoodsReceipts");

        builder.Property(receipt => receipt.SupplierName).HasMaxLength(200);
        builder.Property(receipt => receipt.ReferenceNo).HasMaxLength(50);
        builder.HasIndex(receipt => receipt.SupplierId);
        builder.HasIndex(receipt => receipt.ReceiptDate);

        builder.HasMany(receipt => receipt.Lines)
            .WithOne()
            .HasForeignKey(line => new { line.GoodsReceiptId, line.WarehouseId })
            .HasPrincipalKey(receipt => new { receipt.Id, receipt.WarehouseId })
            .OnDelete(DeleteBehavior.Cascade); // only Draft receipts can be deleted
    }
}

public sealed class GoodsReceiptLineConfiguration : IEntityTypeConfiguration<GoodsReceiptLine>
{
    public void Configure(EntityTypeBuilder<GoodsReceiptLine> builder)
    {
        builder.ToTable("GoodsReceiptLines", table =>
        {
            table.HasCheckConstraint("CK_GoodsReceiptLines_Quantity_Positive", "[Quantity] > 0");
            table.HasCheckConstraint("CK_GoodsReceiptLines_UnitCost_NonNegative", "[UnitCost] IS NULL OR [UnitCost] >= 0");
            table.HasCheckConstraint(
                "CK_GoodsReceiptLines_Expiry_After_Manufacture",
                "[ExpiryDate] IS NULL OR [ManufactureDate] IS NULL OR [ExpiryDate] > [ManufactureDate]");
        });

        builder.HasKey(line => line.Id);

        builder.Property(line => line.Quantity)
            .HasPrecision(InventoryConfiguration.QuantityPrecision, InventoryConfiguration.QuantityScale);
        builder.Property(line => line.UnitCost).HasPrecision(18, 2);
        builder.Property(line => line.BatchCode).HasMaxLength(50);
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
