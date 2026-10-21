using FluentValidation;
using Zatyshok.Api.Common.Paging;
using Zatyshok.Api.Common.Validation;
using Zatyshok.Domain.Modules.Rooms;
using Zatyshok.Infrastructure.Modules.Rooms;

namespace Zatyshok.Api.Modules.Rooms;

/// <summary>Поля сезонного періоду для повної зміни (FR-08). Тариф періоду не змінюється.</summary>
public class SeasonRateRequest
{
    /// <summary>Назва періоду, до 200 символів.</summary>
    /// <example>Літній сезон 2027</example>
    public string Name { get; init; } = string.Empty;

    /// <summary>Перша ніч періоду (дата за календарем готелю).</summary>
    /// <example>2027-06-01</example>
    public DateOnly StartDate { get; init; }

    /// <summary>Остання ніч періоду включно; не раніше за першу.</summary>
    /// <example>2027-08-31</example>
    public DateOnly EndDate { get; init; }

    /// <summary>Ціна за ніч у гривнях у межах періоду, більша за 0, до копійки.</summary>
    /// <example>2400.00</example>
    public decimal PricePerNight { get; init; }

    internal SeasonRateData ToData() => new(Name, StartDate, EndDate, PricePerNight);
}

/// <summary>Новий сезонний період тарифу (FR-08).</summary>
public sealed class NewSeasonRateRequest : SeasonRateRequest
{
    /// <summary>Тариф, ціну якого період замінює на свої ночі.</summary>
    /// <example>44444444-4444-4444-4444-444444440200</example>
    public Guid RateId { get; init; }
}

/// <summary>Сезонний період тарифу.</summary>
public sealed class SeasonRateResponse
{
    /// <summary>Ідентифікатор періоду.</summary>
    /// <example>55555555-5555-5555-5555-555555555501</example>
    public Guid Id { get; init; }

    /// <summary>Тариф періоду.</summary>
    /// <example>44444444-4444-4444-4444-444444440200</example>
    public Guid RateId { get; init; }

    /// <summary>Готель тарифу.</summary>
    /// <example>22222222-2222-2222-2222-222222222203</example>
    public Guid HotelId { get; init; }

    /// <summary>Категорія тарифу.</summary>
    /// <example>11111111-1111-1111-1111-111111111101</example>
    public Guid RoomCategoryId { get; init; }

    /// <summary>Назва періоду.</summary>
    /// <example>Літній сезон 2027</example>
    public required string Name { get; init; }

    /// <summary>Перша ніч періоду.</summary>
    /// <example>2027-06-01</example>
    public DateOnly StartDate { get; init; }

    /// <summary>Остання ніч періоду включно.</summary>
    /// <example>2027-08-31</example>
    public DateOnly EndDate { get; init; }

    /// <summary>Ціна за ніч у гривнях у межах періоду.</summary>
    /// <example>2400.00</example>
    public decimal PricePerNight { get; init; }

    internal static SeasonRateResponse From(SeasonRate season) => new()
    {
        Id = season.Id,
        RateId = season.RateId,
        HotelId = season.Rate.HotelId,
        RoomCategoryId = season.Rate.RoomCategoryId,
        Name = season.Name,
        StartDate = season.StartDate,
        EndDate = season.EndDate,
        PricePerNight = season.PricePerNight,
    };
}

/// <summary>Фільтри списку сезонних періодів.</summary>
public sealed class SeasonRateListQuery : PageQuery
{
    /// <summary>Лише періоди цього тарифу.</summary>
    /// <example>44444444-4444-4444-4444-444444440200</example>
    public Guid? RateId { get; init; }

    /// <summary>Лише періоди тарифів цього готелю.</summary>
    public Guid? HotelId { get; init; }
}

/// <summary>Rules of <see cref="SeasonRateRequest"/>; the length and the price are those of rooms.SeasonRates.</summary>
public sealed class SeasonRateRequestValidator : RequestValidator<SeasonRateRequest>
{
    /// <summary>Creates the rules.</summary>
    public SeasonRateRequestValidator()
    {
        RuleFor(s => s.Name)
            .NotEmpty().WithMessage("Вкажіть назву періоду.")
            .MaximumLength(200).WithMessage("Назва періоду має бути не довшою за 200 символів.");
        RuleFor(s => s.StartDate)
            .NotEmpty().WithMessage("Вкажіть першу ніч періоду.");
        RuleFor(s => s.EndDate)
            .NotEmpty().WithMessage("Вкажіть останню ніч періоду.")
            .GreaterThanOrEqualTo(s => s.StartDate).WithMessage("Остання ніч періоду не може бути раніше за першу.");
        RuleFor(s => s.PricePerNight)
            .GreaterThan(0).WithMessage("Ціна за ніч має бути більшою за 0.")
            .MoneyAmount("Ціна за ніч");
    }
}

/// <summary>Rules of <see cref="NewSeasonRateRequest"/>: those of <see cref="SeasonRateRequest"/> and the rate.</summary>
public sealed class NewSeasonRateRequestValidator : RequestValidator<NewSeasonRateRequest>
{
    /// <summary>Creates the rules.</summary>
    public NewSeasonRateRequestValidator()
    {
        Include(new SeasonRateRequestValidator());
        RuleFor(s => s.RateId)
            .NotEmpty().WithMessage("Вкажіть тариф.");
    }
}

/// <summary>Rules of <see cref="SeasonRateListQuery"/>.</summary>
public sealed class SeasonRateListQueryValidator : RequestValidator<SeasonRateListQuery>
{
    /// <summary>Creates the rules.</summary>
    public SeasonRateListQueryValidator()
    {
        Include(new PageQueryValidator());
    }
}
