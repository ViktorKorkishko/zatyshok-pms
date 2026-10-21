using Microsoft.AspNetCore.Mvc;
using Zatyshok.Api.Common.Authorization;
using Zatyshok.Api.Common.Paging;
using Zatyshok.Infrastructure.Modules.Rooms;

namespace Zatyshok.Api.Modules.Rooms;

/// <summary>
/// Категорії номерів — довідник усієї мережі (FR-05, UC-11). Список відкритий для всіх, бо
/// результати пошуку на порталі показують категорії з описом (FR-10); змінює їх адміністратор.
/// </summary>
[ApiController]
[Route("api/v1/room-categories")]
public class RoomCategoriesController(RoomCategoryService categories) : ControllerBase
{
    /// <summary>Список категорій номерів.</summary>
    /// <remarks>Категорії впорядковано за назвою.</remarks>
    /// <param name="query">Фільтри і сторінка.</param>
    /// <param name="cancellationToken">Скасування запиту.</param>
    /// <response code="200">Сторінка списку.</response>
    /// <response code="400">Некоректні параметри сторінки.</response>
    [HttpGet]
    [AllowedRoles(Roles.Anonymous)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<PagedResponse<RoomCategoryResponse>> List([FromQuery] RoomCategoryListQuery query, CancellationToken cancellationToken)
    {
        var page = await categories.ListAsync(query.IsActive, query.ToPageRequest(), cancellationToken);
        return PagedResponse<RoomCategoryResponse>.From(page, RoomCategoryResponse.From);
    }

    /// <summary>Категорія номерів.</summary>
    /// <param name="id" example="11111111-1111-1111-1111-111111111101">Ідентифікатор категорії.</param>
    /// <param name="cancellationToken">Скасування запиту.</param>
    /// <response code="200">Категорія.</response>
    /// <response code="404">Категорію не знайдено.</response>
    [HttpGet("{id:guid}")]
    [AllowedRoles(Roles.Anonymous)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<RoomCategoryResponse> Get(Guid id, CancellationToken cancellationToken)
        => RoomCategoryResponse.From(await categories.GetAsync(id, cancellationToken));

    /// <summary>Створити категорію номерів.</summary>
    /// <param name="request">Поля категорії.</param>
    /// <param name="cancellationToken">Скасування запиту.</param>
    /// <response code="201">Категорію створено; адреса — у заголовку Location.</response>
    /// <response code="400">Помилки в полях.</response>
    /// <response code="409">Категорія з такою назвою вже існує.</response>
    [HttpPost]
    [AllowedRoles(Roles.Administrator)]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<RoomCategoryResponse>> Create(RoomCategoryRequest request, CancellationToken cancellationToken)
    {
        var category = await categories.CreateAsync(request.ToData(), cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = category.Id }, RoomCategoryResponse.From(category));
    }

    /// <summary>Змінити категорію номерів.</summary>
    /// <remarks>Місткість категорії — типова для нових номерів; наявні номери зберігають свою місткість (FR-06).</remarks>
    /// <param name="id" example="11111111-1111-1111-1111-111111111101">Ідентифікатор категорії.</param>
    /// <param name="request">Нові значення полів.</param>
    /// <param name="cancellationToken">Скасування запиту.</param>
    /// <response code="200">Категорія після зміни.</response>
    /// <response code="400">Помилки в полях.</response>
    /// <response code="404">Категорію не знайдено.</response>
    /// <response code="409">Категорія з такою назвою вже існує.</response>
    [HttpPut("{id:guid}")]
    [AllowedRoles(Roles.Administrator)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<RoomCategoryResponse> Update(Guid id, RoomCategoryRequest request, CancellationToken cancellationToken)
        => RoomCategoryResponse.From(await categories.UpdateAsync(id, request.ToData(), cancellationToken));

    /// <summary>Деактивувати категорію номерів.</summary>
    /// <remarks>Категорію, яку мають номери або бронювання, не видаляють, а деактивують (UC-11).</remarks>
    /// <param name="id" example="11111111-1111-1111-1111-111111111101">Ідентифікатор категорії.</param>
    /// <param name="cancellationToken">Скасування запиту.</param>
    /// <response code="200">Категорія після зміни.</response>
    /// <response code="404">Категорію не знайдено.</response>
    [HttpPost("{id:guid}/deactivate")]
    [AllowedRoles(Roles.Administrator)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<RoomCategoryResponse> Deactivate(Guid id, CancellationToken cancellationToken)
        => RoomCategoryResponse.From(await categories.SetActiveAsync(id, isActive: false, cancellationToken));

    /// <summary>Знову активувати категорію номерів.</summary>
    /// <param name="id" example="11111111-1111-1111-1111-111111111101">Ідентифікатор категорії.</param>
    /// <param name="cancellationToken">Скасування запиту.</param>
    /// <response code="200">Категорія після зміни.</response>
    /// <response code="404">Категорію не знайдено.</response>
    [HttpPost("{id:guid}/activate")]
    [AllowedRoles(Roles.Administrator)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<RoomCategoryResponse> Activate(Guid id, CancellationToken cancellationToken)
        => RoomCategoryResponse.From(await categories.SetActiveAsync(id, isActive: true, cancellationToken));

    /// <summary>Видалити категорію номерів.</summary>
    /// <remarks>Лише категорію, якої ще не мають номери, тарифи чи бронювання (UC-11).</remarks>
    /// <param name="id">Ідентифікатор категорії.</param>
    /// <param name="cancellationToken">Скасування запиту.</param>
    /// <response code="204">Категорію видалено.</response>
    /// <response code="404">Категорію не знайдено.</response>
    /// <response code="409">Категорія використовується; деактивуйте її.</response>
    // No example id, unlike the other actions: "Try it out" in Swagger would delete a seeded record.
    [HttpDelete("{id:guid}")]
    [AllowedRoles(Roles.Administrator)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await categories.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
