using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WMS.Api.Dtos.Suppliers;
using WMS.Api.Services;

namespace WMS.Api.Controllers;

[ApiController]
[Authorize(Policy = WMS.Api.Authorization.ActiveAdminRequirement.PolicyName)]
[Route("api/suppliers")]
public class SuppliersController(ISupplierService supplierService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<SupplierResponseDto>>> GetAll(
        [FromQuery] string? keyword,
        [FromQuery] bool? isActive,
        CancellationToken cancellationToken) =>
        Ok(await supplierService.GetAllAsync(keyword, isActive, cancellationToken));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<SupplierResponseDto>> GetById(
        int id,
        CancellationToken cancellationToken)
    {
        var result = await supplierService.GetByIdAsync(id, cancellationToken);
        if (!result.Success)
        {
            return this.ToErrorResult(result);
        }

        return Ok(result.Value);
    }

    [HttpPost]
    public async Task<ActionResult<SupplierResponseDto>> Create(
        CreateSupplierRequestDto request,
        CancellationToken cancellationToken)
    {
        var result = await supplierService.CreateAsync(request, cancellationToken);
        if (!result.Success)
        {
            return this.ToErrorResult(result);
        }

        return CreatedAtAction(nameof(GetById), new { id = result.Value!.Id }, result.Value);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<SupplierResponseDto>> Update(
        int id,
        UpdateSupplierRequestDto request,
        CancellationToken cancellationToken)
    {
        var result = await supplierService.UpdateAsync(id, request, cancellationToken);
        if (!result.Success)
        {
            return this.ToErrorResult(result);
        }

        return Ok(result.Value);
    }

    [HttpDelete("{id:int}")]
    public async Task<ActionResult<SupplierResponseDto>> Deactivate(
        int id,
        CancellationToken cancellationToken)
    {
        var result = await supplierService.DeactivateAsync(id, cancellationToken);
        if (!result.Success)
        {
            return this.ToErrorResult(result);
        }

        return Ok(result.Value);
    }
}
