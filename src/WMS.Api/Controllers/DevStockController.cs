using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WMS.Api.Models;
using WMS.Api.Services.Inventories;

namespace WMS.Api.Controllers;

/// <summary>
/// DEVELOPMENT ONLY: applies a stock change through the real <see cref="IStockLedger"/> so the
/// ledger can be tried from Swagger before goods receipt/issue documents exist (W-07+).
/// Movements are tagged ReferenceType = DEV_TEST. Returns 404 outside Development.
/// </summary>
[ApiController]
[Route("api/dev/stock-changes")]
[Authorize]
[Produces("application/json")]
public sealed class DevStockController(IWebHostEnvironment environment, IStockLedger ledger) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType<IReadOnlyList<DevStockChangeResult>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<DevStockChangeResult>>> Apply(
        IReadOnlyList<DevStockChangeRequest> changes,
        CancellationToken cancellationToken)
    {
        if (!environment.IsDevelopment())
        {
            return NotFound();
        }

        var movements = await ledger.ApplyAsync(
            changes.Select(c => new StockChange(c.ProductId, c.WarehouseId, c.LocationId, c.Quantity, c.MovementType, c.Note))
                .ToList(),
            new StockReference("DEV_TEST", null, $"DEV-{DateTime.UtcNow:yyyyMMddHHmmss}"),
            cancellationToken);

        return Ok(movements
            .Select(m => new DevStockChangeResult(m.Id, m.InventoryId, m.MovementType, m.Quantity, m.QuantityBefore, m.QuantityAfter))
            .ToList());
    }
}

/// <param name="Quantity">Signed: e.g. 50 with AdjustmentIn, -5 with Issue.</param>
public sealed record DevStockChangeRequest(
    int ProductId,
    int WarehouseId,
    int LocationId,
    decimal Quantity,
    MovementType MovementType,
    string? Note);

public sealed record DevStockChangeResult(
    long MovementId,
    int InventoryId,
    MovementType MovementType,
    decimal Quantity,
    decimal QuantityBefore,
    decimal QuantityAfter);
