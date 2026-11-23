using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WMS.Api.Models;

namespace WMS.Api.Data.Configurations;

public sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.HasKey(product => product.Id);

        builder.Property(product => product.Sku)
            .HasMaxLength(50)
            .IsRequired();

        builder.HasIndex(product => product.Sku)
            .IsUnique();

        builder.Property(product => product.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(product => product.Description)
            .HasMaxLength(1000);

        builder.Property(product => product.Category)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(product => product.UnitOfMeasure)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(product => product.SalePrice)
            .HasPrecision(18, 2);

        builder.Property(product => product.IsActive)
            .HasDefaultValue(true);
    }
}
