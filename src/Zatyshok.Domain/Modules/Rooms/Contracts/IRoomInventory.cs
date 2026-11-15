namespace Zatyshok.Domain.Modules.Rooms.Contracts;

/// <summary>Rooms of a pair «hotel – category» that can be sold (12.8.2): active and not out of order.</summary>
/// <param name="Count">How many such rooms the pair has.</param>
/// <param name="Amenities">
/// Amenities that every one of these rooms has (FR-10). A guest books a category and gets one of its rooms
/// at check-in (FR-19), so only what all the rooms have is promised.
/// </param>
public sealed record SellableRooms(Guid HotelId, Guid RoomCategoryId, int Count, IReadOnlyList<string> Amenities);

/// <summary>
/// Public contract of the module «Номерний фонд і тарифи» (table 9.6): the room inventory for other
/// modules, which may not read the rooms.* tables themselves (9.4.1, rule 1). T3 has the part that the
/// availability search needs. The rate lock of 12.8.2 comes here with booking creation in T5, the room
/// state changes of check-in (reserve, occupy, release) in T6.
/// </summary>
public interface IRoomInventory
{
    /// <summary>
    /// Sellable rooms of every pair of <paramref name="hotelIds"/> × <paramref name="roomCategoryIds"/>, by the
    /// rule of listing 12.1: active rooms that are not out of order (FR-09). A room in any other state (free,
    /// reserved, occupied, dirty) counts: which nights it is taken is decided by the bookings, not by its
    /// current state. A pair without sellable rooms is not in the result.
    /// </summary>
    Task<IReadOnlyList<SellableRooms>> GetSellableRoomsAsync(
        IReadOnlyCollection<Guid> hotelIds, IReadOnlyCollection<Guid> roomCategoryIds, CancellationToken cancellationToken);
}
