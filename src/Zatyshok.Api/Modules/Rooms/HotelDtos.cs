using FluentValidation;
using Zatyshok.Api.Common.Paging;
using Zatyshok.Api.Common.Validation;
using Zatyshok.Domain.Modules.Rooms;
using Zatyshok.Infrastructure.Modules.Rooms;

namespace Zatyshok.Api.Modules.Rooms;

/// <summary>Готель мережі для створення або повної зміни (FR-05).</summary>
public sealed class HotelRequest
{
    /// <summary>Назва готелю, до 200 символів.</summary>
    /// <example>Затишок Ужгород</example>
    public string Name { get; init; } = string.Empty;

    /// <summary>Місто, до 100 символів; за ним шукають доступність (FR-10).</summary>
    /// <example>Ужгород</example>
    public string City { get; init; } = string.Empty;

    /// <summary>Адреса, до 300 символів.</summary>
    /// <example>вул. Корзо, 7</example>
    public string Address { get; init; } = string.Empty;

    /// <summary>Категорія готелю: кількість зірок від 1 до 5.</summary>
    /// <example>3</example>
    public int Stars { get; init; }

    /// <summary>Часовий пояс IANA; дати заїзду й виїзду — календарні дати цього поясу (NFR-20).</summary>
    /// <example>Europe/Kyiv</example>
    public string TimeZoneId { get; init; } = string.Empty;

    /// <summary>Телефон, до 32 символів; необов'язковий.</summary>
    /// <example>+380312123456</example>
    public string? Phone { get; init; }

    /// <summary>Електронна пошта, до 256 символів; необов'язкова.</summary>
    /// <example>uzhhorod@zatyshok.ua</example>
    public string? Email { get; init; }

    /// <summary>
    /// За скільки діб до заїзду закінчується безкоштовне скасування (FR-15), від 0 до 365;
    /// якщо не вказано — 1.
    /// </summary>
    /// <example>1</example>
    public int? CancellationDeadlineDaysBefore { get; init; }

    /// <summary>Година за часом готелю, о якій закінчується безкоштовне скасування; якщо не вказано — 18:00.</summary>
    /// <example>18:00:00</example>
    public TimeOnly? CancellationDeadlineTime { get; init; }

    /// <summary>Час заїзду; якщо не вказано — 14:00.</summary>
    /// <example>14:00:00</example>
    public TimeOnly? CheckInTime { get; init; }

    /// <summary>Час виїзду; якщо не вказано — 12:00.</summary>
    /// <example>12:00:00</example>
    public TimeOnly? CheckOutTime { get; init; }

    /// <summary>The fields for the service; an omitted rule takes the default of 12.3.2 (FR-15).</summary>
    internal HotelData ToData() => new(
        Name,
        City,
        Address,
        Stars,
        TimeZoneId,
        Phone,
        Email,
        CancellationDeadlineDaysBefore ?? Hotel.DefaultCancellationDeadlineDaysBefore,
        CancellationDeadlineTime ?? Hotel.DefaultCancellationDeadlineTime,
        CheckInTime ?? Hotel.DefaultCheckInTime,
        CheckOutTime ?? Hotel.DefaultCheckOutTime);
}

/// <summary>Готель мережі.</summary>
public sealed class HotelResponse
{
    /// <summary>Ідентифікатор готелю.</summary>
    /// <example>22222222-2222-2222-2222-222222222201</example>
    public Guid Id { get; init; }

    /// <summary>Назва готелю.</summary>
    /// <example>Затишок Київ</example>
    public required string Name { get; init; }

    /// <summary>Місто.</summary>
    /// <example>Київ</example>
    public required string City { get; init; }

    /// <summary>Адреса.</summary>
    /// <example>вул. Хрещатик, 1</example>
    public required string Address { get; init; }

    /// <summary>Кількість зірок.</summary>
    /// <example>4</example>
    public int Stars { get; init; }

    /// <summary>Часовий пояс IANA.</summary>
    /// <example>Europe/Kyiv</example>
    public required string TimeZoneId { get; init; }

    /// <summary>Телефон.</summary>
    /// <example>+380441234567</example>
    public string? Phone { get; init; }

    /// <summary>Електронна пошта.</summary>
    /// <example>kyiv@zatyshok.ua</example>
    public string? Email { get; init; }

    /// <summary>Чи працює готель; деактивований готель не видаляється, щоб зберегти історію (FR-05).</summary>
    /// <example>true</example>
    public bool IsActive { get; init; }

