using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Zatyshok.Domain.Common;
using Zatyshok.Domain.Modules.Rooms;
using Zatyshok.Infrastructure.Persistence;

namespace Zatyshok.Infrastructure.Modules.Rooms;

/// <summary>
/// Editable fields of a room (FR-06). A null capacity (only for a new room: the API requires it in an update)
/// takes the base capacity of the category; a given one is not smaller than it (<see cref="RoomService"/>).
/// The hotel is not here: a room never moves to another hotel.
/// </summary>
public record RoomData(string Number, int Floor, Guid RoomCategoryId, int? Capacity, string? Amenities, string? Notes);

/// <summary>Filters of the room list; null means "any".</summary>
public record RoomFilter(Guid? HotelId, Guid? RoomCategoryId, RoomStatus? Status, bool? IsActive, int? Floor);

/// <summary>
/// Rooms of the hotels (FR-06, UC-08) and their manual state changes (FR-07). Every query is limited
/// by the hotel scope of the user (FR-02, 9.6): a room of another hotel is "not found", and a new
/// room or a filter for another hotel is forbidden.
/// </summary>
public class RoomService(ZatyshokDbContext db, IHotelScope scope, ILogger<RoomService> logger)
{
    public Task<PagedList<Room>> ListAsync(RoomFilter filter, PageRequest page, CancellationToken cancellationToken)
    {
        var query = Scoped(db.Rooms.AsNoTracking().Include(r => r.Category));
        if (filter.HotelId is { } hotelId)
        {
            scope.EnsureIncludes(hotelId);
            query = query.Where(r => r.HotelId == hotelId);
        }

        if (filter.RoomCategoryId is { } categoryId)
        {
            query = query.Where(r => r.RoomCategoryId == categoryId);
        }

        if (filter.Status is { } status)
        {
            query = query.Where(r => r.Status == status);
        }

        if (filter.IsActive is { } isActive)
        {
            query = query.Where(r => r.IsActive == isActive);
        }

        if (filter.Floor is { } floor)
        {
            query = query.Where(r => r.Floor == floor);
        }

        // By hotel, then as a person walks the hotel: floor by floor. A number is unique only
        // within its hotel, so HotelId keeps two hotels with the same name apart.
        return query
            .OrderBy(r => r.Hotel.Name).ThenBy(r => r.HotelId).ThenBy(r => r.Floor).ThenBy(r => r.Number)
            .ToPagedListAsync(page, cancellationToken);
    }

    /// <summary>The room with its category, for the response.</summary>
    public async Task<Room> GetAsync(Guid id, CancellationToken cancellationToken)
        => await Scoped(db.Rooms.AsNoTracking().Include(r => r.Category)).SingleOrDefaultAsync(r => r.Id == id, cancellationToken)
           ?? throw NotFound();

    public async Task<Room> CreateAsync(Guid hotelId, RoomData data, CancellationToken cancellationToken)
    {
        scope.EnsureIncludes(hotelId);
        var hotel = await db.Hotels.AsNoTracking().SingleOrDefaultAsync(h => h.Id == hotelId, cancellationToken)
            ?? throw new NotFoundException("Готель не знайдено.");
        if (!hotel.IsActive)
        {
            throw new DomainException("Готель деактивовано. Щоб додати номер, спершу активуйте готель.");
        }

        var category = await FindCategoryAsync(data.RoomCategoryId, cancellationToken);
        EnsureActive(category);

        var room = new Room { HotelId = hotelId, RoomCategoryId = category.Id, Number = data.Number };
        Apply(room, data, category);
        db.Rooms.Add(room);
        await SaveAsync(room, cancellationToken);

        logger.LogInformation("Room {RoomId} ({RoomNumber}) created in hotel {HotelId}.", room.Id, room.Number, room.HotelId);
        return await GetAsync(room.Id, cancellationToken);
    }

