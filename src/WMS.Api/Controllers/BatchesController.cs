using WMS.Api.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WMS.Api.Constants;
using WMS.Api.Dtos.Batches;
using WMS.Api.Dtos.Common;
using WMS.Api.Services.Batches;

namespace WMS.Api.Controllers;

/// <summary>
/// Batches / lots (lô hàng) with manufacture and expiry dates. Batches are normally created by goods
/// receipts; stock per lot follows receipts, issues (FEFO), transfers, stocktakes and adjustments.
/// </summary>
[ApiController]
[Authorize]
[Route("api/batches")]
[Produces("application/json")]
public sealed class BatchesController(IBatchService service) : ControllerBase
{
    /// <summary>Searches batches; filter by product, warehouse, expiry status, "expiring within N days".</summary>
    [HttpGet]
    [WarehousePermission(Permissions.BatchView)]
    [ProducesResponseType<PagedResult<BatchResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<BatchResponse>>> Search([FromQuery] BatchQuery query, CancellationToken cancellationToken) =>
        Ok(await service.SearchAsync(query, cancellationToken));

    /// <summary>Batch with its stock per warehouse/bin.</summary>
    [HttpGet("{id:int}")]
    [WarehousePermission(Permissions.BatchView)]
    [ProducesResponseType<BatchDetail>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BatchDetail>> Get(int id, CancellationToken cancellationToken) =>
        Ok(await service.GetAsync(id, cancellationToken));

    /// <summary>Lots in stock that are expired or expire within N days (default Warehouse:NearExpiryDays).</summary>
    [HttpGet("expiry-alerts")]
    [WarehousePermission(Permissions.BatchView)]
    [ProducesResponseType<PagedResult<ExpiryAlertRow>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<ExpiryAlertRow>>> ExpiryAlerts(
        [FromQuery] ExpiryAlertQuery query,
        CancellationToken cancellationToken) =>
        Ok(await service.GetExpiryAlertsAsync(query, cancellationToken));

    /// <summary>Which lots/bins to pick for a quantity, by FEFO (default) or FIFO. Read-only suggestion.</summary>
    [HttpGet("pick-suggestion")]
    [WarehousePermission(Permissions.BatchView)]
    [ProducesResponseType<PickSuggestion>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PickSuggestion>> PickSuggestion(
        [FromQuery] PickSuggestionQuery query,
        CancellationToken cancellationToken) =>
        Ok(await service.SuggestPickAsync(query, cancellationToken));

    /// <summary>Registers a batch in advance (receipts also create batches automatically).</summary>
    [HttpPost]
    [Authorize(Policy = ActiveAdminRequirement.PolicyName)]
    [ProducesResponseType<BatchDetail>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<BatchDetail>> Create(CreateBatchRequest request, CancellationToken cancellationToken)
    {
        var batch = await service.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = batch.Batch.Id }, batch);
    }

    /// <summary>Corrects dates/note (code and product are immutable). Audited.</summary>
    [HttpPut("{id:int}")]
    [Authorize(Policy = ActiveAdminRequirement.PolicyName)]
    [ProducesResponseType<BatchDetail>(StatusCodes.Status200OK)]
    public async Task<ActionResult<BatchDetail>> Update(int id, UpdateBatchRequest request, CancellationToken cancellationToken) =>
        Ok(await service.UpdateAsync(id, request, cancellationToken));

    /// <summary>Deletes a batch that was never used for stock.</summary>
    [HttpDelete("{id:int}")]
    [Authorize(Policy = ActiveAdminRequirement.PolicyName)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await service.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
