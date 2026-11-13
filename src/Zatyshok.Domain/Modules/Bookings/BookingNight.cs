namespace Zatyshok.Domain.Modules.Bookings;

/// <summary>
/// One night of a booking with its price at booking time (FR-08, table 12.13). The key is
/// (BookingId, Night). Rows are created and changed only by <see cref="Booking"/>.
/// </summary>
public class BookingNight
{
    // For EF Core.
    private BookingNight()
    {
    }

    internal BookingNight(Guid bookingId, DateOnly night, decimal price)
    {
        BookingId = bookingId;
        Night = night;
        Price = price;
    }

    public Guid BookingId { get; private set; }

    public Booking Booking { get; private set; } = null!;

    /// <summary>The date the night starts.</summary>
    public DateOnly Night { get; private set; }

    /// <summary>Price in UAH; a snapshot that later tariff changes do not touch (UC-11).</summary>
    public decimal Price { get; internal set; }
}
