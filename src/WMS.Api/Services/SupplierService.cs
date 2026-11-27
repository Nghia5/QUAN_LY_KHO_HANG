using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using WMS.Api.Dtos.Suppliers;
using WMS.Api.Models;
using WMS.Api.Repositories;

namespace WMS.Api.Services;

public interface ISupplierService
{
    Task<List<SupplierResponseDto>> GetAllAsync(
        string? keyword,
        bool? isActive,
        CancellationToken cancellationToken);
    Task<ServiceResult<SupplierResponseDto>> GetByIdAsync(int id, CancellationToken cancellationToken);
    Task<ServiceResult<SupplierResponseDto>> CreateAsync(
        CreateSupplierRequestDto request,
        CancellationToken cancellationToken);
    Task<ServiceResult<SupplierResponseDto>> UpdateAsync(
        int id,
        UpdateSupplierRequestDto request,
        CancellationToken cancellationToken);
    Task<ServiceResult<SupplierResponseDto>> DeactivateAsync(int id, CancellationToken cancellationToken);
}

public class SupplierService(ISupplierRepository repository) : ISupplierService
{
    public async Task<List<SupplierResponseDto>> GetAllAsync(
        string? keyword,
        bool? isActive,
        CancellationToken cancellationToken) =>
        (await repository.GetAllAsync(keyword, isActive, cancellationToken))
        .Select(ToResponse)
        .ToList();

    public async Task<ServiceResult<SupplierResponseDto>> GetByIdAsync(
        int id,
        CancellationToken cancellationToken)
    {
        var supplier = await repository.GetByIdAsync(id, cancellationToken);
        return supplier is null
            ? ServiceResult<SupplierResponseDto>.NotFound("Không tìm thấy nhà cung cấp.")
            : ServiceResult<SupplierResponseDto>.Ok(ToResponse(supplier));
    }

    public async Task<ServiceResult<SupplierResponseDto>> CreateAsync(
        CreateSupplierRequestDto request,
        CancellationToken cancellationToken)
    {
        var code = request.Code.Trim().ToUpperInvariant();
        if (await repository.CodeExistsAsync(code, null, cancellationToken))
        {
            return Conflict(code);
        }

        var supplier = new SupplierModel
        {
            Code = code,
            Name = request.Name.Trim(),
            Phone = TextHelper.Clean(request.Phone),
            Email = TextHelper.Clean(request.Email),
            Address = TextHelper.Clean(request.Address)
        };
        await repository.AddAsync(supplier, cancellationToken);

        try
        {
            await repository.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (exception.GetBaseException() is SqlException { Number: 2601 or 2627 })
        {
            return Conflict(code);
        }

        return ServiceResult<SupplierResponseDto>.Ok(ToResponse(supplier));
    }

    public async Task<ServiceResult<SupplierResponseDto>> UpdateAsync(
        int id,
        UpdateSupplierRequestDto request,
        CancellationToken cancellationToken)
    {
        var supplier = await repository.GetByIdAsync(id, cancellationToken);
        if (supplier is null)
        {
            return ServiceResult<SupplierResponseDto>.NotFound("Không tìm thấy nhà cung cấp.");
        }

        var code = request.Code.Trim().ToUpperInvariant();
        if (await repository.CodeExistsAsync(code, id, cancellationToken))
        {
            return Conflict(code);
        }

        supplier.Code = code;
        supplier.Name = request.Name.Trim();
        supplier.Phone = TextHelper.Clean(request.Phone);
        supplier.Email = TextHelper.Clean(request.Email);
        supplier.Address = TextHelper.Clean(request.Address);
        supplier.IsActive = request.IsActive;
        supplier.UpdatedAt = DateTime.UtcNow;

        try
        {
            await repository.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (exception.GetBaseException() is SqlException { Number: 2601 or 2627 })
        {
            return Conflict(code);
        }

        return ServiceResult<SupplierResponseDto>.Ok(ToResponse(supplier));
    }

    public async Task<ServiceResult<SupplierResponseDto>> DeactivateAsync(
        int id,
        CancellationToken cancellationToken)
    {
        var supplier = await repository.GetByIdAsync(id, cancellationToken);
        if (supplier is null)
        {
            return ServiceResult<SupplierResponseDto>.NotFound("Không tìm thấy nhà cung cấp.");
        }

        supplier.IsActive = false;
        supplier.UpdatedAt = DateTime.UtcNow;
        await repository.SaveChangesAsync(cancellationToken);

        return ServiceResult<SupplierResponseDto>.Ok(ToResponse(supplier));
    }

    private static ServiceResult<SupplierResponseDto> Conflict(string code) =>
        ServiceResult<SupplierResponseDto>.Conflict($"Mã nhà cung cấp '{code}' đã tồn tại.");

    private static SupplierResponseDto ToResponse(SupplierModel supplier) => new()
    {
        Id = supplier.Id,
        Code = supplier.Code,
        Name = supplier.Name,
        Phone = supplier.Phone,
        Email = supplier.Email,
        Address = supplier.Address,
        IsActive = supplier.IsActive,
        CreatedAt = supplier.CreatedAt,
        UpdatedAt = supplier.UpdatedAt
    };
}
