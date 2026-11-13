using Zatyshok.Domain.Modules.Bookings;

namespace Zatyshok.Tests.Modules.Bookings;

/// <summary>
/// The availability formula of FR-09 as revised in 12.8.2: free rooms are counted for every night of the
/// stay, and the stay has as many rooms as its worst night.
/// </summary>
public class AvailabilityCalculatorTests
{
    private static readonly DateOnly October10 = new(2026, 10, 10);
    private static readonly DateOnly October14 = new(2026, 10, 14);

    [Fact]
    public void ExampleOf12_8_2GivesOneRoomWhereTheOverlapFormulaGaveZero()
    {
        // 2 rooms; booking A 10–12.10 takes the nights 10 and 11, booking B 12–14.10 the nights 12 and 13.
        // The literal formula of report 1 subtracts both bookings, because both periods overlap the request,
        // and gives 2 − 2 = 0; the application has no code of that formula to test, only the per-night one.
        var bookings = new[] { (CheckIn: October10, CheckOut: new DateOnly(2026, 10, 12)), (CheckIn: new DateOnly(2026, 10, 12), CheckOut: October14) };
        var bookedPerNight = bookings
            .SelectMany(b => Enumerable.Range(0, b.CheckOut.DayNumber - b.CheckIn.DayNumber).Select(b.CheckIn.AddDays))
            .GroupBy(night => night)
            .ToDictionary(g => g.Key, g => g.Count());

        var nights = AvailabilityCalculator.FreeRoomsPerNight(2, bookedPerNight, October10, October14);

        // Per night only one room is taken, so one room is free for the whole stay.
        Assert.Equal([1, 1, 1, 1], nights.Select(n => n.FreeRooms));
        Assert.Equal(1, AvailabilityCalculator.AvailableRooms(nights));
    }

    [Fact]
    public void WithoutBookingsEveryRoomIsAvailable()
    {
        var nights = AvailabilityCalculator.FreeRoomsPerNight(5, new Dictionary<DateOnly, int>(), October10, October14);

        Assert.Equal(4, nights.Count);
        Assert.Equal(5, AvailabilityCalculator.AvailableRooms(nights));
    }

    [Fact]
    public void TheWorstNightDecides()
    {
        // 3 rooms: one night in the middle is fully booked.
        var booked = new Dictionary<DateOnly, int> { [October10] = 1, [October10.AddDays(1)] = 3, [October10.AddDays(2)] = 2 };

        var nights = AvailabilityCalculator.FreeRoomsPerNight(3, booked, October10, October14);

        Assert.Equal([2, 0, 1, 3], nights.Select(n => n.FreeRooms));
        Assert.Equal(0, AvailabilityCalculator.AvailableRooms(nights));
    }

    [Fact]
    public void NightsOutsideTheStayDoNotCount()
    {
        // The day of check-out is not a night of the stay, nor is the night before check-in.
        var booked = new Dictionary<DateOnly, int> { [October10.AddDays(-1)] = 2, [October14] = 2, [October10] = 1 };

        var nights = AvailabilityCalculator.FreeRoomsPerNight(2, booked, October10, October14);

        Assert.Equal([October10, October10.AddDays(1), October10.AddDays(2), October10.AddDays(3)], nights.Select(n => n.Night));
        Assert.Equal([1, 2, 2, 2], nights.Select(n => n.FreeRooms));
        Assert.Equal(1, AvailabilityCalculator.AvailableRooms(nights));
    }

    [Fact]
    public void OverbookedNightShowsTheDeficitButTheStayHasZeroRooms()
    {
        // 2 bookings on a night, but one of the 2 rooms went out of order: 1 − 2 = −1 (12.8.3, UC-16 A2).
        var booked = new Dictionary<DateOnly, int> { [October10] = 2 };

        var nights = AvailabilityCalculator.FreeRoomsPerNight(1, booked, October10, October10.AddDays(2));

        Assert.Equal([-1, 1], nights.Select(n => n.FreeRooms));
        Assert.Equal(0, AvailabilityCalculator.AvailableRooms(nights));
    }

    [Fact]
    public void CategoryWithoutRoomsHasNothingAvailable()
    {
        var nights = AvailabilityCalculator.FreeRoomsPerNight(0, new Dictionary<DateOnly, int>(), October10, October10.AddDays(1));

        Assert.Equal(0, AvailabilityCalculator.AvailableRooms(nights));
    }

    [Fact]
    public void OnlyConfirmedAndCheckedInBookingsTakeRooms()
    {
        Assert.Equal([BookingStatus.Confirmed, BookingStatus.CheckedIn], AvailabilityCalculator.StatusesThatTakeRooms);
    }

    [Fact]
    public void InvalidArgumentsAreProgrammingErrors()
    {
        var none = new Dictionary<DateOnly, int>();

        Assert.Throws<ArgumentException>(() => AvailabilityCalculator.FreeRoomsPerNight(1, none, October14, October10));
        Assert.Throws<ArgumentOutOfRangeException>(() => AvailabilityCalculator.FreeRoomsPerNight(-1, none, October10, October14));
        Assert.Throws<ArgumentException>(() => AvailabilityCalculator.AvailableRooms([]));
    }
}
