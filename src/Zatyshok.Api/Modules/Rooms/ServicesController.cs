using Microsoft.AspNetCore.Mvc;
using Zatyshok.Api.Common.Authorization;
using Zatyshok.Api.Common.Paging;
using Zatyshok.Infrastructure.Modules.Rooms;

namespace Zatyshok.Api.Modules.Rooms;

/// <summary>
/// Довідник додаткових послуг мережі: сніданок, паркування, трансфер (FR-05, UC-11). Оператор
/// рецепції обирає з нього позиції рахунку (FR-32); змінює довідник адміністратор.
/// </summary>
[ApiController]
[Route("api/v1/services")]
public class ServicesController(ServiceCatalogService services) : ControllerBase
{
    /// <summary>Список послуг.</summary>
    /// <remarks>Послуги впорядковано за назвою.</remarks>
    /// <param name="query">Фільтри і сторінка.</param>
    /// <param name="cancellationToken">Скасування запиту.</param>
    /// <response code="200">Сторінка списку.</response>
    /// <response code="400">Некоректні параметри сторінки.</response>
    [HttpGet]
    [AllowedRoles(Roles.Administrator, Roles.Receptionist)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<PagedResponse<ServiceResponse>> List([FromQuery] ServiceListQuery query, CancellationToken cancellationToken)
    {
        var page = await services.ListAsync(query.IsActive, query.ToPageRequest(), cancellationToken);
        return PagedResponse<ServiceResponse>.From(page, ServiceResponse.From);
    }

    /// <summary>Послуга.</summary>
    /// <param name="id" example="88888888-8888-8888-8888-888888888801">Ідентифікатор послуги.</param>
    /// <param name="cancellationToken">Скасування запиту.</param>
    /// <response code="200">Послуга.</response>
    /// <response code="404">Послугу не знайдено.</response>
    [HttpGet("{id:guid}")]
    [AllowedRoles(Roles.Administrator, Roles.Receptionist)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ServiceResponse> Get(Guid id, CancellationToken cancellationToken)
        => ServiceResponse.From(await services.GetAsync(id, cancellationToken));

    /// <summary>Створити послугу.</summary>
    /// <param name="request">Поля послуги.</param>
    /// <param name="cancellationToken">Скасування запиту.</param>
    /// <response code="201">Послугу створено; адреса — у заголовку Location.</response>
    /// <response code="400">Помилки в полях.</response>
    /// <response code="409">Послуга з такою назвою вже існує.</response>
    [HttpPost]
    [AllowedRoles(Roles.Administrator)]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ServiceResponse>> Create(ServiceRequest request, CancellationToken cancellationToken)
    {
        var service = await services.CreateAsync(request.ToData(), cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = service.Id }, ServiceResponse.From(service));
    }

    /// <summary>Змінити послугу.</summary>
    /// <remarks>Нова ціна діє для нових позицій рахунків; виставлені рахунки зберігають скопійовані назву й ціну.</remarks>
    /// <param name="id" example="88888888-8888-8888-8888-888888888801">Ідентифікатор послуги.</param>
    /// <param name="request">Нові значення полів.</param>
    /// <param name="cancellationToken">Скасування запиту.</param>
    /// <response code="200">Послуга після зміни.</response>
    /// <response code="400">Помилки в полях.</response>
    /// <response code="404">Послугу не знайдено.</response>
    /// <response code="409">Послуга з такою назвою вже існує.</response>
    [HttpPut("{id:guid}")]
    [AllowedRoles(Roles.Administrator)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ServiceResponse> Update(Guid id, ServiceRequest request, CancellationToken cancellationToken)
        => ServiceResponse.From(await services.UpdateAsync(id, request.ToData(), cancellationToken));

    /// <summary>Деактивувати послугу.</summary>
    /// <remarks>Послугу, яку вже додано до рахунків, не видаляють, а деактивують (UC-11).</remarks>
    /// <param name="id" example="88888888-8888-8888-8888-888888888801">Ідентифікатор послуги.</param>
    /// <param name="cancellationToken">Скасування запиту.</param>
    /// <response code="200">Послуга після зміни.</response>
    /// <response code="404">Послугу не знайдено.</response>
    [HttpPost("{id:guid}/deactivate")]
    [AllowedRoles(Roles.Administrator)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ServiceResponse> Deactivate(Guid id, CancellationToken cancellationToken)
        => ServiceResponse.From(await services.SetActiveAsync(id, isActive: false, cancellationToken));

    /// <summary>Знову активувати послугу.</summary>
    /// <param name="id" example="88888888-8888-8888-8888-888888888801">Ідентифікатор послуги.</param>
    /// <param name="cancellationToken">Скасування запиту.</param>
    /// <response code="200">Послуга після зміни.</response>
    /// <response code="404">Послугу не знайдено.</response>
    [HttpPost("{id:guid}/activate")]
    [AllowedRoles(Roles.Administrator)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ServiceResponse> Activate(Guid id, CancellationToken cancellationToken)
        => ServiceResponse.From(await services.SetActiveAsync(id, isActive: true, cancellationToken));

    /// <summary>Видалити послугу.</summary>
    /// <remarks>Лише послугу, якої ще немає в жодному рахунку (UC-11).</remarks>
    /// <param name="id">Ідентифікатор послуги.</param>
    /// <param name="cancellationToken">Скасування запиту.</param>
    /// <response code="204">Послугу видалено.</response>
    /// <response code="404">Послугу не знайдено.</response>
    /// <response code="409">Послуга використовується; деактивуйте її.</response>
    // No example id, unlike the other actions: "Try it out" in Swagger would delete a seeded record.
    [HttpDelete("{id:guid}")]
    [AllowedRoles(Roles.Administrator)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await services.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
