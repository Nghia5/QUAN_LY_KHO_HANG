namespace WMS.Api.Exceptions;

public sealed class ProductSkuConflictException()
    : Exception("A product with this SKU already exists.");
