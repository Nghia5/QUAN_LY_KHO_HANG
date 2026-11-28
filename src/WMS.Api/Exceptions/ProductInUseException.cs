namespace WMS.Api.Exceptions;

public sealed class ProductInUseException()
    : Exception("The product is referenced by other records and cannot be deleted.");
