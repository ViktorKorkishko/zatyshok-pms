using FluentValidation;
using Zatyshok.Api.Common.Paging;
using Zatyshok.Api.Common.Validation;
using Zatyshok.Domain.Modules.Rooms;
using Zatyshok.Infrastructure.Modules.Rooms;

namespace Zatyshok.Api.Modules.Rooms;

/// <summary>Категорія номерів мережі для створення або повної зміни (FR-05).</summary>
public sealed class RoomCategoryRequest
{
    /// <summary>Назва категорії, унікальна в мережі, до 100 символів.</summary>
    /// <example>Family</example>
    public string Name { get; init; } = string.Empty;

    /// <summary>Опис для гостей, до 1000 символів; необов'язковий.</summary>
    /// <example>Дві кімнати, двоспальне ліжко і диван.</example>
    public string? Description { get; init; }

    /// <summary>Базова місткість: скільки гостей зазвичай вміщує номер категорії.</summary>
    /// <example>4</example>
    public int Capacity { get; init; }

    internal RoomCategoryData ToData() => new(Name, Description, Capacity);
}

/// <summary>Категорія номерів мережі.</summary>
public sealed class RoomCategoryResponse
{
    /// <summary>Ідентифікатор категорії.</summary>
    /// <example>11111111-1111-1111-1111-111111111101</example>
    public Guid Id { get; init; }

    /// <summary>Назва категорії.</summary>
    /// <example>Standard</example>
    public required string Name { get; init; }

    /// <summary>Опис для гостей.</summary>
    /// <example>Стандартний номер з одним двоспальним або двома односпальними ліжками</example>
    public string? Description { get; init; }

    /// <summary>Базова місткість.</summary>
    /// <example>2</example>
    public int Capacity { get; init; }

    /// <summary>Чи діє категорія; деактивовану не можна обрати для нових номерів і тарифів.</summary>
    /// <example>true</example>
    public bool IsActive { get; init; }

    internal static RoomCategoryResponse From(RoomCategory category) => new()
    {
        Id = category.Id,
        Name = category.Name,
        Description = category.Description,
        Capacity = category.Capacity,
        IsActive = category.IsActive,
    };
}

/// <summary>Фільтри списку категорій.</summary>
public sealed class RoomCategoryListQuery : PageQuery
{
    /// <summary>true — лише діючі категорії, false — лише деактивовані; не вказано — усі.</summary>
    public bool? IsActive { get; init; }
}

/// <summary>Rules of <see cref="RoomCategoryRequest"/>; the lengths are those of rooms.RoomCategories.</summary>
public sealed class RoomCategoryRequestValidator : RequestValidator<RoomCategoryRequest>
{
    /// <summary>Creates the rules.</summary>
    public RoomCategoryRequestValidator()
    {
        RuleFor(c => c.Name)
            .NotEmpty().WithMessage("Вкажіть назву категорії.")
            .MaximumLength(100).WithMessage("Назва категорії має бути не довшою за 100 символів.");
        RuleFor(c => c.Description)
            .MaximumLength(1000).WithMessage("Опис має бути не довшим за 1000 символів.");
        RuleFor(c => c.Capacity)
            .GreaterThan(0).WithMessage("Місткість має бути більшою за 0.");
    }
}

/// <summary>Rules of <see cref="RoomCategoryListQuery"/>.</summary>
public sealed class RoomCategoryListQueryValidator : RequestValidator<RoomCategoryListQuery>
{
    /// <summary>Creates the rules.</summary>
    public RoomCategoryListQueryValidator()
    {
        Include(new PageQueryValidator());
    }
}
