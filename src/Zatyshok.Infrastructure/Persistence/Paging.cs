using Microsoft.EntityFrameworkCore;

namespace Zatyshok.Infrastructure.Persistence;

/// <summary>Which page of a list to read: <paramref name="Page"/> starts at 1 (the API validates both values).</summary>
public record PageRequest(int Page, int PageSize);

/// <summary>One page of a list and the number of rows in the whole list (for the page count in the client).</summary>
public record PagedList<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);

public static class PagingExtensions
{
    /// <summary>
    /// Counts the rows of the query and reads one page of it. The query must be ordered: without
    /// ORDER BY PostgreSQL may return the rows of two pages in different orders, and a row would
    /// then appear on both pages or on none.
    /// </summary>
    public static async Task<PagedList<T>> ToPagedListAsync<T>(
        this IOrderedQueryable<T> query, PageRequest page, CancellationToken cancellationToken)
    {
        var totalCount = await query.CountAsync(cancellationToken);
        // checked: an overflow throws here instead of sending a negative OFFSET to PostgreSQL. The API
        // keeps the page number small enough (PageQuery.MaxPage), so this is only a safety net.
        var items = await query
            .Skip(checked((page.Page - 1) * page.PageSize))
            .Take(page.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedList<T>(items, page.Page, page.PageSize, totalCount);
    }
}
