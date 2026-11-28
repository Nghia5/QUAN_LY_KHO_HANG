using WMS.Api.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WMS.Api.Constants;
using WMS.Api.Dtos.Common;
using WMS.Api.Dtos.Documents;
using WMS.Api.Dtos.GoodsReceipts;
using WMS.Api.Services.GoodsReceipts;

namespace WMS.Api.Controllers;

/// <summary>
/// Goods receipts (phiếu nhập kho): Draft → submit → approve → complete (stock is increased on complete).
/// Errors: 400 invalid payload, 403 missing permission, 404 not found, 409 concurrent change,
/// 422 business rule (invalid status transition, inactive product/location, self-approval...).
/// </summary>
[ApiController]
[Authorize]
[Route("api/goods-receipts")]
[Produces("application/json")]
public sealed class GoodsReceiptsController(IGoodsReceiptService service) : ControllerBase
{
    [HttpGet]
    [WarehousePermission(Permissions.GoodsReceiptView)]
    [ProducesResponseType<PagedResult<GoodsReceiptSummary>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<GoodsReceiptSummary>>> Search(
        [FromQuery] GoodsReceiptQuery query,
        CancellationToken cancellationToken) =>
        Ok(await service.SearchAsync(query, cancellationToken));

    [HttpGet("{id:int}")]
    [WarehousePermission(Permissions.GoodsReceiptView, WarehouseResource.Receipt)]
    [ProducesResponseType<GoodsReceiptDetail>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GoodsReceiptDetail>> Get(int id, CancellationToken cancellationToken) =>
        Ok(await service.GetAsync(id, cancellationToken));

    /// <summary>Creates a Draft receipt (no stock change yet).</summary>
    [HttpPost]
    [WarehousePermission(Permissions.GoodsReceiptCreate)]
    [ProducesResponseType<GoodsReceiptDetail>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<GoodsReceiptDetail>> Create(
        CreateGoodsReceiptRequest request,
        CancellationToken cancellationToken)
    {
        var receipt = await service.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = receipt.Id }, receipt);
    }

    /// <summary>Replaces header and lines of a Draft receipt.</summary>
    [HttpPut("{id:int}")]
    [WarehousePermission(Permissions.GoodsReceiptCreate, WarehouseResource.Receipt)]
    [ProducesResponseType<GoodsReceiptDetail>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<GoodsReceiptDetail>> Update(
        int id,
        UpdateGoodsReceiptRequest request,
        CancellationToken cancellationToken) =>
        Ok(await service.UpdateAsync(id, request, cancellationToken));

    /// <summary>Deletes a Draft receipt.</summary>
    [HttpDelete("{id:int}")]
    [WarehousePermission(Permissions.GoodsReceiptCreate, WarehouseResource.Receipt)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await service.DeleteAsync(id, cancellationToken);
        return NoContent();
    }

    /// <summary>Draft → PendingApproval.</summary>
    [HttpPost("{id:int}/submit")]
    [WarehousePermission(Permissions.GoodsReceiptCreate, WarehouseResource.Receipt)]
    [ProducesResponseType<GoodsReceiptDetail>(StatusCodes.Status200OK)]
    public async Task<ActionResult<GoodsReceiptDetail>> Submit(int id, CancellationToken cancellationToken) =>
        Ok(await service.SubmitAsync(id, cancellationToken));

    /// <summary>PendingApproval → Approved (approver must differ from the creator).</summary>
    [HttpPost("{id:int}/approve")]
    [WarehousePermission(Permissions.GoodsReceiptApprove, WarehouseResource.Receipt)]
    [ProducesResponseType<GoodsReceiptDetail>(StatusCodes.Status200OK)]
    public async Task<ActionResult<GoodsReceiptDetail>> Approve(int id, CancellationToken cancellationToken) =>
        Ok(await service.ApproveAsync(id, cancellationToken));

    /// <summary>PendingApproval → Rejected (reason required).</summary>
    [HttpPost("{id:int}/reject")]
    [WarehousePermission(Permissions.GoodsReceiptApprove, WarehouseResource.Receipt)]
    [ProducesResponseType<GoodsReceiptDetail>(StatusCodes.Status200OK)]
    public async Task<ActionResult<GoodsReceiptDetail>> Reject(
        int id,
        DocumentReasonRequest request,
        CancellationToken cancellationToken) =>
        Ok(await service.RejectAsync(id, request.Reason, cancellationToken));

    /// <summary>Rejected → Draft, to fix and submit again.</summary>
    [HttpPost("{id:int}/reopen")]
    [WarehousePermission(Permissions.GoodsReceiptCreate, WarehouseResource.Receipt)]
    [ProducesResponseType<GoodsReceiptDetail>(StatusCodes.Status200OK)]
    public async Task<ActionResult<GoodsReceiptDetail>> Reopen(int id, CancellationToken cancellationToken) =>
        Ok(await service.ReopenAsync(id, cancellationToken));

    /// <summary>Approved → Completed: increases stock, writes stock movements and audit in one transaction.</summary>
    [HttpPost("{id:int}/complete")]
    [WarehousePermission(Permissions.GoodsReceiptComplete, WarehouseResource.Receipt)]
    [ProducesResponseType<GoodsReceiptDetail>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<GoodsReceiptDetail>> Complete(int id, CancellationToken cancellationToken) =>
        Ok(await service.CompleteAsync(id, cancellationToken));

    /// <summary>Cancels a receipt that is not completed (reason required; approved ones need GOODS_RECEIPT_APPROVE).</summary>
    [HttpPost("{id:int}/cancel")]
    [WarehousePermission(Permissions.GoodsReceiptCreate, WarehouseResource.Receipt)]
    [ProducesResponseType<GoodsReceiptDetail>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<GoodsReceiptDetail>> Cancel(
        int id,
        DocumentReasonRequest request,
        CancellationToken cancellationToken) =>
        Ok(await service.CancelAsync(id, request.Reason, cancellationToken));
}
