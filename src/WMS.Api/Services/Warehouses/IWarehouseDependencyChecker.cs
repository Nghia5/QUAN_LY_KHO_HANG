namespace WMS.Api.Services.Warehouses;

/// <summary>
/// Reports data that depends on a warehouse. Each module that references warehouses
/// (locations, inventory, stock movements, documents) registers its own checker, so the
/// warehouse service never needs to know about those tables.
/// </summary>
public interface IWarehouseDependencyChecker
{
    Task<WarehouseDependency?> CheckAsync(int warehouseId, CancellationToken cancellationToken);
}

/// <param name="Description">Human readable reason, e.g. "3 inventory records with stock".</param>
/// <param name="BlocksDeactivation">True for stock on hand or open documents.</param>
/// <param name="BlocksDeletion">True for any referencing data (stock, history, locations...).</param>
public sealed record WarehouseDependency(string Description, bool BlocksDeactivation, bool BlocksDeletion);
