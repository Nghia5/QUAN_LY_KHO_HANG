using WMS.Api.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WMS.Api.Constants;
using WMS.Api.Dtos.Common;
using WMS.Api.Dtos.Documents;
using WMS.Api.Dtos.Stocktakes;
using WMS.Api.Services.Stocktakes;

namespace WMS.Api.Controllers;

/// <summary>
/// Stocktakes (phiếu kiểm kê): create → snapshot → count → compare/submit → approve → adjust → complete.
/// While a stocktake is counting (snapshot taken, not adjusted yet), stock in its locations is frozen.
/// </summary>
[ApiController]
[Authorize]
[Route("api/stocktakes")]
[Produces("application/json")]
public sealed class StocktakesController(IStocktakeService service) : ControllerBase
{
    [HttpGet]
    [WarehousePermission(Permissions.StocktakeView)]
    [ProducesResponseType<PagedResult<StocktakeSummary>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<StocktakeSummary>>> Search([FromQuery] StocktakeQuery query, CancellationToken cancellationToken) =>
        Ok(await service.SearchAsync(query, cancellationToken));

    [HttpGet("{id:int}")]
    [WarehousePermission(Permissions.StocktakeView, WarehouseResource.Stocktake)]
    [ProducesResponseType<StocktakeDetail>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<StocktakeDetail>> Get(int id, CancellationToken cancellationToken) =>
        Ok(await service.GetAsync(id, cancellationToken));

    /// <summary>COMPARE: lines where counted quantity differs from the snapshot, with totals.</summary>
    [HttpGet("{id:int}/differences")]
    [WarehousePermission(Permissions.StocktakeView, WarehouseResource.Stocktake)]
    [ProducesResponseType<StocktakeDetail>(StatusCodes.Status200OK)]
    public async Task<ActionResult<StocktakeDetail>> Differences(int id, CancellationToken cancellationToken) =>
        Ok(await service.GetDifferencesAsync(id, cancellationToken));

    /// <summary>CREATE: Draft stocktake for a warehouse (empty scope = whole warehouse).</summary>
    [HttpPost]
    [WarehousePermission(Permissions.StocktakeCreate)]
    [ProducesResponseType<StocktakeDetail>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<StocktakeDetail>> Create(CreateStocktakeRequest request, CancellationToken cancellationToken)
    {
        var stocktake = await service.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = stocktake.Id }, stocktake);
    }

    [HttpPut("{id:int}")]
    [WarehousePermission(Permissions.StocktakeCreate, WarehouseResource.Stocktake)]
    [ProducesResponseType<StocktakeDetail>(StatusCodes.Status200OK)]
    public async Task<ActionResult<StocktakeDetail>> Update(int id, UpdateStocktakeRequest request, CancellationToken cancellationToken) =>
        Ok(await service.UpdateAsync(id, request, cancellationToken));

    [HttpDelete("{id:int}")]
    [WarehousePermission(Permissions.StocktakeCreate, WarehouseResource.Stocktake)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await service.DeleteAsync(id, cancellationToken);
        return NoContent();
    }

    /// <summary>SNAPSHOT: Draft → InProgress; records system quantities and locks the locations.</summary>
    [HttpPost("{id:int}/snapshot")]
    [WarehousePermission(Permissions.StocktakeCreate, WarehouseResource.Stocktake)]
    [ProducesResponseType<StocktakeDetail>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<StocktakeDetail>> Snapshot(int id, CancellationToken cancellationToken) =>
        Ok(await service.SnapshotAsync(id, cancellationToken));

    /// <summary>COUNT: records counted quantities (repeatable while InProgress; new items are added).</summary>
    [HttpPut("{id:int}/counts")]
    [WarehousePermission(Permissions.StocktakeCount, WarehouseResource.Stocktake)]
    [ProducesResponseType<StocktakeDetail>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<StocktakeDetail>> Count(int id, StocktakeCountRequest request, CancellationToken cancellationToken) =>
        Ok(await service.CountAsync(id, request, cancellationToken));

    /// <summary>COMPARE → PendingApproval (every line must be counted).</summary>
    [HttpPost("{id:int}/submit")]
    [WarehousePermission(Permissions.StocktakeCreate, WarehouseResource.Stocktake)]
    [ProducesResponseType<StocktakeDetail>(StatusCodes.Status200OK)]
    public async Task<ActionResult<StocktakeDetail>> Submit(int id, CancellationToken cancellationToken) =>
        Ok(await service.SubmitAsync(id, cancellationToken));

    /// <summary>APPROVE: only STOCKTAKE_APPROVE, and not by the creator.</summary>
    [HttpPost("{id:int}/approve")]
    [WarehousePermission(Permissions.StocktakeApprove, WarehouseResource.Stocktake)]
    [ProducesResponseType<StocktakeDetail>(StatusCodes.Status200OK)]
    public async Task<ActionResult<StocktakeDetail>> Approve(int id, CancellationToken cancellationToken) =>
        Ok(await service.ApproveAsync(id, cancellationToken));

    /// <summary>Rejects the counts: back to InProgress for a recount (reason required).</summary>
    [HttpPost("{id:int}/reject")]
    [WarehousePermission(Permissions.StocktakeApprove, WarehouseResource.Stocktake)]
    [ProducesResponseType<StocktakeDetail>(StatusCodes.Status200OK)]
    public async Task<ActionResult<StocktakeDetail>> Reject(int id, DocumentReasonRequest request, CancellationToken cancellationToken) =>
        Ok(await service.RejectAsync(id, request.Reason, cancellationToken));

    /// <summary>ADJUSTMENT: posts the differences as STOCKTAKE_IN/OUT movements.</summary>
    [HttpPost("{id:int}/adjust")]
    [WarehousePermission(Permissions.StocktakeAdjust, WarehouseResource.Stocktake)]
    [ProducesResponseType<StocktakeDetail>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<StocktakeDetail>> Adjust(int id, CancellationToken cancellationToken) =>
        Ok(await service.AdjustAsync(id, cancellationToken));

    [HttpPost("{id:int}/complete")]
    [WarehousePermission(Permissions.StocktakeComplete, WarehouseResource.Stocktake)]
    [ProducesResponseType<StocktakeDetail>(StatusCodes.Status200OK)]
    public async Task<ActionResult<StocktakeDetail>> Complete(int id, CancellationToken cancellationToken) =>
        Ok(await service.CompleteAsync(id, cancellationToken));

    /// <summary>Cancels before adjustment (unlocks the locations). Reason required.</summary>
    [HttpPost("{id:int}/cancel")]
    [WarehousePermission(Permissions.StocktakeCreate, WarehouseResource.Stocktake)]
    [ProducesResponseType<StocktakeDetail>(StatusCodes.Status200OK)]
    public async Task<ActionResult<StocktakeDetail>> Cancel(int id, DocumentReasonRequest request, CancellationToken cancellationToken) =>
        Ok(await service.CancelAsync(id, request.Reason, cancellationToken));
}
