using Microsoft.EntityFrameworkCore;
using WMS.Api.Models;

namespace WMS.Api.Data;

public class WmsDbContext(DbContextOptions<WmsDbContext> options) : DbContext(options)
{
    public DbSet<Product> Products => Set<Product>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(WmsDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
