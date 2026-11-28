using Zatyshok.Infrastructure.Persistence;

namespace Zatyshok.Api.Common.Paging;

/// <summary>Одна сторінка списку.</summary>
/// <typeparam name="T">Item DTO.</typeparam>
public sealed class PagedResponse<T>
{
    /// <summary>Записи цієї сторінки; після останньої сторінки список порожній.</summary>
    public required IReadOnlyList<T> Items { get; init; }

    /// <summary>Номер сторінки, починаючи з 1.</summary>
    /// <example>1</example>
    public int Page { get; init; }

    /// <summary>Кількість записів на сторінці, як у запиті.</summary>
    /// <example>20</example>
    public int PageSize { get; init; }

    /// <summary>Кількість записів у всьому списку з тими самими фільтрами.</summary>
    /// <example>25</example>
    public int TotalCount { get; init; }

    /// <summary>Кількість сторінок; 0 для порожнього списку.</summary>
    /// <example>2</example>
    public int TotalPages => (TotalCount + PageSize - 1) / PageSize;

    /// <summary>Converts a page of entities read by a service into a page of DTOs.</summary>
    public static PagedResponse<T> From<TEntity>(PagedList<TEntity> page, Func<TEntity, T> map) => new()
    {
        Items = page.Items.Select(map).ToList(),
        Page = page.Page,
        PageSize = page.PageSize,
        TotalCount = page.TotalCount,
    };
}
