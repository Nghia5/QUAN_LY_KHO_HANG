using Microsoft.EntityFrameworkCore;
using WMS.Api.Data;
using WMS.Api.Models;

namespace WMS.Api.Repositories;

public interface ISupplierRepository
{
    Task<List<SupplierModel>> GetAllAsync(string? keyword, bool? isActive, CancellationToken cancellationToken);
    Task<SupplierModel?> GetByIdAsync(int id, CancellationToken cancellationToken);
    Task<bool> CodeExistsAsync(string code, int? excludeId, CancellationToken cancellationToken);
    Task AddAsync(SupplierModel supplier, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public class SupplierRepository(WmsDbContext dbContext) : ISupplierRepository
{
    private DbSet<SupplierModel> Suppliers => dbContext.Set<SupplierModel>();

    public async Task<List<SupplierModel>> GetAllAsync(
        string? keyword,
        bool? isActive,
        CancellationToken cancellationToken)
    {
        var query = Suppliers.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var searchTerm = keyword.Trim();
            query = query.Where(supplier =>
                supplier.Code.Contains(searchTerm) ||
                supplier.Name.Contains(searchTerm) ||
                (supplier.Phone != null && supplier.Phone.Contains(searchTerm)) ||
                (supplier.Email != null && supplier.Email.Contains(searchTerm)));
        }

        if (isActive.HasValue)
        {
            query = query.Where(supplier => supplier.IsActive == isActive.Value);
        }

        return await query.OrderBy(supplier => supplier.Name).ToListAsync(cancellationToken);
    }

    public Task<SupplierModel?> GetByIdAsync(int id, CancellationToken cancellationToken) =>
        Suppliers.FirstOrDefaultAsync(supplier => supplier.Id == id, cancellationToken);

    public Task<bool> CodeExistsAsync(
        string code,
        int? excludeId,
        CancellationToken cancellationToken) =>
        Suppliers.AnyAsync(
            supplier => supplier.Code == code && (excludeId == null || supplier.Id != excludeId),
            cancellationToken);

    public async Task AddAsync(SupplierModel supplier, CancellationToken cancellationToken) =>
        await Suppliers.AddAsync(supplier, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
