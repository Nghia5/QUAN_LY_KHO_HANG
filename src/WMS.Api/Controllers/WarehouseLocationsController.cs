using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WMS.Api.Constants;
using WMS.Api.Dtos.Common;
using WMS.Api.Dtos.Locations;
using WMS.Api.Services.Locations;

namespace WMS.Api.Controllers;

/// <summary>
/// Locations are always addressed through their warehouse, so a location id from another
/// warehouse returns 404 instead of leaking or modifying data across warehouses.
/// </summary>
[ApiController]
[Route("api/warehouses/{warehouseId:int}/locations")]
[Produces("application/json")]
public sealed class WarehouseLocationsController(ILocationService locationService) : ControllerBase
{
    /// <summary>Searches locations of a warehouse (paging, keyword, type/parent/active filters).</summary>
    [HttpGet]
    [Authorize(Policy = Permissions.LocationView)]
    [ProducesResponseType<PagedResult<LocationResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PagedResult<LocationResponse>>> Search(
        int warehouseId,
        [FromQuery] LocationQuery query,
        CancellationToken cancellationToken) =>
        Ok(await locationService.SearchAsync(warehouseId, query, cancellationToken));

    /// <summary>Returns the full Zone &gt; Rack &gt; Shelf &gt; Bin tree of a warehouse.</summary>
    [HttpGet("tree")]
    [Authorize(Policy = Permissions.LocationView)]
    [ProducesResponseType<IReadOnlyList<LocationTreeNode>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<LocationTreeNode>>> GetTree(
        int warehouseId,
        CancellationToken cancellationToken) =>
        Ok(await locationService.GetTreeAsync(warehouseId, cancellationToken));

    [HttpGet("{id:int}")]
    [Authorize(Policy = Permissions.LocationView)]
    [ProducesResponseType<LocationResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LocationResponse>> GetById(
        int warehouseId,
        int id,
        CancellationToken cancellationToken) =>
        Ok(await locationService.GetByIdAsync(warehouseId, id, cancellationToken));

    /// <summary>Creates a location; the parent must be the level directly above, in the same warehouse.</summary>
    [HttpPost]
    [Authorize(Policy = Permissions.LocationCreate)]
    [ProducesResponseType<LocationResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<LocationResponse>> Create(
        int warehouseId,
        CreateLocationRequest request,
        CancellationToken cancellationToken)
    {
        var location = await locationService.CreateAsync(warehouseId, request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { warehouseId, id = location.Id }, location);
    }

    /// <summary>Updates name/description (code, type and parent are immutable).</summary>
    [HttpPut("{id:int}")]
    [Authorize(Policy = Permissions.LocationUpdate)]
    [ProducesResponseType<LocationResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LocationResponse>> Update(
        int warehouseId,
        int id,
        UpdateLocationRequest request,
        CancellationToken cancellationToken) =>
        Ok(await locationService.UpdateAsync(warehouseId, id, request, cancellationToken));

    [HttpPatch("{id:int}/activate")]
    [Authorize(Policy = Permissions.LocationUpdate)]
    [ProducesResponseType<LocationResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<LocationResponse>> Activate(
        int warehouseId,
        int id,
        CancellationToken cancellationToken) =>
        Ok(await locationService.ActivateAsync(warehouseId, id, cancellationToken));

    /// <summary>Deactivates a location; fails while it has active children or stock.</summary>
    [HttpPatch("{id:int}/deactivate")]
    [Authorize(Policy = Permissions.LocationUpdate)]
    [ProducesResponseType<LocationResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<LocationResponse>> Deactivate(
        int warehouseId,
        int id,
        CancellationToken cancellationToken) =>
        Ok(await locationService.DeactivateAsync(warehouseId, id, cancellationToken));

    /// <summary>Hard-deletes a location without children, stock or history.</summary>
    [HttpDelete("{id:int}")]
    [Authorize(Policy = Permissions.LocationDelete)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(int warehouseId, int id, CancellationToken cancellationToken)
    {
        await locationService.DeleteAsync(warehouseId, id, cancellationToken);
        return NoContent();
    }
}
