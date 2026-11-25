using WMS.Api.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WMS.Api.Constants;
using WMS.Api.Dtos.Common;
using WMS.Api.Dtos.Documents;
using WMS.Api.Dtos.GoodsIssues;
using WMS.Api.Services.GoodsIssues;

namespace WMS.Api.Controllers;

/// <summary>
/// Goods issues (phiếu xuất kho): Draft → submit → approve (reserves stock) → complete (decreases stock).
/// Errors: 400 invalid payload, 403 missing permission, 404 not found, 409 concurrent change,
/// 422 business rule (insufficient available stock, invalid status transition, self-approval...).
/// </summary>
[ApiController]
[Authorize]
[Route("api/goods-issues")]
[Produces("application/json")]
public sealed class GoodsIssuesController(IGoodsIssueService service) : ControllerBase
{
    [HttpGet]
    [WarehousePermission(Permissions.GoodsIssueView)]
    [ProducesResponseType<PagedResult<GoodsIssueSummary>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<GoodsIssueSummary>>> Search(
        [FromQuery] GoodsIssueQuery query,
        CancellationToken cancellationToken) =>
        Ok(await service.SearchAsync(query, cancellationToken));

    [HttpGet("{id:int}")]
    [WarehousePermission(Permissions.GoodsIssueView, WarehouseResource.Issue)]
    [ProducesResponseType<GoodsIssueDetail>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GoodsIssueDetail>> Get(int id, CancellationToken cancellationToken) =>
        Ok(await service.GetAsync(id, cancellationToken));

    /// <summary>Creates a Draft issue (quantities checked against current available stock).</summary>
    [HttpPost]
    [WarehousePermission(Permissions.GoodsIssueCreate)]
    [ProducesResponseType<GoodsIssueDetail>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<GoodsIssueDetail>> Create(CreateGoodsIssueRequest request, CancellationToken cancellationToken)
    {
        var issue = await service.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = issue.Id }, issue);
    }

    [HttpPut("{id:int}")]
    [WarehousePermission(Permissions.GoodsIssueCreate, WarehouseResource.Issue)]
    [ProducesResponseType<GoodsIssueDetail>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<GoodsIssueDetail>> Update(int id, UpdateGoodsIssueRequest request, CancellationToken cancellationToken) =>
        Ok(await service.UpdateAsync(id, request, cancellationToken));

    [HttpDelete("{id:int}")]
    [WarehousePermission(Permissions.GoodsIssueCreate, WarehouseResource.Issue)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await service.DeleteAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:int}/submit")]
    [WarehousePermission(Permissions.GoodsIssueCreate, WarehouseResource.Issue)]
    [ProducesResponseType<GoodsIssueDetail>(StatusCodes.Status200OK)]
    public async Task<ActionResult<GoodsIssueDetail>> Submit(int id, CancellationToken cancellationToken) =>
        Ok(await service.SubmitAsync(id, cancellationToken));

    /// <summary>PendingApproval → Approved; reserves stock for every line or fails with 422 (nothing reserved).</summary>
    [HttpPost("{id:int}/approve")]
    [WarehousePermission(Permissions.GoodsIssueApprove, WarehouseResource.Issue)]
    [ProducesResponseType<GoodsIssueDetail>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<GoodsIssueDetail>> Approve(int id, CancellationToken cancellationToken) =>
        Ok(await service.ApproveAsync(id, cancellationToken));

    [HttpPost("{id:int}/reject")]
    [WarehousePermission(Permissions.GoodsIssueApprove, WarehouseResource.Issue)]
    [ProducesResponseType<GoodsIssueDetail>(StatusCodes.Status200OK)]
    public async Task<ActionResult<GoodsIssueDetail>> Reject(int id, DocumentReasonRequest request, CancellationToken cancellationToken) =>
        Ok(await service.RejectAsync(id, request.Reason, cancellationToken));

    [HttpPost("{id:int}/reopen")]
    [WarehousePermission(Permissions.GoodsIssueCreate, WarehouseResource.Issue)]
    [ProducesResponseType<GoodsIssueDetail>(StatusCodes.Status200OK)]
    public async Task<ActionResult<GoodsIssueDetail>> Reopen(int id, CancellationToken cancellationToken) =>
        Ok(await service.ReopenAsync(id, cancellationToken));

    /// <summary>Approved → Completed: decreases stock and reservation, writes ISSUE movements and audit.</summary>
    [HttpPost("{id:int}/complete")]
    [WarehousePermission(Permissions.GoodsIssueComplete, WarehouseResource.Issue)]
    [ProducesResponseType<GoodsIssueDetail>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<GoodsIssueDetail>> Complete(int id, CancellationToken cancellationToken) =>
        Ok(await service.CompleteAsync(id, cancellationToken));

    /// <summary>Cancels (reason required). Cancelling an approved issue releases its reservation.</summary>
    [HttpPost("{id:int}/cancel")]
    [WarehousePermission(Permissions.GoodsIssueCreate, WarehouseResource.Issue)]
    [ProducesResponseType<GoodsIssueDetail>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<GoodsIssueDetail>> Cancel(int id, DocumentReasonRequest request, CancellationToken cancellationToken) =>
        Ok(await service.CancelAsync(id, request.Reason, cancellationToken));
}
