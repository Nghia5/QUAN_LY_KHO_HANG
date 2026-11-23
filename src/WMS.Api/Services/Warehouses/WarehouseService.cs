using WMS.Api.Common;
using WMS.Api.Dtos.Common;
using WMS.Api.Dtos.Warehouses;
using WMS.Api.Exceptions;
using WMS.Api.Models;
using WMS.Api.Repositories;
using WMS.Api.Validators;

namespace WMS.Api.Services.Warehouses;

public interface IWarehouseService
{
    Task<PagedResult<WarehouseResponse>> SearchAsync(WarehouseQuery query, CancellationToken cancellationToken);

    Task<WarehouseResponse> GetByIdAsync(int id, CancellationToken cancellationToken);

    Task<WarehouseResponse> CreateAsync(CreateWarehouseRequest request, CancellationToken cancellationToken);

    Task<WarehouseResponse> UpdateAsync(int id, UpdateWarehouseRequest request, CancellationToken cancellationToken);

    Task<WarehouseResponse> ActivateAsync(int id, CancellationToken cancellationToken);

    Task<WarehouseResponse> DeactivateAsync(int id, CancellationToken cancellationToken);

    /// <summary>Hard delete; only allowed when nothing references the warehouse.</summary>
    Task DeleteAsync(int id, CancellationToken cancellationToken);
}

public sealed class WarehouseService(
    IWarehouseRepository repository,
    IEnumerable<IWarehouseDependencyChecker> dependencyCheckers,
    ICurrentUser currentUser,
    TimeProvider timeProvider) : IWarehouseService
{
    public Task<PagedResult<WarehouseResponse>> SearchAsync(WarehouseQuery query, CancellationToken cancellationToken) =>
        repository.SearchAsync(query, cancellationToken);

    public async Task<WarehouseResponse> GetByIdAsync(int id, CancellationToken cancellationToken) =>
        await repository.GetResponseByIdAsync(id, cancellationToken)
        ?? throw new NotFoundException("Warehouse", id);

    public async Task<WarehouseResponse> CreateAsync(CreateWarehouseRequest request, CancellationToken cancellationToken)
    {
        var code = CommonRules.NormalizeCode(request.Code);
        if (await repository.CodeExistsAsync(code, cancellationToken))
        {
            throw new ConflictException($"Warehouse code '{code}' already exists.");
        }

        var warehouse = new Warehouse
        {
            Code = code,
            Name = request.Name.Trim(),
            Address = TrimToNull(request.Address),
            Phone = TrimToNull(request.Phone),
            Description = TrimToNull(request.Description),
            ManagerId = request.ManagerId,
            IsActive = true,
            CreatedAt = Now,
            CreatedBy = currentUser.UserId
        };

        repository.Add(warehouse);
        await repository.SaveChangesAsync(cancellationToken);

        return WarehouseRepository.ToResponse(warehouse);
    }

    public async Task<WarehouseResponse> UpdateAsync(
        int id,
        UpdateWarehouseRequest request,
        CancellationToken cancellationToken)
    {
        var warehouse = await FindOrThrowAsync(id, cancellationToken);

        warehouse.Name = request.Name.Trim();
        warehouse.Address = TrimToNull(request.Address);
        warehouse.Phone = TrimToNull(request.Phone);
        warehouse.Description = TrimToNull(request.Description);
        warehouse.ManagerId = request.ManagerId;
        Touch(warehouse);

        await repository.SaveChangesAsync(cancellationToken);
        return WarehouseRepository.ToResponse(warehouse);
    }

    public async Task<WarehouseResponse> ActivateAsync(int id, CancellationToken cancellationToken)
    {
        var warehouse = await FindOrThrowAsync(id, cancellationToken);
        if (!warehouse.IsActive)
        {
            warehouse.IsActive = true;
            Touch(warehouse);
            await repository.SaveChangesAsync(cancellationToken);
        }

        return WarehouseRepository.ToResponse(warehouse);
    }

    public async Task<WarehouseResponse> DeactivateAsync(int id, CancellationToken cancellationToken)
    {
        var warehouse = await FindOrThrowAsync(id, cancellationToken);
        if (!warehouse.IsActive)
        {
            return WarehouseRepository.ToResponse(warehouse);
        }

        var blockers = (await GetDependenciesAsync(id, cancellationToken))
            .Where(dependency => dependency.BlocksDeactivation)
            .ToList();
        if (blockers.Count > 0)
        {
            throw new BusinessRuleException(
                "Cannot deactivate warehouse: " + string.Join("; ", blockers.Select(d => d.Description)) + ".");
        }

        warehouse.IsActive = false;
        Touch(warehouse);
        await repository.SaveChangesAsync(cancellationToken);

        return WarehouseRepository.ToResponse(warehouse);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken)
    {
        var warehouse = await FindOrThrowAsync(id, cancellationToken);

        var blockers = (await GetDependenciesAsync(id, cancellationToken))
            .Where(dependency => dependency.BlocksDeletion)
            .ToList();
        if (blockers.Count > 0)
        {
            throw new ConflictException(
                "Cannot delete warehouse: " + string.Join("; ", blockers.Select(d => d.Description))
                + ". Deactivate it instead.");
        }

        repository.Remove(warehouse);
        await repository.SaveChangesAsync(cancellationToken);
    }

    private async Task<List<WarehouseDependency>> GetDependenciesAsync(int id, CancellationToken cancellationToken)
    {
        var dependencies = new List<WarehouseDependency>();
        foreach (var checker in dependencyCheckers)
        {
            if (await checker.CheckAsync(id, cancellationToken) is { } dependency)
            {
                dependencies.Add(dependency);
            }
        }

        return dependencies;
    }

    private async Task<Warehouse> FindOrThrowAsync(int id, CancellationToken cancellationToken) =>
        await repository.FindAsync(id, cancellationToken) ?? throw new NotFoundException("Warehouse", id);

    private void Touch(Warehouse warehouse)
    {
        warehouse.UpdatedAt = Now;
        warehouse.UpdatedBy = currentUser.UserId;
    }

    private DateTime Now => timeProvider.GetUtcNow().UtcDateTime;

    private static string? TrimToNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
