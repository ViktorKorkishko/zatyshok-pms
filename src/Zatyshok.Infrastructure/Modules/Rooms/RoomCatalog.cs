using Microsoft.EntityFrameworkCore;
using Zatyshok.Domain.Modules.Rooms;
using Zatyshok.Domain.Modules.Rooms.Contracts;
using Zatyshok.Infrastructure.Persistence;

namespace Zatyshok.Infrastructure.Modules.Rooms;

/// <summary>Hotels, categories and night prices for other modules (<see cref="IRoomCatalog"/>, table 9.6).</summary>
internal sealed class RoomCatalog(ZatyshokDbContext db, TimeProvider time) : IRoomCatalog
{
    public async Task<HotelInfo?> FindHotelAsync(Guid hotelId, CancellationToken cancellationToken)
    {
        var hotel = await db.Hotels.AsNoTracking().SingleOrDefaultAsync(h => h.Id == hotelId, cancellationToken);
        return hotel is null ? null : ToInfo(hotel);
    }

    public async Task<IReadOnlyList<HotelInfo>> FindActiveHotelsInCityAsync(string city, CancellationToken cancellationToken)
    {
        // A guest types the city, so "одеса" must find "Одеса". The comparison is made here, not in SQL:
        // lower() in PostgreSQL depends on the collation of the database (Collations.Ukrainian), while
        // the network has at most 20 hotels (NFR-15), so reading all the active ones costs nothing.
        var wanted = city.Trim();
        var hotels = await db.Hotels.AsNoTracking()
            .Where(h => h.IsActive)
            .OrderBy(h => h.Name).ThenBy(h => h.Id)
            .ToListAsync(cancellationToken);

        return hotels
            .Where(h => string.Equals(h.City.Trim(), wanted, StringComparison.OrdinalIgnoreCase))
            .Select(ToInfo)
            .ToList();
    }

    public async Task<IReadOnlyList<RoomCategoryInfo>> GetCategoriesAsync(CancellationToken cancellationToken)
        => await db.RoomCategories.AsNoTracking()
            .OrderBy(c => c.Name)
            .Select(c => new RoomCategoryInfo(c.Id, c.Name, c.Description, c.Capacity, c.IsActive))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<StayPrices>> GetNightPricesAsync(
        IReadOnlyCollection<Guid> hotelIds,
        IReadOnlyCollection<Guid> roomCategoryIds,
        DateOnly checkIn,
        DateOnly checkOut,
        CancellationToken cancellationToken)
    {
        var hotels = hotelIds.ToArray();
        var categories = roomCategoryIds.ToArray();
        var lastNight = checkOut.AddDays(-1);

        var rates = await db.Rates.AsNoTracking()
            .Where(r => hotels.Contains(r.HotelId) && categories.Contains(r.RoomCategoryId))
            // Only the seasonal periods that share a night with the stay: the others price no night of it.
            .Include(r => r.SeasonRates.Where(s => s.StartDate <= lastNight && s.EndDate >= checkIn))
            .ToListAsync(cancellationToken);

        // The prices themselves are the domain rule of Rate (FR-08), the same for search and booking.
        return rates
            .Select(r => new StayPrices(r.HotelId, r.RoomCategoryId, r.GetNightPrices(checkIn, checkOut)))
            .ToList();
    }

    private HotelInfo ToInfo(Hotel hotel)
        => new(hotel.Id, hotel.Name, hotel.City, hotel.Address, hotel.IsActive, hotel.GetLocalDate(time.GetUtcNow().UtcDateTime));
}
