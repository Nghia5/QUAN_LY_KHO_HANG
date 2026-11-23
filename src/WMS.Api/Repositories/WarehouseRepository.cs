using System.Linq.Expressions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using WMS.Api.Common;
using WMS.Api.Data;
using WMS.Api.Dtos.Common;
using WMS.Api.Dtos.Warehouses;
using WMS.Api.Exceptions;
using WMS.Api.Models;

namespace WMS.Api.Repositories;

public sealed class WarehouseRepository(WmsDbContext dbContext) : IWarehouseRepository
{
    private static readonly IReadOnlyDictionary<string, Expression<Func<Warehouse, object?>>> SortMap =
        QueryableExtensions.SortMap<Warehouse>(
            ("code", warehouse => warehouse.Code),
            ("name", warehouse => warehouse.Name),
            ("createdAt", warehouse => warehouse.CreatedAt),
            ("isActive", warehouse => warehouse.IsActive));

    public async Task<PagedResult<WarehouseResponse>> SearchAsync(
        WarehouseQuery query,
        CancellationToken cancellationToken)
    {
        var warehouses = dbContext.Set<Warehouse>().AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var keyword = query.Keyword.Trim();
            warehouses = warehouses.Where(warehouse =>
                warehouse.Code.Contains(keyword)
                || warehouse.Name.Contains(keyword)
                || (warehouse.Address != null && warehouse.Address.Contains(keyword)));
        }

        if (query.IsActive is not null)
        {
            warehouses = warehouses.Where(warehouse => warehouse.IsActive == query.IsActive);
        }

        if (query.ManagerId is not null)
        {
            warehouses = warehouses.Where(warehouse => warehouse.ManagerId == query.ManagerId);
        }

        return await warehouses
            .ApplySort(query, SortMap, "code", warehouse => warehouse.Id)
            .Select(warehouse => ToResponse(warehouse))
            .ToPagedResultAsync(query, cancellationToken);
    }

    public Task<WarehouseResponse?> GetResponseByIdAsync(int id, CancellationToken cancellationToken) =>
        dbContext.Set<Warehouse>()
            .AsNoTracking()
            .Where(warehouse => warehouse.Id == id)
            .Select(warehouse => ToResponse(warehouse))
            .SingleOrDefaultAsync(cancellationToken);

    public Task<Warehouse?> FindAsync(int id, CancellationToken cancellationToken) =>
        dbContext.Set<Warehouse>().SingleOrDefaultAsync(warehouse => warehouse.Id == id, cancellationToken);

    public Task<bool> CodeExistsAsync(string code, CancellationToken cancellationToken) =>
        dbContext.Set<Warehouse>().AnyAsync(warehouse => warehouse.Code == code, cancellationToken);

    public void Add(Warehouse warehouse) => dbContext.Set<Warehouse>().Add(warehouse);

    public void Remove(Warehouse warehouse) => dbContext.Set<Warehouse>().Remove(warehouse);

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (exception.GetBaseException() is SqlException { Number: 2601 or 2627 })
        {
            throw new ConflictException("A warehouse with this code already exists.");
        }
        catch (DbUpdateException exception)
            when (exception.GetBaseException() is SqlException { Number: 547 })
        {
            // FK violation: another table still references this warehouse (last line of defence).
            throw new ConflictException("The warehouse is still referenced by other data. Deactivate it instead.");
        }
    }

    internal static WarehouseResponse ToResponse(Warehouse warehouse) =>
        new(
            warehouse.Id,
            warehouse.Code,
            warehouse.Name,
            warehouse.Address,
            warehouse.Phone,
            warehouse.Description,
            warehouse.ManagerId,
            warehouse.IsActive,
            warehouse.CreatedAt,
            warehouse.UpdatedAt);
}
