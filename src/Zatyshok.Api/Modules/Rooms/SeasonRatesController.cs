using Microsoft.AspNetCore.Mvc;
using Zatyshok.Api.Common.Authorization;
using Zatyshok.Api.Common.Paging;
using Zatyshok.Infrastructure.Modules.Rooms;

namespace Zatyshok.Api.Modules.Rooms;

/// <summary>
/// Сезонні періоди тарифів (FR-08): на ночі періоду діє ціна періоду замість базової. Періоди одного
/// тарифу не перетинаються. Вносять адміністратор або відділ бронювання, керівництво переглядає.
/// </summary>
[ApiController]
[Route("api/v1/season-rates")]
public class SeasonRatesController(SeasonRateService seasons) : ControllerBase
{
    /// <summary>Список сезонних періодів.</summary>
    /// <remarks>Періоди впорядковано за першою ніччю.</remarks>
    /// <param name="query">Фільтри і сторінка.</param>
    /// <param name="cancellationToken">Скасування запиту.</param>
    /// <response code="200">Сторінка списку.</response>
    /// <response code="400">Некоректні фільтри або параметри сторінки.</response>
    /// <response code="403">Вказаний готель поза областю видимості користувача.</response>
    [HttpGet]
    [AllowedRoles(Roles.Administrator, Roles.ReservationAgent, Roles.Executive)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<PagedResponse<SeasonRateResponse>> List([FromQuery] SeasonRateListQuery query, CancellationToken cancellationToken)
    {
        var page = await seasons.ListAsync(new SeasonRateFilter(query.RateId, query.HotelId), query.ToPageRequest(), cancellationToken);
        return PagedResponse<SeasonRateResponse>.From(page, SeasonRateResponse.From);
    }

    /// <summary>Сезонний період.</summary>
    /// <param name="id" example="55555555-5555-5555-5555-555555555501">Ідентифікатор періоду.</param>
    /// <param name="cancellationToken">Скасування запиту.</param>
    /// <response code="200">Період.</response>
    /// <response code="404">Період не знайдено.</response>
    [HttpGet("{id:guid}")]
    [AllowedRoles(Roles.Administrator, Roles.ReservationAgent, Roles.Executive)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<SeasonRateResponse> Get(Guid id, CancellationToken cancellationToken)
        => SeasonRateResponse.From(await seasons.GetAsync(id, cancellationToken));

    /// <summary>Створити сезонний період.</summary>
    /// <param name="request">Тариф і поля періоду.</param>
    /// <param name="cancellationToken">Скасування запиту.</param>
    /// <response code="201">Період створено; адреса — у заголовку Location.</response>
    /// <response code="400">Помилки в полях.</response>
    /// <response code="404">Тариф не знайдено.</response>
    /// <response code="409">Період перетинається з іншим періодом тарифу.</response>
    [HttpPost]
    [AllowedRoles(Roles.Administrator, Roles.ReservationAgent)]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SeasonRateResponse>> Create(NewSeasonRateRequest request, CancellationToken cancellationToken)
    {
        var season = await seasons.CreateAsync(request.RateId, request.ToData(), cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = season.Id }, SeasonRateResponse.From(season));
    }

    /// <summary>Змінити сезонний період.</summary>
    /// <remarks>Нові дати й ціна діють для нових бронювань; наявні зберігають зафіксовані ціни ночей.</remarks>
    /// <param name="id" example="55555555-5555-5555-5555-555555555501">Ідентифікатор періоду.</param>
    /// <param name="request">Нові значення полів.</param>
    /// <param name="cancellationToken">Скасування запиту.</param>
    /// <response code="200">Період після зміни.</response>
    /// <response code="400">Помилки в полях.</response>
    /// <response code="404">Період не знайдено.</response>
    /// <response code="409">Період перетинається з іншим періодом тарифу.</response>
    [HttpPut("{id:guid}")]
    [AllowedRoles(Roles.Administrator, Roles.ReservationAgent)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<SeasonRateResponse> Update(Guid id, SeasonRateRequest request, CancellationToken cancellationToken)
        => SeasonRateResponse.From(await seasons.UpdateAsync(id, request.ToData(), cancellationToken));

    /// <summary>Видалити сезонний період.</summary>
    /// <remarks>Період видаляється повністю: бронювання зберігають ціни своїх ночей, тож історія не втрачається.</remarks>
    /// <param name="id">Ідентифікатор періоду.</param>
    /// <param name="cancellationToken">Скасування запиту.</param>
    /// <response code="204">Період видалено.</response>
    /// <response code="404">Період не знайдено.</response>
    // No example id, unlike the other actions: "Try it out" in Swagger would delete a seeded record.
    [HttpDelete("{id:guid}")]
    [AllowedRoles(Roles.Administrator, Roles.ReservationAgent)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await seasons.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
