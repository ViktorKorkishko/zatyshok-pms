using Zatyshok.Domain.Common;

namespace Zatyshok.Domain.Modules.Bookings;

/// <summary>Free rooms of a category on one night of a stay; negative when the night is overbooked.</summary>
public sealed record NightFreeRooms(DateOnly Night, int FreeRooms);

/// <summary>
/// The availability formula of FR-09 in its revision of 12.8.2 (listing 12.1 and «Формула доступності»):
/// for every night of the stay [check-in, check-out) the free rooms of a category in a hotel are the rooms
/// that can be sold minus the bookings of the category that include this night, and the stay has as many
/// rooms as its worst night.
/// <para>
/// Why per night: the literal formula of report 1 subtracts every booking whose period overlaps the
/// request. With 2 rooms and the bookings 10–12.10 and 12–14.10 it gives 2 − 2 = 0 for 10–14.10, although
/// on each night only one room is taken; per night it is 2 − 1 = 1. The result is exact and safe: when no
/// night has more bookings than rooms, the rooms can be given out in the order of arrival without
/// conflicts, and a room is assigned no earlier than the arrival day (FR-19).
/// </para>
/// The rooms that can be sold (IRoomInventory) and the bookings per night (Bookings tables) are counted by
/// AvailabilityService in the database; this class only does the arithmetic, so it is unit-tested alone.
/// </summary>
public static class AvailabilityCalculator
{
    /// <summary>
    /// Longest stay that can be searched (and, from T5, booked), in nights. The specification sets no limit,
    /// so this is an assumption to agree with the customer: the average stay is 2 nights (table 12.14), and a
    /// month covers a business trip or a holiday; a longer stay is booked as several bookings or extended
    /// at the hotel (FR-21). The limit also bounds the work of one search (nights × categories × hotels of
    /// a city), which must answer within 3 s for the whole network (NFR-01).
    /// </summary>
    public const int MaxNights = 30;

    /// <summary>
    /// Bookings in these statuses take a room on each of their nights (FR-09, listing 12.1): confirmed and
    /// checked in. A New booking holds no room until it is confirmed (FR-13); Cancelled, NoShow and
    /// Completed bookings hold none any more.
    /// </summary>
    public static readonly IReadOnlyList<BookingStatus> StatusesThatTakeRooms = [BookingStatus.Confirmed, BookingStatus.CheckedIn];

    /// <summary>Free rooms of every night from <paramref name="checkIn"/> to the day before <paramref name="checkOut"/>.</summary>
    /// <param name="sellableRooms">Active rooms of the category that are not out of order (12.8.2).</param>
    /// <param name="bookedRoomsPerNight">
    /// Bookings of the category in <see cref="StatusesThatTakeRooms"/> that include the night, by night. A night
    /// without bookings may be missing; nights outside the stay are ignored.
    /// </param>
    public static IReadOnlyList<NightFreeRooms> FreeRoomsPerNight(
        int sellableRooms, IReadOnlyDictionary<DateOnly, int> bookedRoomsPerNight, DateOnly checkIn, DateOnly checkOut)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(sellableRooms);

        return StayNights.Between(checkIn, checkOut)
            .Select(night => new NightFreeRooms(night, sellableRooms - bookedRoomsPerNight.GetValueOrDefault(night)))
            .ToList();
    }

    /// <summary>
    /// Rooms available for the whole stay: the smallest number of free rooms over its nights. A night may
    /// have more bookings than rooms after a room went out of order (12.8.3, UC-16 A2); the stay then has
    /// 0 rooms to offer, not a negative number.
    /// </summary>
    public static int AvailableRooms(IReadOnlyList<NightFreeRooms> nights)
    {
        if (nights.Count == 0)
        {
            throw new ArgumentException("A stay has at least one night.", nameof(nights));
        }

        return Math.Max(0, nights.Min(n => n.FreeRooms));
    }
}
