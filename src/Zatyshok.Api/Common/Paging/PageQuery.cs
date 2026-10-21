using FluentValidation;
using Zatyshok.Api.Common.Validation;
using Zatyshok.Infrastructure.Persistence;

namespace Zatyshok.Api.Common.Paging;

// XML comments of the types that Swagger shows (requests, responses, controllers) are the API
// documentation for the users of the API, so they are written in Ukrainian (NFR-13).

/// <summary>
/// Параметри сторінки для кожного списку. Запит списку ресурсу успадковує цей клас і додає свої
/// фільтри, тож усі списки діляться на сторінки однаково.
/// </summary>
public class PageQuery
{
    /// <summary>Largest page; a client that needs more rows reads the next page.</summary>
    public const int MaxPageSize = 100;

    /// <summary>Номер сторінки, починаючи з 1.</summary>
    /// <example>1</example>
    public int Page { get; init; } = 1;

    /// <summary>Кількість записів на сторінці, від 1 до 100.</summary>
    /// <example>20</example>
    public int PageSize { get; init; } = 20;

    /// <summary>The paging part of the query for a service.</summary>
    public PageRequest ToPageRequest() => new(Page, PageSize);
}

/// <summary>Rules of <see cref="PageQuery"/>; validators of list queries include them with <c>Include(new PageQueryValidator())</c>.</summary>
public class PageQueryValidator : RequestValidator<PageQuery>
{
    /// <summary>Creates the rules.</summary>
    public PageQueryValidator()
    {
        RuleFor(q => q.Page)
            .GreaterThanOrEqualTo(1).WithMessage("Номер сторінки починається з 1.");
        RuleFor(q => q.PageSize)
            .InclusiveBetween(1, PageQuery.MaxPageSize).WithMessage($"Розмір сторінки має бути від 1 до {PageQuery.MaxPageSize}.");
    }
}
