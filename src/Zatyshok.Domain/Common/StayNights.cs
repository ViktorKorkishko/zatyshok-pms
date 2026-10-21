namespace Zatyshok.Domain.Common;

/// <summary>
/// Nights of a stay. A night is named by the date it starts (the Night of BookingNights, 12.1.2), so a stay from check-in to
/// check-out has the nights check-in … check-out − 1: the check-out day is not a night and is not paid.
/// Shared by Rooms (night prices) and Bookings (availability per night).
/// </summary>
public static class StayNights
{
    /// <summary>The nights from <paramref name="checkIn"/> to the day before <paramref name="checkOut"/>, in order.</summary>
    public static IReadOnlyList<DateOnly> Between(DateOnly checkIn, DateOnly checkOut)
    {
        // Callers validate the dates first (400 for the client), so this is a programming error.
        if (checkOut <= checkIn)
        {
            throw new ArgumentException("The check-out date must be later than the check-in date.", nameof(checkOut));
        }

        return Enumerable.Range(0, checkOut.DayNumber - checkIn.DayNumber).Select(checkIn.AddDays).ToList();
    }
}
