using Microsoft.AspNetCore.Mvc;
using Zatyshok.Api.Common.Authorization;
using Zatyshok.Api.Common.Paging;
using Zatyshok.Infrastructure.Modules.Rooms;

namespace Zatyshok.Api.Modules.Rooms;

/// <summary>
/// Номери готелів (FR-06, UC-08) і ручна зміна їхнього стану (FR-07). Користувач бачить і змінює
/// лише номери свого готелю (FR-02): номер іншого готелю — 404, явно вказаний інший готель — 403.
/// </summary>
/// <remarks>
/// The hotel scope is applied by <see cref="RoomService"/>, which takes the IHotelScope of the request
/// (9.6). Until T4 it is the whole network (WholeNetworkScope); T4 registers the scope of the signed-in
/// user, and this controller does not change.
/// </remarks>
[ApiController]
[Route("api/v1/rooms")]
public class RoomsController(RoomService rooms) : ControllerBase
{
    /// <summary>Список номерів.</summary>
    /// <remarks>Номери впорядковано за готелем, поверхом і номером.</remarks>
    /// <param name="query">Фільтри і сторінка.</param>
    /// <param name="cancellationToken">Скасування запиту.</param>
    /// <response code="200">Сторінка списку.</response>
    /// <response code="400">Некоректні фільтри або параметри сторінки.</response>
    /// <response code="403">Вказаний готель поза областю видимості користувача.</response>
    [HttpGet]
    [AllowedRoles(Roles.Administrator, Roles.HotelManager, Roles.Receptionist, Roles.HeadHousekeeper, Roles.Housekeeper)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<PagedResponse<RoomResponse>> List([FromQuery] RoomListQuery query, CancellationToken cancellationToken)
    {
        var filter = new RoomFilter(query.HotelId, query.RoomCategoryId, query.Status, query.IsActive, query.Floor);
        var page = await rooms.ListAsync(filter, query.ToPageRequest(), cancellationToken);
        return PagedResponse<RoomResponse>.From(page, RoomResponse.From);
    }

    /// <summary>Картка номера.</summary>
    /// <param name="id">Ідентифікатор номера.</param>
    /// <param name="cancellationToken">Скасування запиту.</param>
    /// <response code="200">Номер.</response>
    /// <response code="404">Номер не знайдено.</response>
    [HttpGet("{id:guid}")]
    [AllowedRoles(Roles.Administrator, Roles.HotelManager, Roles.Receptionist, Roles.HeadHousekeeper, Roles.Housekeeper)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<RoomResponse> Get(Guid id, CancellationToken cancellationToken)
        => RoomResponse.From(await rooms.GetAsync(id, cancellationToken));

    /// <summary>Створити номер.</summary>
    /// <remarks>Новий номер вільний і діючий. Готель і категорія мають бути діючими.</remarks>
    /// <param name="request">Поля номера.</param>
    /// <param name="cancellationToken">Скасування запиту.</param>
    /// <response code="201">Номер створено; адреса картки — у заголовку Location.</response>
    /// <response code="400">Помилки в полях.</response>
    /// <response code="403">Готель поза областю видимості користувача.</response>
    /// <response code="404">Готель або категорію не знайдено.</response>
    /// <response code="409">Такий номер у готелі вже є, або готель чи категорію деактивовано.</response>
    [HttpPost]
    [AllowedRoles(Roles.Administrator, Roles.HotelManager)]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<RoomResponse>> Create(NewRoomRequest request, CancellationToken cancellationToken)
    {
        var room = await rooms.CreateAsync(request.HotelId, request.ToData(), cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = room.Id }, RoomResponse.From(room));
    }

    /// <summary>Змінити номер.</summary>
    /// <remarks>
    /// Замінює всі поля номера. Категорію заброньованого або зайнятого номера змінити не можна: вона
    /// має збігатися з категорією бронювання.
    /// </remarks>
    /// <param name="id">Ідентифікатор номера.</param>
    /// <param name="request">Нові значення полів.</param>
    /// <param name="cancellationToken">Скасування запиту.</param>
    /// <response code="200">Номер після зміни.</response>
    /// <response code="400">Помилки в полях.</response>
    /// <response code="404">Номер або категорію не знайдено.</response>
    /// <response code="409">Такий номер у готелі вже є, категорію змінити не можна, або номер щойно змінив інший користувач.</response>
    [HttpPut("{id:guid}")]
    [AllowedRoles(Roles.Administrator, Roles.HotelManager)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<RoomResponse> Update(Guid id, RoomRequest request, CancellationToken cancellationToken)
        => RoomResponse.From(await rooms.UpdateAsync(id, request.ToData(), cancellationToken));

    /// <summary>Вивести номер з експлуатації («на ремонті»).</summary>
    /// <remarks>
    /// Переводить вільний номер або номер, що потребує прибирання, у стан «на ремонті»; номер
    /// виключається з доступності (FR-07, FR-30). Несправність у зайнятому номері записують у примітки,
    /// а з заброньованого номера спершу знімають призначення (UC-16 А1).
    /// </remarks>
    /// <param name="id">Ідентифікатор номера.</param>
    /// <param name="cancellationToken">Скасування запиту.</param>
    /// <response code="200">Номер після зміни.</response>
    /// <response code="404">Номер не знайдено.</response>
    /// <response code="409">Перехід заборонено з поточного стану номера.</response>
    [HttpPost("{id:guid}/out-of-order")]
    [AllowedRoles(Roles.Housekeeper, Roles.HeadHousekeeper, Roles.HotelManager)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<RoomResponse> MarkOutOfOrder(Guid id, CancellationToken cancellationToken)
        => RoomResponse.From(await rooms.MarkOutOfOrderAsync(id, cancellationToken));

    /// <summary>Повернути номер в експлуатацію.</summary>
    /// <remarks>Номер «на ремонті» переходить у стан «потребує прибирання» (FR-07).</remarks>
    /// <param name="id">Ідентифікатор номера.</param>
    /// <param name="cancellationToken">Скасування запиту.</param>
    /// <response code="200">Номер після зміни.</response>
    /// <response code="404">Номер не знайдено.</response>
    /// <response code="409">Номер не на ремонті.</response>
    [HttpPost("{id:guid}/return-to-service")]
    [AllowedRoles(Roles.HeadHousekeeper, Roles.HotelManager)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<RoomResponse> ReturnToService(Guid id, CancellationToken cancellationToken)
        => RoomResponse.From(await rooms.ReturnToServiceAsync(id, cancellationToken));

    /// <summary>Деактивувати номер.</summary>
    /// <remarks>
    /// Номер з історією проживання не видаляють, а деактивують: він зникає з доступності (UC-08).
    /// Зайнятий номер деактивувати не можна (UC-08 А2).
    /// </remarks>
    /// <param name="id">Ідентифікатор номера.</param>
    /// <param name="cancellationToken">Скасування запиту.</param>
    /// <response code="200">Номер після зміни.</response>
    /// <response code="404">Номер не знайдено.</response>
    /// <response code="409">Номер зайнятий.</response>
    [HttpPost("{id:guid}/deactivate")]
    [AllowedRoles(Roles.Administrator, Roles.HotelManager)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<RoomResponse> Deactivate(Guid id, CancellationToken cancellationToken)
        => RoomResponse.From(await rooms.DeactivateAsync(id, cancellationToken));

    /// <summary>Знову активувати номер.</summary>
    /// <param name="id">Ідентифікатор номера.</param>
    /// <param name="cancellationToken">Скасування запиту.</param>
    /// <response code="200">Номер після зміни.</response>
    /// <response code="404">Номер не знайдено.</response>
    [HttpPost("{id:guid}/activate")]
    [AllowedRoles(Roles.Administrator, Roles.HotelManager)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<RoomResponse> Activate(Guid id, CancellationToken cancellationToken)
        => RoomResponse.From(await rooms.ActivateAsync(id, cancellationToken));

    /// <summary>Видалити номер.</summary>
    /// <remarks>Лише номер без історії (бронювань, завдань, несправностей), наприклад створений помилково.</remarks>
    /// <param name="id">Ідентифікатор номера.</param>
    /// <param name="cancellationToken">Скасування запиту.</param>
    /// <response code="204">Номер видалено.</response>
    /// <response code="404">Номер не знайдено.</response>
    /// <response code="409">Номер має історію; деактивуйте його.</response>
    [HttpDelete("{id:guid}")]
    [AllowedRoles(Roles.Administrator, Roles.HotelManager)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await rooms.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
