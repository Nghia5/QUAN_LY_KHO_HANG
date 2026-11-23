using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WMS.Api.Constants;
using WMS.Api.Dtos.Common;
using WMS.Api.Dtos.Inventories;
using WMS.Api.Exceptions;
using WMS.Api.Repositories;

namespace WMS.Api.Controllers;

/// <summary>
/// Read-only inventory queries. Stock is never edited here: it changes only through
/// warehouse documents (receipt, issue, transfer, stocktake, adjustment).
/// </summary>
[ApiController]
[Route("api/inventories")]
[Produces("application/json")]
[Authorize(Policy = Permissions.InventoryView)]
public sealed class InventoriesController(IInventoryRepository inventoryRepository) : ControllerBase
{
    /// <summary>
    /// Searches inventory by warehouse/location/product/category with stock-status filter
    /// (InStock, LowStock, OutOfStock), keyword, whitelisted sorting and server-side paging.
    /// </summary>
    [HttpGet]
    [ProducesResponseType<PagedResult<InventoryResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<InventoryResponse>>> Search(
        [FromQuery] InventoryQuery query,
        CancellationToken cancellationToken) =>
        Ok(await inventoryRepository.SearchAsync(query, cancellationToken));

    [HttpGet("{id:int}")]
    [ProducesResponseType<InventoryResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<InventoryResponse>> GetById(int id, CancellationToken cancellationToken) =>
        Ok(await inventoryRepository.GetByIdAsync(id, cancellationToken) ?? throw new NotFoundException("Inventory", id));
}
