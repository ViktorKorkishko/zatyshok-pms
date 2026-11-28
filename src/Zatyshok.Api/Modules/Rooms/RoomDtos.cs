using FluentValidation;
using Zatyshok.Api.Common.Paging;
using Zatyshok.Api.Common.Validation;
using Zatyshok.Domain.Modules.Rooms;
using Zatyshok.Infrastructure.Modules.Rooms;

namespace Zatyshok.Api.Modules.Rooms;

/// <summary>Поля номера для повної зміни (FR-06). Готель номера не змінюється.</summary>
public class RoomRequest
{
    /// <summary>Номер на дверях, унікальний у межах готелю, до 16 символів.</summary>
    /// <example>305</example>
    public string Number { get; init; } = string.Empty;

    /// <summary>Поверх.</summary>
    /// <example>3</example>
    public int? Floor { get; init; }

    /// <summary>Категорія номера; для нового номера або зміни категорії — лише діюча.</summary>
    /// <example>11111111-1111-1111-1111-111111111101</example>
    public Guid RoomCategoryId { get; init; }

    /// <summary>
    /// Місткість номера: не менша за базову місткість категорії, бо гості бронюють категорію; більша —
    /// наприклад, з додатковим ліжком. Обов'язкова під час зміни номера; для нового номера, якщо не
    /// вказано, — базова місткість категорії.
    /// </summary>
    /// <example>3</example>
    public int? Capacity { get; init; }

    /// <summary>Перелік зручностей через кому, до 1000 символів.</summary>
    /// <example>кондиціонер, сейф, балкон</example>
    public string? Amenities { get; init; }

    /// <summary>Примітки, до 2000 символів; сюди записують і несправність у зайнятому номері (UC-16 А1).</summary>
    /// <example>Скриплять двері шафи.</example>
    public string? Notes { get; init; }

    // The validators have checked that the floor is given (and the capacity of an update).
    internal RoomData ToData() => new(Number, Floor!.Value, RoomCategoryId, Capacity, Amenities, Notes);
}

/// <summary>Новий номер готелю (FR-06).</summary>
public sealed class NewRoomRequest : RoomRequest
{
    /// <summary>Готель, до якого належить номер.</summary>
    /// <example>22222222-2222-2222-2222-222222222201</example>
    public Guid HotelId { get; init; }
}

/// <summary>Номер готелю.</summary>
public sealed class RoomResponse
{
    /// <summary>Ідентифікатор номера.</summary>
    /// <example>33333333-3333-3333-3333-333333330000</example>
    public Guid Id { get; init; }

    /// <summary>Готель номера.</summary>
    /// <example>22222222-2222-2222-2222-222222222201</example>
    public Guid HotelId { get; init; }

    /// <summary>Номер на дверях.</summary>
    /// <example>101</example>
    public required string Number { get; init; }

    /// <summary>Поверх.</summary>
    /// <example>1</example>
    public int Floor { get; init; }

    /// <summary>Категорія номера.</summary>
    /// <example>11111111-1111-1111-1111-111111111101</example>
    public Guid RoomCategoryId { get; init; }

    /// <summary>Назва категорії.</summary>
    /// <example>Standard</example>
    public required string RoomCategoryName { get; init; }

    /// <summary>Місткість номера.</summary>
    /// <example>2</example>
    public int Capacity { get; init; }

    /// <summary>Перелік зручностей.</summary>
    /// <example>кондиціонер, сейф</example>
    public string? Amenities { get; init; }

    /// <summary>Примітки.</summary>
    /// <example>Скриплять двері шафи.</example>
    public string? Notes { get; init; }

    /// <summary>
    /// Стан номера (рисунок 6.2): Free — вільний, Reserved — заброньований (попередньо призначений),
    /// Occupied — зайнятий, Dirty — потребує прибирання, OutOfOrder — на ремонті.
    /// </summary>
    /// <example>Free</example>
    public RoomStatus Status { get; init; }

    /// <summary>Чи діє номер; деактивований номер не враховується в доступності (UC-08).</summary>
    /// <example>true</example>
    public bool IsActive { get; init; }

