using Microsoft.EntityFrameworkCore;
using Zatyshok.Domain.Common;
using Zatyshok.Domain.Modules.Rooms;
using Zatyshok.Infrastructure.Persistence;

namespace Zatyshok.Infrastructure.Modules.Rooms;

/// <summary>Editable fields of a hotel (FR-05): the same for a new hotel and for a full update.</summary>
public record HotelData(
    string Name,
    string City,
    string Address,
    int Stars,
    string TimeZoneId,
    string? Phone,
    string? Email,
    int CancellationDeadlineDaysBefore,
    TimeOnly CancellationDeadlineTime,
    TimeOnly CheckInTime,
    TimeOnly CheckOutTime);

/// <summary>Filters of the hotel list; null means "any".</summary>
public record HotelFilter(string? City, bool? IsActive);

/// <summary>
/// Hotels of the network (FR-05, UC-11). Not limited by the hotel scope: the hotel list is the
/// public catalog of the portal (UC-04), and only the network-wide administrator changes it.
/// </summary>
public class HotelService(ZatyshokDbContext db)
{
    public Task<PagedList<Hotel>> ListAsync(HotelFilter filter, PageRequest page, CancellationToken cancellationToken)
    {
        var query = db.Hotels.AsNoTracking();
        if (filter.City is { } city)
        {
            var trimmed = city.Trim();
            query = query.Where(h => h.City == trimmed);
        }

        if (filter.IsActive is { } isActive)
        {
            query = query.Where(h => h.IsActive == isActive);
        }

        // Id as the last key keeps the order stable for hotels with the same name (see ToPagedListAsync).
        return query.OrderBy(h => h.Name).ThenBy(h => h.Id).ToPagedListAsync(page, cancellationToken);
    }

    public async Task<Hotel> GetAsync(Guid id, CancellationToken cancellationToken)
        => await db.Hotels.AsNoTracking().SingleOrDefaultAsync(h => h.Id == id, cancellationToken)
           ?? throw NotFound();

    public async Task<Hotel> CreateAsync(HotelData data, CancellationToken cancellationToken)
    {
        // The required members first, then Apply sets every field the same way as an update does.
        var hotel = new Hotel { Name = data.Name, City = data.City, Address = data.Address, TimeZoneId = data.TimeZoneId };
        Apply(hotel, data);

        db.Hotels.Add(hotel);
        await db.SaveChangesAsync(cancellationToken);
        return hotel;
    }

    /// <summary>
    /// Replaces the fields of the hotel. New cancellation rules apply to bookings made after the
    /// change: a booking keeps the deadline computed when it was made (FR-15, 12.3.3).
    /// </summary>
    public async Task<Hotel> UpdateAsync(Guid id, HotelData data, CancellationToken cancellationToken)
    {
        var hotel = await FindAsync(id, cancellationToken);
        Apply(hotel, data);
        await db.SaveChangesAsync(cancellationToken);
        return hotel;
    }

    /// <summary>A hotel with bookings is deactivated instead of deleted (FR-05); it can be activated again.</summary>
    public async Task<Hotel> SetActiveAsync(Guid id, bool isActive, CancellationToken cancellationToken)
    {
        var hotel = await FindAsync(id, cancellationToken);
        hotel.IsActive = isActive;
        await db.SaveChangesAsync(cancellationToken);
        return hotel;
    }

    /// <summary>
    /// Deletes a hotel that nothing refers to yet, e.g. one created by mistake (8.3). Rooms, rates,
    /// bookings and staff accounts refer to a hotel with ON DELETE RESTRICT, so PostgreSQL itself
    /// rejects the deletion of a hotel that has them (table 12.12); Rooms cannot see the tables of
    /// Bookings and Identity to check this beforehand (9.4.1, rule 1).
    /// </summary>
    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var hotel = await FindAsync(id, cancellationToken);
        db.Hotels.Remove(hotel);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.IsForeignKeyViolation())
        {
            throw new DomainException(
                "Готель має номери, тарифи, бронювання або облікові записи персоналу, тому його не можна видалити. Деактивуйте його.");
        }
    }

    private async Task<Hotel> FindAsync(Guid id, CancellationToken cancellationToken)
        => await db.Hotels.SingleOrDefaultAsync(h => h.Id == id, cancellationToken) ?? throw NotFound();

    private static void Apply(Hotel hotel, HotelData data)
    {
        hotel.Name = data.Name.Trim();
        hotel.City = data.City.Trim();
        hotel.Address = data.Address.Trim();
        hotel.Stars = data.Stars;
        hotel.TimeZoneId = data.TimeZoneId.Trim();
        hotel.Phone = Text.OrNull(data.Phone);
        hotel.Email = Text.OrNull(data.Email);
        hotel.CancellationDeadlineDaysBefore = data.CancellationDeadlineDaysBefore;
        hotel.CancellationDeadlineTime = data.CancellationDeadlineTime;
        hotel.CheckInTime = data.CheckInTime;
        hotel.CheckOutTime = data.CheckOutTime;
    }

    private static NotFoundException NotFound() => new("Готель не знайдено.");
}
