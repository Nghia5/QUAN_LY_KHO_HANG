namespace WMS.Api.Models.Documents;

/// <summary>
/// Goods issue (phiếu xuất kho): Draft → PendingApproval → Approved (stock reserved) →
/// Completed (stock and reservation decreased, ISSUE movements written).
/// Cancelling an approved issue releases its reservation.
/// </summary>
public sealed class GoodsIssue : WarehouseDocument
{
    /// <summary>Customer id; FK/validation added when a Customer module exists.</summary>
    public int? CustomerId { get; set; }

    public string? CustomerName { get; set; }

    /// <summary>Sales order / delivery request number.</summary>
    public string? ReferenceNo { get; set; }

    public DateOnly IssueDate { get; set; }

    public List<GoodsIssueLine> Lines { get; set; } = [];
}

public sealed class GoodsIssueLine
{
    public int Id { get; set; }

    public int GoodsIssueId { get; set; }

    /// <summary>Copied from the header; composite FKs keep header, line and location in one warehouse.</summary>
    public int WarehouseId { get; set; }

    public int ProductId { get; set; }

    /// <summary>Source Bin.</summary>
    public int LocationId { get; set; }

    /// <summary>Always &gt; 0.</summary>
    public decimal Quantity { get; set; }

    public string? Note { get; set; }

    public Product? Product { get; set; }

    public WarehouseLocation? Location { get; set; }
}
