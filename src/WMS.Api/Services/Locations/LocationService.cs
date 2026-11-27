using WMS.Api.Common;
using WMS.Api.Dtos.Common;
using WMS.Api.Dtos.Locations;
using WMS.Api.Exceptions;
using WMS.Api.Models;
using WMS.Api.Repositories;
using WMS.Api.Validators;

namespace WMS.Api.Services.Locations;

public interface ILocationService
{
    Task<PagedResult<LocationResponse>> SearchAsync(int warehouseId, LocationQuery query, CancellationToken cancellationToken);

    Task<IReadOnlyList<LocationTreeNode>> GetTreeAsync(int warehouseId, CancellationToken cancellationToken);

    Task<LocationResponse> GetByIdAsync(int warehouseId, int id, CancellationToken cancellationToken);

    Task<LocationResponse> CreateAsync(int warehouseId, CreateLocationRequest request, CancellationToken cancellationToken);

    Task<LocationResponse> UpdateAsync(int warehouseId, int id, UpdateLocationRequest request, CancellationToken cancellationToken);

    Task<LocationResponse> ActivateAsync(int warehouseId, int id, CancellationToken cancellationToken);

    Task<LocationResponse> DeactivateAsync(int warehouseId, int id, CancellationToken cancellationToken);

    Task DeleteAsync(int warehouseId, int id, CancellationToken cancellationToken);
}

