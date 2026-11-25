using Microsoft.EntityFrameworkCore;
using WMS.Api.Models;

namespace WMS.Api.Data;

public class WmsDbContext(DbContextOptions<WmsDbContext> options) : DbContext(options)
{
    public DbSet<Product> Products => Set<Product>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<UploadedFile> UploadedFiles => Set<UploadedFile>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<WarehouseRoleAssignment>(assignment =>
        {
            assignment.ToTable("WarehouseRoleAssignments");
            assignment.HasKey(a => new { a.UserId, a.WarehouseId, a.RoleId });
            assignment.HasOne(a => a.User).WithMany().HasForeignKey(a => a.UserId);
            assignment.HasOne(a => a.Warehouse).WithMany().HasForeignKey(a => a.WarehouseId);
            assignment.HasOne(a => a.Role).WithMany().HasForeignKey(a => a.RoleId);
        });

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(WmsDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