    /// <summary>
    /// Replaces the fields of the room. A new category takes a room away from the old one, so the
    /// rate of the old pair is locked first (12.8.2); the room must not be held by a booking.
    /// </summary>
    public async Task<Room> UpdateAsync(Guid id, RoomData data, CancellationToken cancellationToken)
    {
        var room = await FindAsync(id, cancellationToken);
        var category = await FindCategoryAsync(data.RoomCategoryId, cancellationToken);

        await using var transaction = await RateLock.BeginAsync(db, cancellationToken);
        if (category.Id != room.RoomCategoryId)
        {
            // A room may stay in a category deactivated later, but cannot move into one.
            EnsureActive(category);
            await RateLock.LockAsync(db, room.HotelId, room.RoomCategoryId, cancellationToken);
            room.ChangeCategory(category.Id);
        }

        Apply(room, data, category);
        await SaveAsync(room, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return await GetAsync(room.Id, cancellationToken);
    }

    /// <summary>Free or Dirty → OutOfOrder (FR-07, FR-30): the room leaves availability.</summary>
    public Task<Room> MarkOutOfOrderAsync(Guid id, CancellationToken cancellationToken)
        => ChangeAsync(id, "out of order", room => room.MarkOutOfOrder(), reducesAvailability: true, cancellationToken);

    /// <summary>OutOfOrder → Dirty (FR-07): the room returns to availability after cleaning.</summary>
    public Task<Room> ReturnToServiceAsync(Guid id, CancellationToken cancellationToken)
        => ChangeAsync(id, "returned to service", room => room.ReturnToService(), reducesAvailability: false, cancellationToken);

    /// <summary>A room with history is deactivated instead of deleted (UC-08); not while occupied (UC-08 A2).</summary>
    public Task<Room> DeactivateAsync(Guid id, CancellationToken cancellationToken)
        => ChangeAsync(id, "deactivated", room => room.Deactivate(), reducesAvailability: true, cancellationToken);

    public Task<Room> ActivateAsync(Guid id, CancellationToken cancellationToken)
        => ChangeAsync(id, "activated", room => room.Activate(), reducesAvailability: false, cancellationToken);

    /// <summary>
    /// Deletes a room without history, e.g. one created by mistake. Bookings, cleaning tasks and
    /// faults refer to a room with ON DELETE RESTRICT, so PostgreSQL rejects the deletion of a room
    /// that has them (table 12.12); Rooms cannot read those tables to check beforehand (9.4.1, rule 1).
    /// </summary>
    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var room = await FindAsync(id, cancellationToken);

        await using var transaction = await RateLock.BeginAsync(db, cancellationToken);
        await RateLock.LockAsync(db, room.HotelId, room.RoomCategoryId, cancellationToken);
        db.Rooms.Remove(room);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.IsForeignKeyViolation())
        {
            throw new DomainException(
                "Номер має бронювання, завдання на прибирання або несправності, тому його не можна видалити. Деактивуйте його.");
        }

        await transaction.CommitAsync(cancellationToken);
        logger.LogInformation("Room {RoomId} ({RoomNumber}) of hotel {HotelId} deleted.", room.Id, room.Number, room.HotelId);
    }

    /// <summary>
    /// One state change of a room through its domain method, which rejects a forbidden transition
    /// (InvalidTransitionException → 409). A change that reduces availability takes the rate lock first.
    /// </summary>
    private async Task<Room> ChangeAsync(
        Guid id, string action, Action<Room> change, bool reducesAvailability, CancellationToken cancellationToken)
    {
        var room = await FindAsync(id, cancellationToken);
        var oldStatus = room.Status;

        await using var transaction = await RateLock.BeginAsync(db, cancellationToken);
        if (reducesAvailability)
        {
            await RateLock.LockAsync(db, room.HotelId, room.RoomCategoryId, cancellationToken);
        }

        change(room);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        logger.LogInformation(
            "Room {RoomId} ({RoomNumber}) of hotel {HotelId} {Action}: status {OldStatus} -> {NewStatus}, active {IsActive}.",
            room.Id, room.Number, room.HotelId, action, oldStatus, room.Status, room.IsActive);
        return await GetAsync(room.Id, cancellationToken);
    }

    // The hotel scope (9.6): the only place the room queries are limited to the user's hotel.
    private IQueryable<Room> Scoped(IQueryable<Room> rooms)
        => scope.HotelId is { } hotelId ? rooms.Where(r => r.HotelId == hotelId) : rooms;

    private async Task<Room> FindAsync(Guid id, CancellationToken cancellationToken)
        => await Scoped(db.Rooms).SingleOrDefaultAsync(r => r.Id == id, cancellationToken) ?? throw NotFound();

    private async Task<RoomCategory> FindCategoryAsync(Guid id, CancellationToken cancellationToken)
        => await db.RoomCategories.AsNoTracking().SingleOrDefaultAsync(c => c.Id == id, cancellationToken)
           ?? throw new NotFoundException("Категорію номерів не знайдено.");

    private static void EnsureActive(RoomCategory category)
    {
        if (!category.IsActive)
        {
            throw new DomainException($"Категорію «{category.Name}» деактивовано. Оберіть діючу категорію.");
        }
    }

    private async Task SaveAsync(Room room, CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.IsUniqueViolation("IX_Rooms_HotelId_Number"))
        {
            throw new DomainException($"Номер «{room.Number}» у цьому готелі вже є.");
        }
    }

    private static void Apply(Room room, RoomData data, RoomCategory category)
    {
        var capacity = data.Capacity ?? category.Capacity;
        EnsureHoldsTheBaseCapacity(capacity, category);

        room.Number = data.Number.Trim();
        room.Floor = data.Floor;
        room.Capacity = capacity;
        room.Amenities = Text.OrNull(data.Amenities);
        room.Notes = Text.OrNull(data.Notes);
    }

    /// <summary>
    /// A guest books a category, not a room, and the search offers a category for up to its base capacity
    /// (FR-05, FR-10). So every room of the category holds at least that many guests; otherwise the room
    /// given out at check-in (FR-19) could be too small for the booking. A room may hold more (e.g. an extra
    /// bed), but the search does not count on it. RoomCategoryService keeps the rule when the base grows.
    /// </summary>
    private static void EnsureHoldsTheBaseCapacity(int capacity, RoomCategory category)
    {
        if (capacity < category.Capacity)
        {
            throw new InvalidRequestException(
                nameof(RoomData.Capacity),
                $"Місткість номера не може бути меншою за базову місткість категорії «{category.Name}» ({category.Capacity}): гості бронюють категорію.");
        }
    }

    private static NotFoundException NotFound() => new("Номер не знайдено.");
}
