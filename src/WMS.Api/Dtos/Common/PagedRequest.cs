namespace WMS.Api.Dtos.Common;

/// <summary>
/// Common list query (paging, keyword, sorting). Temporary until the shared Common Query DTO
/// from the platform module is merged; property names follow the agreed contract.
/// </summary>
public record PagedRequest
{
    public const int MaxPageSize = 100;

    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = 20;

    public string? Keyword { get; init; }

    public string? SortBy { get; init; }

    /// <summary>"asc" or "desc".</summary>
    public string? SortDir { get; init; }

    public bool IsDescending => string.Equals(SortDir, "desc", StringComparison.OrdinalIgnoreCase);

    internal long Offset => ((long)Page - 1) * PageSize;
}

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}
