using Microsoft.AspNetCore.Mvc;
using Zatyshok.Api.Common.Authorization;
using Zatyshok.Api.Common.Paging;
using Zatyshok.Infrastructure.Modules.Rooms;

namespace Zatyshok.Api.Modules.Rooms;

/// <summary>
/// Тарифи: базова ціна за ніч для кожної пари «готель – категорія» (FR-08). Тарифи вносять
/// адміністратор або відділ бронювання, керівництво їх переглядає. Тариф не видаляється — змінюють
/// його ціну; сезонні ціни задаються в /api/v1/season-rates.
/// </summary>
[ApiController]
[Route("api/v1/rates")]
public class RatesController(RateService rates) : ControllerBase
{
    /// <summary>Список тарифів.</summary>
    /// <remarks>Тарифи впорядковано за готелем і назвою категорії.</remarks>
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
    public async Task<PagedResponse<RateResponse>> List([FromQuery] RateListQuery query, CancellationToken cancellationToken)
    {
        var page = await rates.ListAsync(new RateFilter(query.HotelId, query.RoomCategoryId), query.ToPageRequest(), cancellationToken);
        return PagedResponse<RateResponse>.From(page, RateResponse.From);
    }

    /// <summary>Тариф.</summary>
    /// <param name="id">Ідентифікатор тарифу.</param>
    /// <param name="cancellationToken">Скасування запиту.</param>
    /// <response code="200">Тариф.</response>
    /// <response code="404">Тариф не знайдено.</response>
    [HttpGet("{id:guid}")]
    [AllowedRoles(Roles.Administrator, Roles.ReservationAgent, Roles.Executive)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<RateResponse> Get(Guid id, CancellationToken cancellationToken)
        => RateResponse.From(await rates.GetAsync(id, cancellationToken));

    /// <summary>Створити тариф.</summary>
    /// <remarks>Для кожної пари «готель – категорія» тариф лише один; готель і категорія мають бути діючими.</remarks>
    /// <param name="request">Пара і базова ціна.</param>
    /// <param name="cancellationToken">Скасування запиту.</param>
    /// <response code="201">Тариф створено; адреса — у заголовку Location.</response>
    /// <response code="400">Помилки в полях.</response>
    /// <response code="403">Готель поза областю видимості користувача.</response>
    /// <response code="404">Готель або категорію не знайдено.</response>
    /// <response code="409">Тариф пари вже є, або готель чи категорію деактивовано.</response>
    [HttpPost]
    [AllowedRoles(Roles.Administrator, Roles.ReservationAgent)]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<RateResponse>> Create(NewRateRequest request, CancellationToken cancellationToken)
    {
        var rate = await rates.CreateAsync(request.HotelId, request.RoomCategoryId, request.BasePricePerNight, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = rate.Id }, RateResponse.From(rate));
    }

    /// <summary>Змінити базову ціну тарифу.</summary>
    /// <remarks>Нова ціна діє для нових бронювань: бронювання зберігає ціни ночей, зафіксовані під час створення.</remarks>
    /// <param name="id">Ідентифікатор тарифу.</param>
    /// <param name="request">Нова базова ціна.</param>
    /// <param name="cancellationToken">Скасування запиту.</param>
    /// <response code="200">Тариф після зміни.</response>
    /// <response code="400">Помилки в полях.</response>
    /// <response code="404">Тариф не знайдено.</response>
    [HttpPut("{id:guid}")]
    [AllowedRoles(Roles.Administrator, Roles.ReservationAgent)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<RateResponse> Update(Guid id, RateRequest request, CancellationToken cancellationToken)
        => RateResponse.From(await rates.UpdateAsync(id, request.BasePricePerNight, cancellationToken));
}
