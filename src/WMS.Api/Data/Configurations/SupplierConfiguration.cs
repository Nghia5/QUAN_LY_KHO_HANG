using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WMS.Api.Models;

namespace WMS.Api.Data.Configurations;

public class SupplierConfiguration : IEntityTypeConfiguration<SupplierModel>
{
    public void Configure(EntityTypeBuilder<SupplierModel> builder)
    {
        builder.ToTable("Suppliers");
        builder.HasKey(supplier => supplier.Id);
        builder.Property(supplier => supplier.Code).IsRequired().HasMaxLength(30);
        builder.Property(supplier => supplier.Name).IsRequired().HasMaxLength(200);
        builder.Property(supplier => supplier.Phone).HasMaxLength(20);
        builder.Property(supplier => supplier.Email).HasMaxLength(150);
        builder.Property(supplier => supplier.Address).HasMaxLength(300);
        builder.HasIndex(supplier => supplier.Code).IsUnique();
    }
}
