using Microsoft.AspNetCore.Mvc;
using Zatyshok.Api.Common.Authorization;
using Zatyshok.Domain.Modules.Bookings.Contracts;

namespace Zatyshok.Api.Modules.Bookings;

/// <summary>
/// Пошук доступних номерів (FR-09, FR-10): з нього гість починає бронювання на порталі (UC-04), а оператор —
/// бронювання за телефоном чи на рецепції (UC-05).
/// </summary>
// Roles: table 6.2 gives FR-10 to the guest and both operators, and UC-04 starts after the guest signed in
// (UC-02). T3 makes the search a public endpoint of the portal instead, so it stays open to
// anyone in T4. This is a deviation from the precondition of UC-04 to agree with the customer.
// It has no hotel scope: availability and prices are public; the scope applies when a booking is created (T5).
[ApiController]
[Route("api/v1/availability")]
public class AvailabilityController(IAvailabilityService availability) : ControllerBase
{
    /// <summary>Знайти доступні номери на період.</summary>
    /// <remarks>
    /// Для обраного готелю або кожного діючого готелю міста повертає категорії номерів, що вміщують гостей,
    /// з кількістю доступних номерів, ціною кожної ночі і загальною вартістю.
    ///
    /// **Доступність** рахується окремо для кожної ночі від дати заїзду до дня перед виїздом: номери категорії,
    /// що продаються (діючі й не на ремонті), мінус підтверджені бронювання і бронювання із заселеним гостем,
    /// які включають цю ніч. Доступно на весь період — найменше з нічних значень (п. 12.8.2). Нові, скасовані,
    /// завершені бронювання і неявки номерів не займають.
    ///
    /// **Вартість** — сума цін ночей: на ночі сезонного періоду (обидві межі включно) діє сезонна ціна, на інші —
    /// базова ціна тарифу (FR-08). День виїзду не є ніччю і не оплачується.
    ///
    /// Результат — знімок на момент запиту: під час створення бронювання доступність перевіряється ще раз.
    ///
    /// **Приклад:** `GET /api/v1/availability?hotelId=22222222-2222-2222-2222-222222222203&amp;checkIn=2027-05-30&amp;checkOut=2027-06-02&amp;guests=2`
    /// — «Затишок Одеса», три ночі; ніч на 1 червня вже в «Літньому сезоні 2027», тому Standard коштує
    /// 1800 + 1800 + 2400 = 6000 грн.
    /// </remarks>
    /// <param name="query">Готель або місто, дати і кількість гостей.</param>
    /// <param name="cancellationToken">Скасування запиту.</param>
    /// <response code="200">Готелі з категоріями; порожній список, якщо в місті немає діючих готелів.</response>
    /// <response code="400">
    /// Некоректні параметри: не вказано готель чи місто або вказано обидва, виїзд не пізніше заїзду, період
    /// довший за 30 ночей, заїзд раніше поточної дати готелю, гостей більше, ніж уміщує категорія.
    /// </response>
    /// <response code="404">Готель або категорію не знайдено.</response>
    /// <response code="409">Готель або категорію деактивовано.</response>
    [HttpGet]
    [AllowedRoles(Roles.Anonymous)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<AvailabilityResponse> Search([FromQuery] AvailabilityQuery query, CancellationToken cancellationToken)
    {
        var hotels = await availability.SearchAsync(query.ToSearch(), cancellationToken);
        return AvailabilityResponse.From(query, hotels);
    }
}
