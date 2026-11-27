using WMS.Api.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WMS.Api.Constants;
using WMS.Api.Dtos.Common;
using WMS.Api.Dtos.Documents;
using WMS.Api.Dtos.StockTransfers;
using WMS.Api.Services.StockTransfers;

namespace WMS.Api.Controllers;

/// <summary>
/// Stock transfers (phiếu chuyển kho / chuyển vị trí):
/// create → submit → approve (reserve source) → pick → ship (TRANSFER_OUT) → receive (TRANSFER_IN) → complete.
/// Errors: 400 invalid payload, 403 missing permission, 404 not found, 409 concurrent change,
/// 422 business rule (insufficient stock, invalid status transition, self-approval...).
/// </summary>
[ApiController]
[Authorize]
[Route("api/stock-transfers")]
[Produces("application/json")]
public sealed class StockTransfersController(IStockTransferService service) : ControllerBase
{
    [HttpGet]
    [WarehousePermission(Permissions.StockTransferView)]
    [ProducesResponseType<PagedResult<StockTransferSummary>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<StockTransferSummary>>> Search(
        [FromQuery] StockTransferQuery query,
        CancellationToken cancellationToken) =>
        Ok(await service.SearchAsync(query, cancellationToken));

    [HttpGet("{id:int}")]
    [WarehousePermission(Permissions.StockTransferView, WarehouseResource.TransferEither)]
    [ProducesResponseType<StockTransferDetail>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<StockTransferDetail>> Get(int id, CancellationToken cancellationToken) =>
        Ok(await service.GetAsync(id, cancellationToken));

    /// <summary>Creates a Draft transfer (same warehouse on both sides = move between bins).</summary>
    [HttpPost]
    [WarehousePermission(Permissions.StockTransferCreate)]
    [ProducesResponseType<StockTransferDetail>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<StockTransferDetail>> Create(CreateStockTransferRequest request, CancellationToken cancellationToken)
    {
        var transfer = await service.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = transfer.Id }, transfer);
    }

    [HttpPut("{id:int}")]
    [WarehousePermission(Permissions.StockTransferCreate, WarehouseResource.Transfer)]
    [ProducesResponseType<StockTransferDetail>(StatusCodes.Status200OK)]
    public async Task<ActionResult<StockTransferDetail>> Update(int id, UpdateStockTransferRequest request, CancellationToken cancellationToken) =>
        Ok(await service.UpdateAsync(id, request, cancellationToken));

    [HttpDelete("{id:int}")]
    [WarehousePermission(Permissions.StockTransferCreate, WarehouseResource.Transfer)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await service.DeleteAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:int}/submit")]
    [WarehousePermission(Permissions.StockTransferCreate, WarehouseResource.Transfer)]
    [ProducesResponseType<StockTransferDetail>(StatusCodes.Status200OK)]
    public async Task<ActionResult<StockTransferDetail>> Submit(int id, CancellationToken cancellationToken) =>
        Ok(await service.SubmitAsync(id, cancellationToken));

    /// <summary>PendingApproval → Approved; reserves the source stock (422 if insufficient, nothing reserved).</summary>
    [HttpPost("{id:int}/approve")]
    [WarehousePermission(Permissions.StockTransferApprove, WarehouseResource.Transfer)]
    [ProducesResponseType<StockTransferDetail>(StatusCodes.Status200OK)]
    public async Task<ActionResult<StockTransferDetail>> Approve(int id, CancellationToken cancellationToken) =>
        Ok(await service.ApproveAsync(id, cancellationToken));

    [HttpPost("{id:int}/reject")]
    [WarehousePermission(Permissions.StockTransferApprove, WarehouseResource.Transfer)]
    [ProducesResponseType<StockTransferDetail>(StatusCodes.Status200OK)]
    public async Task<ActionResult<StockTransferDetail>> Reject(int id, DocumentReasonRequest request, CancellationToken cancellationToken) =>
        Ok(await service.RejectAsync(id, request.Reason, cancellationToken));

    [HttpPost("{id:int}/reopen")]
    [WarehousePermission(Permissions.StockTransferCreate, WarehouseResource.Transfer)]
    [ProducesResponseType<StockTransferDetail>(StatusCodes.Status200OK)]
    public async Task<ActionResult<StockTransferDetail>> Reopen(int id, CancellationToken cancellationToken) =>
        Ok(await service.ReopenAsync(id, cancellationToken));

    /// <summary>Approved → Picked.</summary>
    [HttpPost("{id:int}/pick")]
    [WarehousePermission(Permissions.StockTransferPick, WarehouseResource.Transfer)]
    [ProducesResponseType<StockTransferDetail>(StatusCodes.Status200OK)]
    public async Task<ActionResult<StockTransferDetail>> Pick(int id, CancellationToken cancellationToken) =>
        Ok(await service.PickAsync(id, cancellationToken));

    /// <summary>Picked → InTransit: source stock decreases (TRANSFER_OUT).</summary>
    [HttpPost("{id:int}/ship")]
    [WarehousePermission(Permissions.StockTransferShip, WarehouseResource.Transfer)]
    [ProducesResponseType<StockTransferDetail>(StatusCodes.Status200OK)]
    public async Task<ActionResult<StockTransferDetail>> Ship(int id, CancellationToken cancellationToken) =>
        Ok(await service.ShipAsync(id, cancellationToken));

    /// <summary>InTransit → Received: destination stock increases (TRANSFER_IN).</summary>
    [HttpPost("{id:int}/receive")]
    [WarehousePermission(Permissions.StockTransferReceive, WarehouseResource.TransferDestination)]
    [ProducesResponseType<StockTransferDetail>(StatusCodes.Status200OK)]
    public async Task<ActionResult<StockTransferDetail>> Receive(int id, CancellationToken cancellationToken) =>
        Ok(await service.ReceiveAsync(id, cancellationToken));

    /// <summary>Received → Completed.</summary>
    [HttpPost("{id:int}/complete")]
    [WarehousePermission(Permissions.StockTransferComplete, WarehouseResource.TransferDestination)]
    [ProducesResponseType<StockTransferDetail>(StatusCodes.Status200OK)]
    public async Task<ActionResult<StockTransferDetail>> Complete(int id, CancellationToken cancellationToken) =>
        Ok(await service.CompleteAsync(id, cancellationToken));

    /// <summary>Cancels before shipping (reason required). Approved/picked transfers release their reservation.</summary>
    [HttpPost("{id:int}/cancel")]
    [WarehousePermission(Permissions.StockTransferCreate, WarehouseResource.Transfer)]
    [ProducesResponseType<StockTransferDetail>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<StockTransferDetail>> Cancel(int id, DocumentReasonRequest request, CancellationToken cancellationToken) =>
        Ok(await service.CancelAsync(id, request.Reason, cancellationToken));
}
