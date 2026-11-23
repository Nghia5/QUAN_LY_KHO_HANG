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
            .Select(product => ToResponse(product))
            .ToListAsync(cancellationToken);
    }

    public async Task<ProductResponse?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken)
    {
        var product = await dbContext.Products
            .AsNoTracking()
            .SingleOrDefaultAsync(product => product.Id == id, cancellationToken);

        return product is null ? null : ToResponse(product);
    }

    public async Task<ProductResponse> CreateAsync(
        CreateProductRequest request,
        CancellationToken cancellationToken)
    {
        var product = new Product
        {
            Sku = request.Sku.Trim(),
            Name = request.Name.Trim(),
            Description = request.Description?.Trim(),
            Category = request.Category.Trim(),
            UnitOfMeasure = request.UnitOfMeasure.Trim(),
            SalePrice = request.SalePrice,
            IsActive = request.IsActive
        };

        dbContext.Products.Add(product);
        await SaveChangesAsync(cancellationToken);

        return ToResponse(product);
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

        product.Sku = request.Sku.Trim();
        product.Name = request.Name.Trim();
        product.Description = request.Description?.Trim();
        product.Category = request.Category.Trim();
        product.UnitOfMeasure = request.UnitOfMeasure.Trim();
        product.SalePrice = request.SalePrice;
        product.IsActive = request.IsActive;

        await SaveChangesAsync(cancellationToken);

        return ToResponse(product);
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
        await dbContext.SaveChangesAsync(cancellationToken);

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

    private static ProductResponse ToResponse(Product product) =>
        new(
            product.Id,
            product.Sku,
            product.Name,
            product.Description,
            product.Category,
            product.UnitOfMeasure,
            product.SalePrice,
            product.IsActive);
}
