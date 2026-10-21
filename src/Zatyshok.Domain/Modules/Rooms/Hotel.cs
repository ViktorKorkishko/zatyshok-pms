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
    // The API uses the same defaults when a new hotel comes without these rules.
    public const int DefaultCancellationDeadlineDaysBefore = 1;
    public static readonly TimeOnly DefaultCancellationDeadlineTime = new(18, 0);
    public static readonly TimeOnly DefaultCheckInTime = new(14, 0);
    public static readonly TimeOnly DefaultCheckOutTime = new(12, 0);

    /// <summary>How many days before check-in the free cancellation ends (FR-15).</summary>
    public int CancellationDeadlineDaysBefore { get; set; } = DefaultCancellationDeadlineDaysBefore;

    /// <summary>Hotel-local time of day when the free cancellation ends (FR-15).</summary>
    public TimeOnly CancellationDeadlineTime { get; set; } = DefaultCancellationDeadlineTime;

    /// <summary>Check-in time, used in the arrival reminder (FR-40).</summary>
    public TimeOnly CheckInTime { get; set; } = DefaultCheckInTime;

    public TimeOnly CheckOutTime { get; set; } = DefaultCheckOutTime;

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

    /// <summary>
    /// Whether <paramref name="timeZoneId"/> is a time zone this server knows, so that the dates of
    /// the hotel can be computed (checked before a hotel is saved, FR-05).
    /// </summary>
    public static bool IsKnownTimeZone(string timeZoneId)
    {
        try
        {
            FindTimeZone(timeZoneId);
            return true;
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException or ArgumentException)
        {
            return false;
        }
    }

    private TimeZoneInfo FindTimeZone() => FindTimeZone(TimeZoneId);

    private static TimeZoneInfo FindTimeZone(string timeZoneId)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch (TimeZoneNotFoundException) when (timeZoneId == "Europe/Kyiv")
        {
            // Windows builds with an older ICU know Kyiv only by its former IANA id.
            return TimeZoneInfo.FindSystemTimeZoneById("Europe/Kiev");
        }
    }
}
