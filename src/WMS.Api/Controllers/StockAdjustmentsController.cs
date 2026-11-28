using WMS.Api.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WMS.Api.Constants;
using WMS.Api.Dtos.Common;
using WMS.Api.Dtos.Documents;
using WMS.Api.Dtos.StockAdjustments;
using WMS.Api.Services.StockAdjustments;

namespace WMS.Api.Controllers;

/// <summary>
/// Stock adjustments (phiếu điều chỉnh tồn kho): create → submit → approve (applies + completes).
/// A reason is mandatory; stock can never become negative or drop below reserved quantity.
/// This is the only supported way to correct stock quantities outside other documents.
/// </summary>
[ApiController]
[Authorize]
[Route("api/stock-adjustments")]
[Produces("application/json")]
public sealed class StockAdjustmentsController(IStockAdjustmentService service) : ControllerBase
{
    [HttpGet]
    [WarehousePermission(Permissions.StockAdjustmentView)]
    [ProducesResponseType<PagedResult<StockAdjustmentSummary>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<StockAdjustmentSummary>>> Search(
        [FromQuery] StockAdjustmentQuery query,
        CancellationToken cancellationToken) =>
        Ok(await service.SearchAsync(query, cancellationToken));

    /// <summary>Detail with a live preview: current quantity and expected quantity after the adjustment.</summary>
    [HttpGet("{id:int}")]
    [WarehousePermission(Permissions.StockAdjustmentView, WarehouseResource.Adjustment)]
    [ProducesResponseType<StockAdjustmentDetail>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<StockAdjustmentDetail>> Get(int id, CancellationToken cancellationToken) =>
        Ok(await service.GetAsync(id, cancellationToken));

    [HttpPost]
    [WarehousePermission(Permissions.StockAdjustmentCreate)]
    [ProducesResponseType<StockAdjustmentDetail>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<StockAdjustmentDetail>> Create(CreateStockAdjustmentRequest request, CancellationToken cancellationToken)
    {
        var adjustment = await service.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = adjustment.Id }, adjustment);
    }

    [HttpPut("{id:int}")]
    [WarehousePermission(Permissions.StockAdjustmentCreate, WarehouseResource.Adjustment)]
    [ProducesResponseType<StockAdjustmentDetail>(StatusCodes.Status200OK)]
    public async Task<ActionResult<StockAdjustmentDetail>> Update(int id, UpdateStockAdjustmentRequest request, CancellationToken cancellationToken) =>
        Ok(await service.UpdateAsync(id, request, cancellationToken));

    [HttpDelete("{id:int}")]
    [WarehousePermission(Permissions.StockAdjustmentCreate, WarehouseResource.Adjustment)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await service.DeleteAsync(id, cancellationToken);
        return NoContent();
    }

    /// <summary>Submits for approval (or applies immediately when the policy does not require approval).</summary>
    [HttpPost("{id:int}/submit")]
    [WarehousePermission(Permissions.StockAdjustmentCreate, WarehouseResource.Adjustment)]
    [ProducesResponseType<StockAdjustmentDetail>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<StockAdjustmentDetail>> Submit(int id, CancellationToken cancellationToken) =>
        Ok(await service.SubmitAsync(id, cancellationToken));

    /// <summary>Approves and applies: stock + ADJUSTMENT_IN/OUT movements + audit, in one transaction.</summary>
    [HttpPost("{id:int}/approve")]
    [WarehousePermission(Permissions.StockAdjustmentApprove, WarehouseResource.Adjustment)]
    [ProducesResponseType<StockAdjustmentDetail>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<StockAdjustmentDetail>> Approve(int id, CancellationToken cancellationToken) =>
        Ok(await service.ApproveAsync(id, cancellationToken));

    [HttpPost("{id:int}/reject")]
    [WarehousePermission(Permissions.StockAdjustmentApprove, WarehouseResource.Adjustment)]
    [ProducesResponseType<StockAdjustmentDetail>(StatusCodes.Status200OK)]
    public async Task<ActionResult<StockAdjustmentDetail>> Reject(int id, DocumentReasonRequest request, CancellationToken cancellationToken) =>
        Ok(await service.RejectAsync(id, request.Reason, cancellationToken));

    [HttpPost("{id:int}/reopen")]
    [WarehousePermission(Permissions.StockAdjustmentCreate, WarehouseResource.Adjustment)]
    [ProducesResponseType<StockAdjustmentDetail>(StatusCodes.Status200OK)]
    public async Task<ActionResult<StockAdjustmentDetail>> Reopen(int id, CancellationToken cancellationToken) =>
        Ok(await service.ReopenAsync(id, cancellationToken));

    [HttpPost("{id:int}/cancel")]
    [WarehousePermission(Permissions.StockAdjustmentCreate, WarehouseResource.Adjustment)]
    [ProducesResponseType<StockAdjustmentDetail>(StatusCodes.Status200OK)]
    public async Task<ActionResult<StockAdjustmentDetail>> Cancel(int id, DocumentReasonRequest request, CancellationToken cancellationToken) =>
        Ok(await service.CancelAsync(id, request.Reason, cancellationToken));
}