public sealed class LocationService(
    ILocationRepository repository,
    IWarehouseRepository warehouseRepository,
    IEnumerable<ILocationDependencyChecker> dependencyCheckers,
    ICurrentUser currentUser,
    TimeProvider timeProvider) : ILocationService
{
    public async Task<PagedResult<LocationResponse>> SearchAsync(
        int warehouseId,
        LocationQuery query,
        CancellationToken cancellationToken)
    {
        await EnsureWarehouseExistsAsync(warehouseId, cancellationToken);
        return await repository.SearchAsync(warehouseId, query, cancellationToken);
    }

    public async Task<IReadOnlyList<LocationTreeNode>> GetTreeAsync(int warehouseId, CancellationToken cancellationToken)
    {
        await EnsureWarehouseExistsAsync(warehouseId, cancellationToken);

        var all = await repository.ListAllAsync(warehouseId, cancellationToken);
        var byParent = all.ToLookup(location => location.ParentId);

        IReadOnlyList<LocationTreeNode> Build(int? parentId) => byParent[parentId]
            .Select(location => new LocationTreeNode(
                location.Id, location.Type, location.Code, location.Name, location.IsActive, Build(location.Id)))
            .ToList();

        return Build(null);
    }

    public async Task<LocationResponse> GetByIdAsync(int warehouseId, int id, CancellationToken cancellationToken) =>
        await repository.GetResponseAsync(warehouseId, id, cancellationToken)
        ?? throw new NotFoundException("Location", id);

    public async Task<LocationResponse> CreateAsync(
        int warehouseId,
        CreateLocationRequest request,
        CancellationToken cancellationToken)
    {
        var warehouse = await warehouseRepository.FindAsync(warehouseId, cancellationToken)
                        ?? throw new NotFoundException("Warehouse", warehouseId);
        if (!warehouse.IsActive)
        {
            throw new BusinessRuleException($"Warehouse '{warehouse.Code}' is inactive.");
        }

        var segment = CommonRules.NormalizeCode(request.SegmentCode);
        var code = segment;

        var requiredParentType = WarehouseLocation.RequiredParentType(request.Type);
        if (requiredParentType is not null)
        {
            // Scoped lookup: a parent from another warehouse is reported as not found in this one.
            var parent = await repository.FindAsync(warehouseId, request.ParentId!.Value, cancellationToken)
                         ?? throw new BusinessRuleException(
                             $"Parent location '{request.ParentId}' does not exist in warehouse '{warehouse.Code}'.");

            if (parent.Type != requiredParentType)
            {
                throw new BusinessRuleException(
                    $"A {request.Type} must be placed under a {requiredParentType}, not a {parent.Type}.");
            }

            if (!parent.IsActive)
            {
                throw new BusinessRuleException($"Parent location '{parent.Code}' is inactive.");
            }

            code = $"{parent.Code}-{segment}";
        }

        if (await repository.CodeExistsAsync(warehouseId, code, cancellationToken))
        {
            throw new ConflictException($"Location code '{code}' already exists in warehouse '{warehouse.Code}'.");
        }

        var location = new WarehouseLocation
        {
            WarehouseId = warehouseId,
            ParentId = request.ParentId,
            Type = request.Type,
            SegmentCode = segment,
            Code = code,
            Name = request.Name.Trim(),
            Description = TrimToNull(request.Description),
            IsActive = true,
            CreatedAt = Now,
            CreatedBy = currentUser.UserId
        };

        repository.Add(location);
        await repository.SaveChangesAsync(cancellationToken);

        return LocationRepository.ToResponse(location);
    }

    public async Task<LocationResponse> UpdateAsync(
        int warehouseId,
        int id,
        UpdateLocationRequest request,
        CancellationToken cancellationToken)
    {
        var location = await FindOrThrowAsync(warehouseId, id, cancellationToken);

        location.Name = request.Name.Trim();
        location.Description = TrimToNull(request.Description);
        Touch(location);

        await repository.SaveChangesAsync(cancellationToken);
        return LocationRepository.ToResponse(location);
    }

    public async Task<LocationResponse> ActivateAsync(int warehouseId, int id, CancellationToken cancellationToken)
    {
        var location = await FindOrThrowAsync(warehouseId, id, cancellationToken);
        if (location.IsActive)
        {
            return LocationRepository.ToResponse(location);
        }

        if (location.ParentId is { } parentId)
        {
            var parent = await repository.FindAsync(warehouseId, parentId, cancellationToken);
            if (parent is { IsActive: false })
            {
                throw new BusinessRuleException($"Activate parent location '{parent.Code}' first.");
            }
        }

        location.IsActive = true;
        Touch(location);
        await repository.SaveChangesAsync(cancellationToken);

        return LocationRepository.ToResponse(location);
    }

    public async Task<LocationResponse> DeactivateAsync(int warehouseId, int id, CancellationToken cancellationToken)
    {
        var location = await FindOrThrowAsync(warehouseId, id, cancellationToken);
        if (!location.IsActive)
        {
            return LocationRepository.ToResponse(location);
        }

        var blockers = new List<string>();
        var activeChildren = await repository.CountChildrenAsync(id, activeOnly: true, cancellationToken);
        if (activeChildren > 0)
        {
            blockers.Add($"{activeChildren} active child location(s)");
        }

        blockers.AddRange((await GetDependenciesAsync(id, cancellationToken))
            .Where(dependency => dependency.BlocksDeactivation)
            .Select(dependency => dependency.Description));

        if (blockers.Count > 0)
        {
            throw new BusinessRuleException($"Cannot deactivate location '{location.Code}': {string.Join("; ", blockers)}.");
        }

        location.IsActive = false;
        Touch(location);
        await repository.SaveChangesAsync(cancellationToken);

        return LocationRepository.ToResponse(location);
    }

    public async Task DeleteAsync(int warehouseId, int id, CancellationToken cancellationToken)
    {
        var location = await FindOrThrowAsync(warehouseId, id, cancellationToken);

        var blockers = new List<string>();
        var children = await repository.CountChildrenAsync(id, activeOnly: false, cancellationToken);
        if (children > 0)
        {
            blockers.Add($"{children} child location(s)");
        }

        blockers.AddRange((await GetDependenciesAsync(id, cancellationToken))
            .Where(dependency => dependency.BlocksDeletion)
            .Select(dependency => dependency.Description));

        if (blockers.Count > 0)
        {
            throw new ConflictException(
                $"Cannot delete location '{location.Code}': {string.Join("; ", blockers)}. Deactivate it instead.");
        }

        repository.Remove(location);
        await repository.SaveChangesAsync(cancellationToken);
    }

    private async Task<List<LocationDependency>> GetDependenciesAsync(int id, CancellationToken cancellationToken)
    {
        var dependencies = new List<LocationDependency>();
        foreach (var checker in dependencyCheckers)
        {
            if (await checker.CheckAsync(id, cancellationToken) is { } dependency)
            {
                dependencies.Add(dependency);
            }
        }

        return dependencies;
    }

    private async Task EnsureWarehouseExistsAsync(int warehouseId, CancellationToken cancellationToken)
    {
        if (await warehouseRepository.GetResponseByIdAsync(warehouseId, cancellationToken) is null)
        {
            throw new NotFoundException("Warehouse", warehouseId);
        }
    }

    private async Task<WarehouseLocation> FindOrThrowAsync(int warehouseId, int id, CancellationToken cancellationToken) =>
        await repository.FindAsync(warehouseId, id, cancellationToken) ?? throw new NotFoundException("Location", id);

    private void Touch(WarehouseLocation location)
    {
        location.UpdatedAt = Now;
        location.UpdatedBy = currentUser.UserId;
    }

    private DateTime Now => timeProvider.GetUtcNow().UtcDateTime;

    private static string? TrimToNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
