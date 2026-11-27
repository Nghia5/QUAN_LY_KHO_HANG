namespace WMS.Api.Services.Locations;

/// <summary>
/// Reports data that depends on a location (inventory, stock movements, documents).
/// Implemented by later modules (e.g. Inventory in W-04) so the location service stays decoupled.
/// </summary>
public interface ILocationDependencyChecker
{
    Task<LocationDependency?> CheckAsync(int locationId, CancellationToken cancellationToken);
}

/// <param name="BlocksDeactivation">True for stock on hand or open documents.</param>
/// <param name="BlocksDeletion">True for any referencing data (stock, history...).</param>
public sealed record LocationDependency(string Description, bool BlocksDeactivation, bool BlocksDeletion);
