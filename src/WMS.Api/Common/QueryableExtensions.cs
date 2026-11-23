using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using WMS.Api.Dtos.Common;

namespace WMS.Api.Common;

public static class QueryableExtensions
{
    /// <summary>
    /// Sorts by a whitelisted key. Unknown keys fall back to <paramref name="defaultSort"/>;
    /// <paramref name="tieBreaker"/> keeps paging stable.
    /// </summary>
    public static IOrderedQueryable<T> ApplySort<T>(
        this IQueryable<T> query,
        PagedRequest request,
        IReadOnlyDictionary<string, Expression<Func<T, object?>>> sortMap,
        string defaultSort,
        Expression<Func<T, object?>> tieBreaker)
    {
        var key = request.SortBy is not null && sortMap.ContainsKey(request.SortBy) ? request.SortBy : defaultSort;
        var selector = sortMap[key];
        var ordered = request.IsDescending ? query.OrderByDescending(selector) : query.OrderBy(selector);
        return ordered.ThenBy(tieBreaker);
    }

    public static async Task<PagedResult<T>> ToPagedResultAsync<T>(
        this IQueryable<T> query,
        PagedRequest request,
        CancellationToken cancellationToken)
    {
        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<T>(items, request.Page, request.PageSize, totalCount);
    }

    public static Dictionary<string, Expression<Func<T, object?>>> SortMap<T>(
        params (string Key, Expression<Func<T, object?>> Selector)[] entries) =>
        entries.ToDictionary(entry => entry.Key, entry => entry.Selector, StringComparer.OrdinalIgnoreCase);
}
