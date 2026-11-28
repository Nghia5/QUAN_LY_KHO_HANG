using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WMS.Api.Models;

namespace WMS.Api.Data.Configurations;

public sealed class PermissionConfiguration : IEntityTypeConfiguration<Permission>
{
    public void Configure(EntityTypeBuilder<Permission> builder)
    {
        builder.ToTable("Permissions");

        builder.HasKey(permission => permission.Id);

        builder.Property(permission => permission.Code)
            .HasMaxLength(100)
            .IsRequired();

        builder.HasIndex(permission => permission.Code)
            .IsUnique();

        builder.Property(permission => permission.Group)
            .HasMaxLength(50)
            .IsRequired();

        builder.HasIndex(permission => permission.Group);

        builder.Property(permission => permission.Description)
            .HasMaxLength(250);

        builder.HasMany(permission => permission.RolePermissions)
            .WithOne(rolePermission => rolePermission.Permission)
            .HasForeignKey(rolePermission => rolePermission.PermissionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