    /// <summary>За скільки діб до заїзду закінчується безкоштовне скасування (FR-15).</summary>
    /// <example>1</example>
    public int CancellationDeadlineDaysBefore { get; init; }

    /// <summary>Година за часом готелю, о якій закінчується безкоштовне скасування.</summary>
    /// <example>18:00:00</example>
    public TimeOnly CancellationDeadlineTime { get; init; }

    /// <summary>Час заїзду.</summary>
    /// <example>14:00:00</example>
    public TimeOnly CheckInTime { get; init; }

    /// <summary>Час виїзду.</summary>
    /// <example>12:00:00</example>
    public TimeOnly CheckOutTime { get; init; }

    internal static HotelResponse From(Hotel hotel) => new()
    {
        Id = hotel.Id,
        Name = hotel.Name,
        City = hotel.City,
        Address = hotel.Address,
        Stars = hotel.Stars,
        TimeZoneId = hotel.TimeZoneId,
        Phone = hotel.Phone,
        Email = hotel.Email,
        IsActive = hotel.IsActive,
        CancellationDeadlineDaysBefore = hotel.CancellationDeadlineDaysBefore,
        CancellationDeadlineTime = hotel.CancellationDeadlineTime,
        CheckInTime = hotel.CheckInTime,
        CheckOutTime = hotel.CheckOutTime,
    };
}

/// <summary>Фільтри списку готелів.</summary>
public sealed class HotelListQuery : PageQuery
{
    /// <summary>Лише готелі цього міста (точний збіг назви).</summary>
    /// <example>Київ</example>
    public string? City { get; init; }

    /// <summary>true — лише діючі готелі, false — лише деактивовані; не вказано — усі.</summary>
    public bool? IsActive { get; init; }
}

/// <summary>Rules of <see cref="HotelRequest"/>; the lengths are those of the columns of rooms.Hotels.</summary>
public sealed class HotelRequestValidator : RequestValidator<HotelRequest>
{
    /// <summary>Largest number of days before check-in for the end of free cancellation.</summary>
    /// <remarks>
    /// Not in the requirements: a guard against a typo. A deadline more than a year before check-in
    /// makes no sense, and a very large value would move the date before 0001-01-01 in
    /// <c>Hotel.GetFreeCancellationUntilUtc</c>, which then throws.
    /// </remarks>
    public const int MaxCancellationDeadlineDays = 365;

    /// <summary>Creates the rules.</summary>
    public HotelRequestValidator()
    {
        RuleFor(h => h.Name)
            .NotEmpty().WithMessage("Вкажіть назву готелю.")
            .MaximumLength(200).WithMessage("Назва готелю має бути не довшою за 200 символів.");
        RuleFor(h => h.City)
            .NotEmpty().WithMessage("Вкажіть місто.")
            .MaximumLength(100).WithMessage("Назва міста має бути не довшою за 100 символів.");
        RuleFor(h => h.Address)
            .NotEmpty().WithMessage("Вкажіть адресу.")
            .MaximumLength(300).WithMessage("Адреса має бути не довшою за 300 символів.");
        RuleFor(h => h.Stars)
            .InclusiveBetween(1, 5).WithMessage("Кількість зірок має бути від 1 до 5.");
        RuleFor(h => h.TimeZoneId)
            .NotEmpty().WithMessage("Вкажіть часовий пояс готелю, наприклад Europe/Kyiv.")
            .MaximumLength(64).WithMessage("Ідентифікатор часового поясу має бути не довшим за 64 символи.")
            .Must(id => Hotel.IsKnownTimeZone(id.Trim())).WithMessage("Невідомий часовий пояс; вкажіть ідентифікатор IANA, наприклад Europe/Kyiv.");
        RuleFor(h => h.Phone)
            .MaximumLength(32).WithMessage("Телефон має бути не довшим за 32 символи.");
        RuleFor(h => h.Email)
            .MaximumLength(256).WithMessage("Електронна пошта має бути не довшою за 256 символів.")
            .EmailAddress().WithMessage("Вкажіть коректну електронну пошту.");
        RuleFor(h => h.CancellationDeadlineDaysBefore)
            .InclusiveBetween(0, MaxCancellationDeadlineDays)
            .WithMessage($"Кількість діб до заїзду для безкоштовного скасування має бути від 0 до {MaxCancellationDeadlineDays}.");
    }
}

/// <summary>Rules of <see cref="HotelListQuery"/>.</summary>
public sealed class HotelListQueryValidator : RequestValidator<HotelListQuery>
{
    /// <summary>Creates the rules.</summary>
    public HotelListQueryValidator()
    {
        Include(new PageQueryValidator());
    }
}
