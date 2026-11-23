using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WMS.Api.Constants;
using WMS.Api.Dtos.Common;
using WMS.Api.Dtos.Warehouses;
using WMS.Api.Services.Warehouses;

namespace WMS.Api.Controllers;

[ApiController]
[Route("api/warehouses")]
[Produces("application/json")]
public sealed class WarehousesController(IWarehouseService warehouseService) : ControllerBase
{
    /// <summary>Searches warehouses with paging, keyword, filters and sorting.</summary>
    [HttpGet]
    [Authorize(Policy = Permissions.WarehouseView)]
    [ProducesResponseType<PagedResult<WarehouseResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<WarehouseResponse>>> Search(
        [FromQuery] WarehouseQuery query,
        CancellationToken cancellationToken) =>
        Ok(await warehouseService.SearchAsync(query, cancellationToken));

    /// <summary>Gets a warehouse by ID.</summary>
    [HttpGet("{id:int}")]
    [Authorize(Policy = Permissions.WarehouseView)]
    [ProducesResponseType<WarehouseResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<WarehouseResponse>> GetById(int id, CancellationToken cancellationToken) =>
        Ok(await warehouseService.GetByIdAsync(id, cancellationToken));

    /// <summary>Creates a warehouse. Code is normalized to upper-case and must be unique.</summary>
    [HttpPost]
    [Authorize(Policy = Permissions.WarehouseCreate)]
    [ProducesResponseType<WarehouseResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<WarehouseResponse>> Create(
        CreateWarehouseRequest request,
        CancellationToken cancellationToken)
    {
        var warehouse = await warehouseService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = warehouse.Id }, warehouse);
    }

    /// <summary>Updates a warehouse (Code is immutable).</summary>
    [HttpPut("{id:int}")]
    [Authorize(Policy = Permissions.WarehouseUpdate)]
    [ProducesResponseType<WarehouseResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<WarehouseResponse>> Update(
        int id,
        UpdateWarehouseRequest request,
        CancellationToken cancellationToken) =>
        Ok(await warehouseService.UpdateAsync(id, request, cancellationToken));

    /// <summary>Re-activates a warehouse.</summary>
    [HttpPatch("{id:int}/activate")]
    [Authorize(Policy = Permissions.WarehouseUpdate)]
    [ProducesResponseType<WarehouseResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<WarehouseResponse>> Activate(int id, CancellationToken cancellationToken) =>
        Ok(await warehouseService.ActivateAsync(id, cancellationToken));

    /// <summary>Deactivates a warehouse. Fails while it holds stock or has open documents.</summary>
    [HttpPatch("{id:int}/deactivate")]
    [Authorize(Policy = Permissions.WarehouseUpdate)]
    [ProducesResponseType<WarehouseResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<WarehouseResponse>> Deactivate(int id, CancellationToken cancellationToken) =>
        Ok(await warehouseService.DeactivateAsync(id, cancellationToken));

    /// <summary>Hard-deletes a warehouse that has no locations, stock or transactions.</summary>
    [HttpDelete("{id:int}")]
    [Authorize(Policy = Permissions.WarehouseDelete)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await warehouseService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
