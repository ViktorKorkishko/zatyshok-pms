namespace Zatyshok.Domain.Modules.Bookings;

/// <summary>Where the booking came from (FR-11, FR-12).</summary>
public enum BookingSource
{
    Portal,
    Phone,
    Email,
    WalkIn,
    Agency,
}
