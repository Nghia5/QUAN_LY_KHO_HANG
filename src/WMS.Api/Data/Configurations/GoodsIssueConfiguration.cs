using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WMS.Api.Models.Documents;

namespace WMS.Api.Data.Configurations;

public sealed class GoodsIssueConfiguration : IEntityTypeConfiguration<GoodsIssue>
{
    public void Configure(EntityTypeBuilder<GoodsIssue> builder)
    {
        builder.ConfigureDocument("GoodsIssues");

        builder.Property(issue => issue.CustomerName).HasMaxLength(200);
        builder.Property(issue => issue.ReferenceNo).HasMaxLength(50);
        builder.HasIndex(issue => issue.CustomerId);
        builder.HasIndex(issue => issue.IssueDate);

        builder.HasMany(issue => issue.Lines)
            .WithOne()
            .HasForeignKey(line => new { line.GoodsIssueId, line.WarehouseId })
            .HasPrincipalKey(issue => new { issue.Id, issue.WarehouseId })
            .OnDelete(DeleteBehavior.Cascade); // only Draft issues can be deleted
    }
}

public sealed class GoodsIssueLineConfiguration : IEntityTypeConfiguration<GoodsIssueLine>
{
    public void Configure(EntityTypeBuilder<GoodsIssueLine> builder)
    {
        builder.ToTable("GoodsIssueLines", table =>
            table.HasCheckConstraint("CK_GoodsIssueLines_Quantity_Positive", "[Quantity] > 0"));

        builder.HasKey(line => line.Id);

        builder.Property(line => line.Quantity)
            .HasPrecision(InventoryConfiguration.QuantityPrecision, InventoryConfiguration.QuantityScale);
        builder.Property(line => line.Note).HasMaxLength(500);

        builder.HasIndex(line => line.ProductId);

        // A chosen lot must be a lot of the line's product.
        builder.HasOne(line => line.Batch)
            .WithMany()
            .HasForeignKey(line => new { line.BatchId, line.ProductId })
            .HasPrincipalKey(batch => new { batch.Id, batch.ProductId })
            .OnDelete(DeleteBehavior.Restrict);

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
