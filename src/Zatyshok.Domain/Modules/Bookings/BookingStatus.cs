namespace Zatyshok.Domain.Modules.Bookings;

/// <summary>Booking life cycle (figure 6.1, FR-16, table 12.25). Stored as strings in the database.</summary>
public enum BookingStatus
{
    /// <summary>Created; holds no room yet (an operator booking may wait here for a prepayment, FR-13).</summary>
    New,

    /// <summary>Counts against availability (FR-09).</summary>
    Confirmed,

    CheckedIn,

    Completed,

    Cancelled,

    /// <summary>The guest did not arrive by the end of the check-in day (FR-15).</summary>
    NoShow,
}
