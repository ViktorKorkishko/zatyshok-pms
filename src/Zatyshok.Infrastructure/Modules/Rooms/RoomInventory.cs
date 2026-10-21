using Microsoft.EntityFrameworkCore;
using Zatyshok.Domain.Modules.Rooms;
using Zatyshok.Domain.Modules.Rooms.Contracts;
using Zatyshok.Infrastructure.Persistence;

namespace Zatyshok.Infrastructure.Modules.Rooms;

/// <summary>The room inventory for other modules (<see cref="IRoomInventory"/>, table 9.6).</summary>
internal sealed class RoomInventory(ZatyshokDbContext db) : IRoomInventory
{
    public async Task<IReadOnlyList<SellableRooms>> GetSellableRoomsAsync(
        IReadOnlyCollection<Guid> hotelIds, IReadOnlyCollection<Guid> roomCategoryIds, CancellationToken cancellationToken)
    {
        var hotels = hotelIds.ToArray();
        var categories = roomCategoryIds.ToArray();

        // The rule of listing 12.1: "IsActive" AND "Status" <> 'OutOfOrder'.
        var rooms = await db.Rooms.AsNoTracking()
            .Where(r => hotels.Contains(r.HotelId) && categories.Contains(r.RoomCategoryId)
                        && r.IsActive && r.Status != RoomStatus.OutOfOrder)
            .Select(r => new { r.HotelId, r.RoomCategoryId, r.Amenities })
            .ToListAsync(cancellationToken);

        // Counted here rather than with GROUP BY in SQL, because the amenities of the rooms are compared
        // as lists. A city has a few hotels with tens of rooms each (at most 2000 in the network, NFR-15).
        return rooms
            .GroupBy(r => (r.HotelId, r.RoomCategoryId))
            .Select(g => new SellableRooms(g.Key.HotelId, g.Key.RoomCategoryId, g.Count(), CommonAmenities(g.Select(r => r.Amenities))))
            .ToList();
    }

    /// <summary>
    /// Amenities found in every list, in the order of the first list. A room keeps them as text separated
    /// by commas (12.1.2), e.g. "Wi-Fi, кондиціонер, телевізор"; case and spaces do not matter.
    /// </summary>
    private static IReadOnlyList<string> CommonAmenities(IEnumerable<string?> lists)
    {
        List<string>? common = null;
        foreach (var list in lists)
        {
            var amenities = (list ?? string.Empty)
                .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            common = common is null
                ? amenities
                : common.Where(a => amenities.Contains(a, StringComparer.OrdinalIgnoreCase)).ToList();
        }

        return common ?? [];
    }
}
