namespace Zatyshok.Domain.Modules.Rooms;

/// <summary>A hotel of the network (table 12.6).</summary>
public class Hotel
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public required string Name { get; set; }

    public required string City { get; set; }

    public required string Address { get; set; }

    /// <summary>Star rating of the hotel (3 or 4 in the current network).</summary>
    public int Stars { get; set; }

    /// <summary>IANA time zone id, e.g. "Europe/Kyiv". Check-in/check-out dates are calendar dates of this zone.</summary>
    public required string TimeZoneId { get; set; }

    public string? Phone { get; set; }

    public string? Email { get; set; }

    /// <summary>Hotels with bookings are deactivated instead of deleted (FR-05).</summary>
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    // Hotel rules are settings, not code (risk R-08). Defaults: 1 day and 18:00 come from
    // FR-15; 14:00 and 12:00 are an assumption to agree with the hotel directors (12.3.2).

    /// <summary>How many days before check-in the free cancellation ends (FR-15).</summary>
    public int CancellationDeadlineDaysBefore { get; set; } = 1;

    /// <summary>Hotel-local time of day when the free cancellation ends (FR-15).</summary>
    public TimeOnly CancellationDeadlineTime { get; set; } = new(18, 0);

    /// <summary>Check-in time, used in the arrival reminder (FR-40).</summary>
    public TimeOnly CheckInTime { get; set; } = new(14, 0);

    public TimeOnly CheckOutTime { get; set; } = new(12, 0);

    public ICollection<Room> Rooms { get; } = new List<Room>();

    public ICollection<Rate> Rates { get; } = new List<Rate>();

    /// <summary>Current calendar date of the hotel (NFR-20): "today" for check-in, no-show and pre-assignment rules.</summary>
    public DateOnly GetLocalDate(DateTime utcNow)
        => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(utcNow, FindTimeZone()));

    /// <summary>
    /// End of free cancellation for a booking (FR-15, 12.3.3): check-in date minus
    /// <see cref="CancellationDeadlineDaysBefore"/> days at <see cref="CancellationDeadlineTime"/>
    /// hotel time, converted to UTC. For the defaults it is 18:00 of the day before check-in.
    /// </summary>
    public DateTime GetFreeCancellationUntilUtc(DateOnly checkInDate)
    {
        var zone = FindTimeZone();
        var local = checkInDate.AddDays(-CancellationDeadlineDaysBefore).ToDateTime(CancellationDeadlineTime);

        // A time skipped by the spring clock change (e.g. 03:30) does not exist; take the next hour.
        if (zone.IsInvalidTime(local))
        {
            local = local.AddHours(1);
        }

        return TimeZoneInfo.ConvertTimeToUtc(local, zone);
    }

    private TimeZoneInfo FindTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(TimeZoneId);
        }
        catch (TimeZoneNotFoundException) when (TimeZoneId == "Europe/Kyiv")
        {
            // Windows builds with an older ICU know Kyiv only by its former IANA id.
            return TimeZoneInfo.FindSystemTimeZoneById("Europe/Kiev");
        }
    }
}
