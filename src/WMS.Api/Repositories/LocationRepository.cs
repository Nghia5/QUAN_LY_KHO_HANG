using System.Linq.Expressions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using WMS.Api.Common;
using WMS.Api.Data;
using WMS.Api.Dtos.Common;
using WMS.Api.Dtos.Locations;
using WMS.Api.Exceptions;
using WMS.Api.Models;

namespace WMS.Api.Repositories;

public sealed class LocationRepository(WmsDbContext dbContext) : ILocationRepository
{
    private static readonly IReadOnlyDictionary<string, Expression<Func<WarehouseLocation, object?>>> SortMap =
        QueryableExtensions.SortMap<WarehouseLocation>(
            ("code", location => location.Code),
            ("name", location => location.Name),
            ("type", location => location.Type),
            ("createdAt", location => location.CreatedAt));

    private IQueryable<WarehouseLocation> Locations => dbContext.Set<WarehouseLocation>();

    public async Task<PagedResult<LocationResponse>> SearchAsync(
        int warehouseId,
        LocationQuery query,
        CancellationToken cancellationToken)
    {
        var locations = Locations.AsNoTracking().Where(location => location.WarehouseId == warehouseId);

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var keyword = query.Keyword.Trim();
            locations = locations.Where(location => location.Code.Contains(keyword) || location.Name.Contains(keyword));
        }

        if (query.Type is not null)
        {
            locations = locations.Where(location => location.Type == query.Type);
        }

        if (query.ParentId is not null)
        {
            locations = locations.Where(location => location.ParentId == query.ParentId);
        }

        if (query.IsActive is not null)
        {
            locations = locations.Where(location => location.IsActive == query.IsActive);
        }

        return await locations
            .ApplySort(query, SortMap, "code", location => location.Id)
            .Select(ToResponseExpression)
            .ToPagedResultAsync(query, cancellationToken);
    }

    public async Task<IReadOnlyList<LocationResponse>> ListAllAsync(int warehouseId, CancellationToken cancellationToken) =>
        await Locations.AsNoTracking()
            .Where(location => location.WarehouseId == warehouseId)
            .OrderBy(location => location.Code)
            .Select(ToResponseExpression)
            .ToListAsync(cancellationToken);

    public Task<LocationResponse?> GetResponseAsync(int warehouseId, int id, CancellationToken cancellationToken) =>
        Locations.AsNoTracking()
            .Where(location => location.Id == id && location.WarehouseId == warehouseId)
            .Select(ToResponseExpression)
            .SingleOrDefaultAsync(cancellationToken);

    public Task<WarehouseLocation?> FindAsync(int warehouseId, int id, CancellationToken cancellationToken) =>
        dbContext.Set<WarehouseLocation>()
            .SingleOrDefaultAsync(location => location.Id == id && location.WarehouseId == warehouseId, cancellationToken);

    public Task<bool> CodeExistsAsync(int warehouseId, string code, CancellationToken cancellationToken) =>
        Locations.AnyAsync(location => location.WarehouseId == warehouseId && location.Code == code, cancellationToken);

    public Task<int> CountChildrenAsync(int locationId, bool activeOnly, CancellationToken cancellationToken) =>
        Locations.CountAsync(
            location => location.ParentId == locationId && (!activeOnly || location.IsActive),
            cancellationToken);

    public Task<int> CountByWarehouseAsync(int warehouseId, CancellationToken cancellationToken) =>
        Locations.CountAsync(location => location.WarehouseId == warehouseId, cancellationToken);

    public void Add(WarehouseLocation location) => dbContext.Set<WarehouseLocation>().Add(location);

    public void Remove(WarehouseLocation location) => dbContext.Set<WarehouseLocation>().Remove(location);

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (exception.GetBaseException() is SqlException { Number: 2601 or 2627 })
        {
            throw new ConflictException("A location with this code already exists in the warehouse.");
        }
        catch (DbUpdateException exception)
            when (exception.GetBaseException() is SqlException { Number: 547 })
        {
            throw new ConflictException(
                "The location violates a warehouse constraint or is still referenced by other data.");
        }
    }

    private static readonly Expression<Func<WarehouseLocation, LocationResponse>> ToResponseExpression =
        location => new LocationResponse(
            location.Id,
            location.WarehouseId,
            location.ParentId,
            location.Type,
            location.SegmentCode,
            location.Code,
            location.Name,
            location.Description,
            location.IsActive,
            location.CreatedAt,
            location.UpdatedAt);

    internal static LocationResponse ToResponse(WarehouseLocation location) =>
        ToResponseCompiled(location);

    private static readonly Func<WarehouseLocation, LocationResponse> ToResponseCompiled = ToResponseExpression.Compile();
}
