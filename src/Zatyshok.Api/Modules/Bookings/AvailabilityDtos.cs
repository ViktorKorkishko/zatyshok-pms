using FluentValidation;
using Zatyshok.Api.Common.Validation;
using Zatyshok.Domain.Modules.Bookings;
using Zatyshok.Domain.Modules.Bookings.Contracts;

namespace Zatyshok.Api.Modules.Bookings;

/// <summary>
/// Параметри пошуку доступних номерів (FR-10). Вкажіть або готель, або місто. Список не ділиться на
/// сторінки: у місті кілька готелів, у готелі — кілька категорій.
/// </summary>
public sealed class AvailabilityQuery
{
    /// <summary>Готель. Вкажіть готель або місто, але не обидва.</summary>
    /// <example>22222222-2222-2222-2222-222222222203</example>
    public Guid? HotelId { get; init; }

    /// <summary>
    /// Місто, до 100 символів: пошук у всіх діючих готелях міста, без урахування регістру (наприклад,
    /// «Одеса» або «одеса»). Вкажіть готель або місто, але не обидва.
    /// </summary>
    public string? City { get; init; }

    /// <summary>Лише ця категорія номерів; якщо не вказано — усі діючі категорії, що вміщують гостей.</summary>
    public Guid? RoomCategoryId { get; init; }

    /// <summary>Дата заїзду: календарна дата готелю (NFR-20), не раніше поточної дати готелю.</summary>
    /// <example>2027-05-30</example>
    public DateOnly? CheckIn { get; init; }

    /// <summary>Дата виїзду: пізніше дати заїзду, щонайбільше через 30 ночей. День виїзду не є ніччю і не оплачується.</summary>
    /// <example>2027-06-02</example>
    public DateOnly? CheckOut { get; init; }

    /// <summary>Кількість гостей: щонайменше 1 і не більше місткості категорії.</summary>
    /// <example>2</example>
    public int? Guests { get; init; }

    /// <summary>The search for the service; the validator has checked that the required values are there.</summary>
    internal AvailabilitySearch ToSearch() => new(
        HotelId,
        string.IsNullOrWhiteSpace(City) ? null : City,
        RoomCategoryId,
        CheckIn!.Value,
        CheckOut!.Value,
        Guests!.Value);
}

/// <summary>Результат пошуку: готелі з категоріями номерів, кількістю доступних номерів і вартістю проживання.</summary>
public sealed class AvailabilityResponse
{
    /// <summary>Дата заїзду із запиту.</summary>
    /// <example>2027-05-30</example>
    public DateOnly CheckIn { get; init; }

    /// <summary>Дата виїзду із запиту.</summary>
    /// <example>2027-06-02</example>
    public DateOnly CheckOut { get; init; }

    /// <summary>Кількість ночей: від дати заїзду до дня перед виїздом.</summary>
    /// <example>3</example>
    public int NightCount { get; init; }

    /// <summary>Кількість гостей із запиту.</summary>
    /// <example>2</example>
    public int Guests { get; init; }

    /// <summary>
    /// Знайдені готелі за назвою. Порожній список — у місті немає діючих готелів мережі (UC-04 А2).
    /// </summary>
    public required IReadOnlyList<HotelAvailabilityResponse> Hotels { get; init; }

    internal static AvailabilityResponse From(AvailabilityQuery query, IReadOnlyList<HotelOffers> hotels) => new()
    {
        CheckIn = query.CheckIn!.Value,
        CheckOut = query.CheckOut!.Value,
        NightCount = query.CheckOut!.Value.DayNumber - query.CheckIn!.Value.DayNumber,
        Guests = query.Guests!.Value,
        Hotels = hotels.Select(HotelAvailabilityResponse.From).ToList(),
    };
}

/// <summary>Готель із категоріями номерів, які він може запропонувати на період.</summary>
public sealed class HotelAvailabilityResponse
{
    /// <summary>Ідентифікатор готелю.</summary>
    /// <example>22222222-2222-2222-2222-222222222203</example>
    public Guid HotelId { get; init; }

    /// <summary>Назва готелю.</summary>
    /// <example>Затишок Одеса</example>
    public required string Name { get; init; }

    /// <summary>Місто.</summary>
    /// <example>Одеса</example>
    public required string City { get; init; }

    /// <summary>Адреса.</summary>
    /// <example>вул. Дерибасівська, 5</example>
    public required string Address { get; init; }

    /// <summary>
    /// Діючі категорії, що вміщують гостей і мають тариф у цьому готелі, від найдешевшої. Категорія без
    /// вільних номерів теж є в списку, з availableRooms = 0.
    /// </summary>
    public required IReadOnlyList<CategoryAvailabilityResponse> Categories { get; init; }

    internal static HotelAvailabilityResponse From(HotelOffers hotel) => new()
    {
        HotelId = hotel.HotelId,
        Name = hotel.Name,
        City = hotel.City,
        Address = hotel.Address,
        Categories = hotel.Categories.Select(CategoryAvailabilityResponse.From).ToList(),
    };
}

