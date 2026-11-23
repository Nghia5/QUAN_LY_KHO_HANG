using WMS.Api.Dtos.Common;
using WMS.Api.Dtos.Documents;
using WMS.Api.Models.Documents;
using WMS.Api.Services.Documents;

namespace WMS.Api.Dtos.GoodsIssues;

public sealed record GoodsIssueLineRequest
{
    public int ProductId { get; init; }

    /// <summary>Source Bin in the issue's warehouse.</summary>
    public int LocationId { get; init; }

    public decimal Quantity { get; init; }

    public string? Note { get; init; }
}

/// <summary>Fields shared by create and update requests.</summary>
public interface IGoodsIssuePayload
{
    int? CustomerId { get; }

    string? CustomerName { get; }

    string? ReferenceNo { get; }

    DateOnly? IssueDate { get; }

    string? Note { get; }

    IReadOnlyList<GoodsIssueLineRequest> Lines { get; }
}

/// <summary>Creates a Draft issue. The warehouse cannot be changed afterwards.</summary>
public sealed record CreateGoodsIssueRequest : IGoodsIssuePayload
{
    public int WarehouseId { get; init; }

    public int? CustomerId { get; init; }

    public string? CustomerName { get; init; }

    public string? ReferenceNo { get; init; }

    /// <summary>Defaults to today (UTC).</summary>
    public DateOnly? IssueDate { get; init; }

    public string? Note { get; init; }

    public IReadOnlyList<GoodsIssueLineRequest> Lines { get; init; } = [];
}

/// <summary>Replaces header fields and all lines of a Draft issue.</summary>
public sealed record UpdateGoodsIssueRequest : IGoodsIssuePayload
{
    public int? CustomerId { get; init; }

    public string? CustomerName { get; init; }

    public string? ReferenceNo { get; init; }

    public DateOnly? IssueDate { get; init; }

    public string? Note { get; init; }

    public IReadOnlyList<GoodsIssueLineRequest> Lines { get; init; } = [];
}

/// <summary>Keyword matches document number, reference number and customer name.</summary>
public sealed record GoodsIssueQuery : PagedRequest
{
    public static readonly string[] SortableFields = ["documentNo", "issueDate", "status", "createdAt", "totalQuantity"];

    public int? WarehouseId { get; init; }

    public DocumentStatus? Status { get; init; }

    public int? CustomerId { get; init; }

    public int? ProductId { get; init; }

    public DateOnly? FromDate { get; init; }

    public DateOnly? ToDate { get; init; }
}

public sealed record GoodsIssueSummary(
    int Id,
    string DocumentNo,
    int WarehouseId,
    string WarehouseCode,
    DocumentStatus Status,
    int? CustomerId,
    string? CustomerName,
    string? ReferenceNo,
    DateOnly IssueDate,
    int LineCount,
    decimal TotalQuantity,
    int? CreatedBy,
    DateTime CreatedAt);

/// <param name="AvailableQuantity">Current available stock at the source bin (live, for the UI).</param>
public sealed record GoodsIssueLineResponse(
    int Id,
    int ProductId,
    string ProductSku,
    string ProductName,
    string UnitOfMeasure,
    int LocationId,
    string LocationCode,
    decimal Quantity,
    decimal AvailableQuantity,
    string? Note);

public sealed record GoodsIssueDetail(
    int Id,
    string DocumentNo,
    int WarehouseId,
    string WarehouseCode,
    string WarehouseName,
    DocumentStatus Status,
    int? CustomerId,
    string? CustomerName,
    string? ReferenceNo,
    DateOnly IssueDate,
    string? Note,
    decimal TotalQuantity,
    IReadOnlyList<GoodsIssueLineResponse> Lines,
    DocumentWorkflowInfo Workflow,
    IReadOnlyList<DocumentAction> AvailableActions);
