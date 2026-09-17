namespace Zatyshok.Domain.Modules.Rooms;

/// <summary>A hotel of the network (docs/07, table 9.4).</summary>
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

    public ICollection<Room> Rooms { get; } = new List<Room>();

    public ICollection<Rate> Rates { get; } = new List<Rate>();
}