/// <summary>Категорія номерів готелю: доступні номери, ціни ночей і загальна вартість проживання.</summary>
public sealed class CategoryAvailabilityResponse
{
    /// <summary>Ідентифікатор категорії.</summary>
    /// <example>11111111-1111-1111-1111-111111111101</example>
    public Guid RoomCategoryId { get; init; }

    /// <summary>Назва категорії.</summary>
    /// <example>Standard</example>
    public required string Name { get; init; }

    /// <summary>Опис категорії.</summary>
    /// <example>Стандартний номер з одним двоспальним або двома односпальними ліжками</example>
    public string? Description { get; init; }

    /// <summary>Базова місткість категорії — найбільша кількість гостей.</summary>
    /// <example>2</example>
    public int Capacity { get; init; }

    /// <summary>Зручності, які є в кожному номері категорії, що продається: номер призначається під час заселення.</summary>
    /// <example>["Wi-Fi", "кондиціонер", "телевізор"]</example>
    public required IReadOnlyList<string> Amenities { get; init; }

    /// <summary>
    /// Скільки номерів категорії можна забронювати на весь період: найменша кількість вільних номерів
    /// серед ночей (п. 12.8.2). 0 — бронювання на ці дати неможливе.
    /// </summary>
    /// <example>2</example>
    public int AvailableRooms { get; init; }

    /// <summary>Загальна вартість проживання в гривнях: сума цін ночей (FR-08).</summary>
    /// <example>6000.00</example>
    public decimal TotalPrice { get; init; }

    /// <summary>Кожна ніч періоду: ціна і кількість вільних номерів категорії.</summary>
    public required IReadOnlyList<NightAvailabilityResponse> Nights { get; init; }

    internal static CategoryAvailabilityResponse From(CategoryOffer offer) => new()
    {
        RoomCategoryId = offer.RoomCategoryId,
        Name = offer.Name,
        Description = offer.Description,
        Capacity = offer.Capacity,
        Amenities = offer.Amenities,
        AvailableRooms = offer.AvailableRooms,
        TotalPrice = offer.TotalPrice,
        Nights = offer.Nights.Select(NightAvailabilityResponse.From).ToList(),
    };
}

/// <summary>Одна ніч періоду.</summary>
public sealed class NightAvailabilityResponse
{
    /// <summary>Дата, з якої починається ніч.</summary>
    /// <example>2027-06-01</example>
    public DateOnly Night { get; init; }

    /// <summary>Ціна ночі в гривнях: сезонна, якщо ніч у сезонному періоді тарифу, інакше базова (FR-08).</summary>
    /// <example>2400.00</example>
    public decimal Price { get; init; }

    /// <summary>
    /// Вільні номери категорії цієї ночі: номери, що продаються (діючі й не на ремонті), мінус підтверджені
    /// бронювання і бронювання із заселеним гостем, які включають цю ніч. Від'ємне значення — нестача номерів
    /// після виведення номера на ремонт.
    /// </summary>
    /// <example>2</example>
    public int FreeRooms { get; init; }

    internal static NightAvailabilityResponse From(NightOffer night) => new()
    {
        Night = night.Night,
        Price = night.Price,
        FreeRooms = night.FreeRooms,
    };
}

/// <summary>
/// Rules of <see cref="AvailabilityQuery"/> that need no data. The rules that need the database — the
/// check-in not before the hotel's current date and the guests within the capacity of a category — are
/// checked by the availability service, which answers 400 in the same format (InvalidRequestException).
/// </summary>
public sealed class AvailabilityQueryValidator : RequestValidator<AvailabilityQuery>
{
    /// <summary>Creates the rules.</summary>
    public AvailabilityQueryValidator()
    {
        // Both errors are under hotelId: the hotel is the usual choice of the operator (UC-05).
        RuleFor(q => q.HotelId)
            .Must((q, hotelId) => hotelId is not null || !string.IsNullOrWhiteSpace(q.City))
            .WithMessage("Вкажіть готель або місто.")
            .Must((q, hotelId) => hotelId is null || string.IsNullOrWhiteSpace(q.City))
            .WithMessage("Вкажіть або готель, або місто, а не обидва.");
        RuleFor(q => q.City)
            .MaximumLength(100).WithMessage("Назва міста — до 100 символів.");

        RuleFor(q => q.CheckIn)
            .NotNull().WithMessage("Вкажіть дату заїзду.");
        RuleFor(q => q.CheckOut)
            .NotNull().WithMessage("Вкажіть дату виїзду.")
            .Must((q, checkOut) => q.CheckIn is null || checkOut > q.CheckIn)
            .WithMessage("Дата виїзду має бути пізнішою за дату заїзду.")
            .Must((q, checkOut) => q.CheckIn is null || checkOut!.Value.DayNumber - q.CheckIn.Value.DayNumber <= AvailabilityCalculator.MaxNights)
            .WithMessage($"Період проживання — не більше {AvailabilityCalculator.MaxNights} ночей.");

        RuleFor(q => q.Guests)
            .NotNull().WithMessage("Вкажіть кількість гостей.")
            .GreaterThanOrEqualTo(1).WithMessage("Кількість гостей має бути не меншою за 1.");
    }
}
