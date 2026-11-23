using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using WMS.Api.Data;

namespace WMS.Api.Services.Documents;

/// <summary>Document number prefixes and the SQL Server sequence behind each of them.</summary>
public static class DocumentTypes
{
    public const string GoodsReceipt = "GOODS_RECEIPT";
    public const string GoodsIssue = "GOODS_ISSUE";
    public const string StockTransfer = "STOCK_TRANSFER";
    public const string Stocktake = "STOCKTAKE";

    /// <summary>(reference type, prefix, sequence name)</summary>
    public static readonly IReadOnlyList<(string Type, string Prefix, string Sequence)> All =
    [
        (GoodsReceipt, "GR", "GoodsReceiptNumbers"),
        (GoodsIssue, "GI", "GoodsIssueNumbers"),
        (StockTransfer, "TR", "StockTransferNumbers"),
        (Stocktake, "ST", "StocktakeNumbers")
    ];
}

public interface IDocumentNumberGenerator
{
    /// <summary>Next number like "GR-2026-000001" (sequence is global, never reused).</summary>
    Task<string> NextAsync(string documentType, CancellationToken cancellationToken);
}

public sealed class DocumentNumberGenerator(WmsDbContext dbContext, TimeProvider timeProvider) : IDocumentNumberGenerator
{
    public async Task<string> NextAsync(string documentType, CancellationToken cancellationToken)
    {
        var (_, prefix, sequence) = DocumentTypes.All.Single(type => type.Type == documentType);

        // NEXT VALUE FOR is not allowed inside the sub-query EF builds for SqlQuery, so read it
        // through an output parameter. The sequence name comes from the constant table above.
        var value = new SqlParameter("@value", SqlDbType.BigInt) { Direction = ParameterDirection.Output };
        await dbContext.Database.ExecuteSqlRawAsync(
            $"SET @value = NEXT VALUE FOR [dbo].[{sequence}];", [value], cancellationToken);

        return $"{prefix}-{timeProvider.GetUtcNow().Year}-{(long)value.Value:000000}";
    }
}
