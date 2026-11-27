using WMS.Api.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WMS.Api.Constants;
using WMS.Api.Dtos.Common;
using WMS.Api.Dtos.StockMovements;
using WMS.Api.Exceptions;
using WMS.Api.Repositories;

namespace WMS.Api.Controllers;

/// <summary>
/// Read-only stock ledger. There are intentionally no POST/PUT/DELETE endpoints:
/// movements are created only by warehouse documents and can never be edited or deleted.
/// </summary>
[ApiController]
[Authorize]
[Route("api/stock-movements")]
[Produces("application/json")]
public sealed class StockMovementsController(IStockMovementRepository repository) : ControllerBase
{
    /// <summary>Searches movement history (newest first by default).</summary>
    [HttpGet]
    [WarehousePermission(Permissions.StockMovementView)]
    [ProducesResponseType<PagedResult<StockMovementResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<StockMovementResponse>>> Search(
        [FromQuery] StockMovementQuery query,
        CancellationToken cancellationToken) =>
        Ok(await repository.SearchAsync(query, cancellationToken));

    [HttpGet("{id:long}")]
    [WarehousePermission(Permissions.StockMovementView, WarehouseResource.Movement)]
    [ProducesResponseType<StockMovementResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<StockMovementResponse>> GetById(long id, CancellationToken cancellationToken) =>
        Ok(await repository.GetByIdAsync(id, cancellationToken) ?? throw new NotFoundException("Stock movement", id));

    /// <summary>
    /// Stock report "Nhập - Xuất - Tồn" for [from, to): opening, in, out and closing quantity
    /// per product and warehouse.
    /// </summary>
    [HttpGet("report")]
    [WarehousePermission(Permissions.StockMovementView)]
    [ProducesResponseType<PagedResult<StockReportRow>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<StockReportRow>>> Report(
        [FromQuery] StockReportQuery query,
        CancellationToken cancellationToken) =>
        Ok(await repository.GetStockReportAsync(query, cancellationToken));
}
