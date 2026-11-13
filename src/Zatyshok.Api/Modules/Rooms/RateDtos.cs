using FluentValidation;
using Zatyshok.Api.Common.Paging;
using Zatyshok.Api.Common.Validation;
using Zatyshok.Domain.Modules.Rooms;

namespace Zatyshok.Api.Modules.Rooms;

/// <summary>Новий тариф: базова ціна за ніч для пари «готель – категорія» (FR-08).</summary>
public sealed class NewRateRequest
{
    /// <summary>Готель.</summary>
    /// <example>22222222-2222-2222-2222-222222222201</example>
    public Guid HotelId { get; init; }

    /// <summary>Категорія номерів; для пари може бути лише один тариф.</summary>
    /// <example>11111111-1111-1111-1111-111111111101</example>
    public Guid RoomCategoryId { get; init; }

    /// <summary>Базова ціна за ніч у гривнях, більша за 0, до копійки.</summary>
    /// <example>2500.00</example>
    public decimal BasePricePerNight { get; init; }
}

/// <summary>Нова базова ціна тарифу. Готель і категорія тарифу не змінюються.</summary>
public sealed class RateRequest
{
    /// <summary>Базова ціна за ніч у гривнях, більша за 0, до копійки.</summary>
    /// <example>2700.00</example>
    public decimal BasePricePerNight { get; init; }
}

/// <summary>Тариф пари «готель – категорія».</summary>
public sealed class RateResponse
{
    /// <summary>Ідентифікатор тарифу.</summary>
    /// <example>44444444-4444-4444-4444-444444440000</example>
    public Guid Id { get; init; }

    /// <summary>Готель.</summary>
    /// <example>22222222-2222-2222-2222-222222222201</example>
    public Guid HotelId { get; init; }

    /// <summary>Категорія номерів.</summary>
    /// <example>11111111-1111-1111-1111-111111111101</example>
    public Guid RoomCategoryId { get; init; }

    /// <summary>Назва категорії.</summary>
    /// <example>Standard</example>
    public required string RoomCategoryName { get; init; }

    /// <summary>Базова ціна за ніч у гривнях; на ночі сезонних періодів діє ціна періоду.</summary>
    /// <example>2500.00</example>
    public decimal BasePricePerNight { get; init; }

    internal static RateResponse From(Rate rate) => new()
    {
        Id = rate.Id,
        HotelId = rate.HotelId,
        RoomCategoryId = rate.RoomCategoryId,
        RoomCategoryName = rate.Category.Name,
        BasePricePerNight = rate.BasePricePerNight,
    };
}

/// <summary>Фільтри списку тарифів.</summary>
public sealed class RateListQuery : PageQuery
{
    /// <summary>Лише тарифи цього готелю.</summary>
    /// <example>22222222-2222-2222-2222-222222222201</example>
    public Guid? HotelId { get; init; }

    /// <summary>Лише тарифи цієї категорії.</summary>
    public Guid? RoomCategoryId { get; init; }
}

/// <summary>Rules of <see cref="NewRateRequest"/>.</summary>
public sealed class NewRateRequestValidator : RequestValidator<NewRateRequest>
{
    /// <summary>Creates the rules.</summary>
    public NewRateRequestValidator()
    {
        RuleFor(r => r.HotelId)
            .NotEmpty().WithMessage("Вкажіть готель.");
        RuleFor(r => r.RoomCategoryId)
            .NotEmpty().WithMessage("Вкажіть категорію номерів.");
        RuleFor(r => r.BasePricePerNight)
            .GreaterThan(0).WithMessage("Ціна за ніч має бути більшою за 0.")
            .MoneyAmount("Ціна за ніч");
    }
}

/// <summary>Rules of <see cref="RateRequest"/>.</summary>
public sealed class RateRequestValidator : RequestValidator<RateRequest>
{
    /// <summary>Creates the rules.</summary>
    public RateRequestValidator()
    {
        RuleFor(r => r.BasePricePerNight)
            .GreaterThan(0).WithMessage("Ціна за ніч має бути більшою за 0.")
            .MoneyAmount("Ціна за ніч");
    }
}

/// <summary>Rules of <see cref="RateListQuery"/>.</summary>
public sealed class RateListQueryValidator : RequestValidator<RateListQuery>
{
    /// <summary>Creates the rules.</summary>
    public RateListQueryValidator()
    {
        Include(new PageQueryValidator());
    }
}
