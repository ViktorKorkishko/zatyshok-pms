using Microsoft.AspNetCore.Mvc;
using Zatyshok.Api.Common.Authorization;
using Zatyshok.Api.Common.Paging;
using Zatyshok.Infrastructure.Modules.Rooms;

namespace Zatyshok.Api.Modules.Rooms;

/// <summary>
/// Готелі мережі (FR-05, UC-11). Список і картка готелю відкриті для всіх, бо з них портал
/// будує пошук за містом і готелем (FR-10); змінює готелі адміністратор системи.
/// </summary>
[ApiController]
[Route("api/v1/hotels")]
public class HotelsController(HotelService hotels) : ControllerBase
{
    /// <summary>Список готелів.</summary>
    /// <remarks>Готелі впорядковано за назвою.</remarks>
    /// <param name="query">Фільтри і сторінка.</param>
    /// <param name="cancellationToken">Скасування запиту.</param>
    /// <response code="200">Сторінка списку.</response>
    /// <response code="400">Некоректні параметри сторінки.</response>
    [HttpGet]
    [AllowedRoles(Roles.Anonymous)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<PagedResponse<HotelResponse>> List([FromQuery] HotelListQuery query, CancellationToken cancellationToken)
    {
        var page = await hotels.ListAsync(new HotelFilter(query.City, query.IsActive), query.ToPageRequest(), cancellationToken);
        return PagedResponse<HotelResponse>.From(page, HotelResponse.From);
    }

    /// <summary>Картка готелю.</summary>
    /// <param name="id" example="22222222-2222-2222-2222-222222222201">Ідентифікатор готелю.</param>
    /// <param name="cancellationToken">Скасування запиту.</param>
    /// <response code="200">Готель.</response>
    /// <response code="404">Готель не знайдено.</response>
    [HttpGet("{id:guid}")]
    [AllowedRoles(Roles.Anonymous)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<HotelResponse> Get(Guid id, CancellationToken cancellationToken)
        => HotelResponse.From(await hotels.GetAsync(id, cancellationToken));

    /// <summary>Створити готель.</summary>
    /// <remarks>Не вказані правила готелю отримують типові значення: скасування — до 18:00 за добу до заїзду, заїзд о 14:00, виїзд о 12:00.</remarks>
    /// <param name="request">Поля готелю.</param>
    /// <param name="cancellationToken">Скасування запиту.</param>
    /// <response code="201">Готель створено; адреса картки — у заголовку Location.</response>
    /// <response code="400">Помилки в полях.</response>
    [HttpPost]
    [AllowedRoles(Roles.Administrator)]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<HotelResponse>> Create(HotelRequest request, CancellationToken cancellationToken)
    {
        var hotel = await hotels.CreateAsync(request.ToData(), cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = hotel.Id }, HotelResponse.From(hotel));
    }

    /// <summary>Змінити готель.</summary>
    /// <remarks>
    /// Замінює всі поля готелю. Нові правила скасування діють для бронювань, створених після зміни;
    /// наявні бронювання зберігають обчислений раніше граничний термін (FR-15).
    /// </remarks>
    /// <param name="id" example="22222222-2222-2222-2222-222222222201">Ідентифікатор готелю.</param>
    /// <param name="request">Нові значення полів.</param>
    /// <param name="cancellationToken">Скасування запиту.</param>
    /// <response code="200">Готель після зміни.</response>
    /// <response code="400">Помилки в полях.</response>
    /// <response code="404">Готель не знайдено.</response>
    [HttpPut("{id:guid}")]
    [AllowedRoles(Roles.Administrator)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<HotelResponse> Update(Guid id, HotelRequest request, CancellationToken cancellationToken)
        => HotelResponse.From(await hotels.UpdateAsync(id, request.ToData(), cancellationToken));

    /// <summary>Деактивувати готель.</summary>
    /// <remarks>Готель з бронюваннями не видаляється, а деактивується (FR-05): історія зберігається.</remarks>
    /// <param name="id" example="22222222-2222-2222-2222-222222222201">Ідентифікатор готелю.</param>
    /// <param name="cancellationToken">Скасування запиту.</param>
    /// <response code="200">Готель після зміни.</response>
    /// <response code="404">Готель не знайдено.</response>
    [HttpPost("{id:guid}/deactivate")]
    [AllowedRoles(Roles.Administrator)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<HotelResponse> Deactivate(Guid id, CancellationToken cancellationToken)
        => HotelResponse.From(await hotels.SetActiveAsync(id, isActive: false, cancellationToken));

    /// <summary>Знову активувати готель.</summary>
    /// <param name="id" example="22222222-2222-2222-2222-222222222201">Ідентифікатор готелю.</param>
    /// <param name="cancellationToken">Скасування запиту.</param>
    /// <response code="200">Готель після зміни.</response>
    /// <response code="404">Готель не знайдено.</response>
    [HttpPost("{id:guid}/activate")]
    [AllowedRoles(Roles.Administrator)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<HotelResponse> Activate(Guid id, CancellationToken cancellationToken)
        => HotelResponse.From(await hotels.SetActiveAsync(id, isActive: true, cancellationToken));

    /// <summary>Видалити готель.</summary>
    /// <remarks>
    /// Лише готель, на який ще ніщо не посилається, наприклад створений помилково. Готель з номерами,
    /// тарифами, бронюваннями або персоналом видалити не можна — його деактивують.
    /// </remarks>
    /// <param name="id">Ідентифікатор готелю.</param>
    /// <param name="cancellationToken">Скасування запиту.</param>
    /// <response code="204">Готель видалено.</response>
    /// <response code="404">Готель не знайдено.</response>
    /// <response code="409">Готель використовується; деактивуйте його.</response>
    // No example id, unlike the other actions: "Try it out" in Swagger would delete a seeded record.
    [HttpDelete("{id:guid}")]
    [AllowedRoles(Roles.Administrator)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await hotels.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
