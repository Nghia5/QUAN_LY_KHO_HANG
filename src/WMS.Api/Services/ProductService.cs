using System.Linq.Expressions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using WMS.Api.Data;
using WMS.Api.Dtos;
using WMS.Api.Exceptions;
using WMS.Api.Models;

namespace WMS.Api.Services;

public sealed class ProductService(WmsDbContext dbContext) : IProductService
{
    public async Task<IReadOnlyList<ProductResponse>> GetAllAsync(
        CancellationToken cancellationToken)
    {
        return await dbContext.Products
            .AsNoTracking()
            .OrderBy(product => product.Name)
            .Select(Projection)
            .ToListAsync(cancellationToken);
    }

    public async Task<ProductResponse?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken)
    {
        return await dbContext.Products
            .AsNoTracking()
            .Where(product => product.Id == id)
            .Select(Projection)
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<ProductResponse> CreateAsync(
        CreateProductRequest request,
        CancellationToken cancellationToken)
    {
        await EnsureSupplierUsableAsync(request.SupplierId, null, cancellationToken);

        var product = new Product
        {
            Sku = request.Sku.Trim(),
            Name = request.Name.Trim(),
            Description = request.Description?.Trim(),
            Category = request.Category.Trim(),
            UnitOfMeasure = request.UnitOfMeasure.Trim(),
            SalePrice = request.SalePrice,
            IsActive = request.IsActive,
            SupplierId = request.SupplierId
        };

        dbContext.Products.Add(product);
        await SaveChangesAsync(cancellationToken);

        return (await GetByIdAsync(product.Id, cancellationToken))!;
    }

    public async Task<ProductResponse?> UpdateAsync(
        int id,
        UpdateProductRequest request,
        CancellationToken cancellationToken)
    {
        var product = await dbContext.Products
            .SingleOrDefaultAsync(product => product.Id == id, cancellationToken);

        if (product is null)
        {
            return null;
        }

        await EnsureSupplierUsableAsync(request.SupplierId, product.SupplierId, cancellationToken);

        product.Sku = request.Sku.Trim();
        product.Name = request.Name.Trim();
        product.Description = request.Description?.Trim();
        product.Category = request.Category.Trim();
        product.UnitOfMeasure = request.UnitOfMeasure.Trim();
        product.SalePrice = request.SalePrice;
        product.IsActive = request.IsActive;
        product.SupplierId = request.SupplierId;

        await SaveChangesAsync(cancellationToken);

        return await GetByIdAsync(id, cancellationToken);
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken)
    {
        var product = await dbContext.Products
            .SingleOrDefaultAsync(product => product.Id == id, cancellationToken);

        if (product is null)
        {
            return false;
        }

        dbContext.Products.Remove(product);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (exception.GetBaseException() is SqlException { Number: 547 })
        {
            throw new ProductInUseException();
        }

        return true;
    }

    private async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (exception.GetBaseException() is SqlException { Number: 2601 or 2627 })
        {
            throw new ProductSkuConflictException();
        }
    }

    /// <summary>
    /// A newly chosen supplier must exist and be active. Keeping the current supplier is always
    /// allowed, so products of a supplier that was deactivated later can still be edited.
    /// </summary>
    private async Task EnsureSupplierUsableAsync(int? supplierId, int? currentSupplierId, CancellationToken cancellationToken)
    {
        if (supplierId is not int id || id == currentSupplierId)
        {
            return;
        }

        var supplier = await dbContext.Set<SupplierModel>()
            .AsNoTracking()
            .Where(s => s.Id == id)
            .Select(s => new { s.Code, s.IsActive })
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new BusinessRuleException($"Supplier {id} does not exist.");

        if (!supplier.IsActive)
        {
            throw new BusinessRuleException($"Supplier {supplier.Code} is inactive.");
        }
    }

    private static readonly Expression<Func<Product, ProductResponse>> Projection = product =>
        new ProductResponse(
            product.Id,
            product.Sku,
            product.Name,
            product.Description,
            product.Category,
            product.UnitOfMeasure,
            product.SalePrice,
            product.IsActive,
            product.SupplierId,
            product.Supplier == null ? null : product.Supplier.Code,
            product.Supplier == null ? null : product.Supplier.Name);
}