    internal static RoomResponse From(Room room) => new()
    {
        Id = room.Id,
        HotelId = room.HotelId,
        Number = room.Number,
        Floor = room.Floor,
        RoomCategoryId = room.RoomCategoryId,
        RoomCategoryName = room.Category.Name,
        Capacity = room.Capacity,
        Amenities = room.Amenities,
        Notes = room.Notes,
        Status = room.Status,
        IsActive = room.IsActive,
    };
}

/// <summary>Фільтри списку номерів.</summary>
public sealed class RoomListQuery : PageQuery
{
    /// <summary>Лише номери цього готелю.</summary>
    /// <example>22222222-2222-2222-2222-222222222201</example>
    public Guid? HotelId { get; init; }

    /// <summary>Лише номери цієї категорії.</summary>
    public Guid? RoomCategoryId { get; init; }

    /// <summary>Лише номери в цьому стані: Free, Reserved, Occupied, Dirty або OutOfOrder.</summary>
    public RoomStatus? Status { get; init; }

    /// <summary>
    /// true (типово) — лише діючі номери, false — лише деактивовані, наприклад щоб знову активувати номер.
    /// Деактивований номер виключається зі списків (UC-08), тож типово його немає.
    /// </summary>
    public bool IsActive { get; init; } = true;

    /// <summary>Лише номери цього поверху.</summary>
    public int? Floor { get; init; }
}

/// <summary>
/// Rules of <see cref="RoomRequest"/>, a full update: the shared ones, and the capacity is required, so that
/// an update without it does not reset the capacity to the base capacity of the category.
/// </summary>
public sealed class RoomRequestValidator : RequestValidator<RoomRequest>
{
    /// <summary>Creates the rules.</summary>
    public RoomRequestValidator()
    {
        Include(new RoomFieldsValidator());
        RuleFor(r => r.Capacity)
            .NotNull().WithMessage("Вкажіть місткість номера.");
    }
}

/// <summary>Rules of <see cref="NewRoomRequest"/>: the shared ones and the hotel; an omitted capacity takes the base one.</summary>
public sealed class NewRoomRequestValidator : RequestValidator<NewRoomRequest>
{
    /// <summary>Creates the rules.</summary>
    public NewRoomRequestValidator()
    {
        Include(new RoomFieldsValidator());
        RuleFor(r => r.HotelId)
            .NotEmpty().WithMessage("Вкажіть готель.");
    }
}

/// <summary>
/// Rules of the fields a new room and an update share; the lengths are those of rooms.Rooms. Internal, so
/// that Program does not register it as the validator of <see cref="RoomRequest"/>.
/// </summary>
internal sealed class RoomFieldsValidator : RequestValidator<RoomRequest>
{
    /// <summary>Creates the rules.</summary>
    public RoomFieldsValidator()
    {
        RuleFor(r => r.Number)
            .NotEmpty().WithMessage("Вкажіть номер на дверях.")
            .MaximumLength(16).WithMessage("Номер має бути не довшим за 16 символів.");

        // Nullable, because 0 is a floor: a missing floor would otherwise become 0 without an error.
        RuleFor(r => r.Floor)
            .NotNull().WithMessage("Вкажіть поверх.");
        RuleFor(r => r.RoomCategoryId)
            .NotEmpty().WithMessage("Вкажіть категорію номера.");
        RuleFor(r => r.Capacity)
            .GreaterThan(0).WithMessage("Місткість має бути більшою за 0.");
        RuleFor(r => r.Amenities)
            .MaximumLength(1000).WithMessage("Перелік зручностей має бути не довшим за 1000 символів.");
        RuleFor(r => r.Notes)
            .MaximumLength(2000).WithMessage("Примітки мають бути не довшими за 2000 символів.");
    }
}

/// <summary>Rules of <see cref="RoomListQuery"/>.</summary>
public sealed class RoomListQueryValidator : RequestValidator<RoomListQuery>
{
    /// <summary>Creates the rules.</summary>
    public RoomListQueryValidator()
    {
        Include(new PageQueryValidator());
    }
}
