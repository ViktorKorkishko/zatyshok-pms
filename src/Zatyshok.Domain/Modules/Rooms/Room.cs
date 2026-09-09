namespace Zatyshok.Domain.Modules.Rooms;

/// <summary>A physical room of a hotel. The number is unique within the hotel (FR-06).</summary>
public class Room
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid HotelId { get; set; }

    public Hotel Hotel { get; set; } = null!;

    public Guid RoomCategoryId { get; set; }

    public RoomCategory Category { get; set; } = null!;

    /// <summary>Room number as printed on the door, e.g. "305". Unique per hotel.</summary>
    public required string Number { get; set; }

    public int Floor { get; set; }

    /// <summary>Actual capacity of this room; may differ from the category default.</summary>
    public int Capacity { get; set; }

    /// <summary>Comma-separated amenity list; a dedicated dictionary is out of scope for the first version.</summary>
    public string? Amenities { get; set; }

    public string? Notes { get; set; }

    public RoomStatus Status { get; set; } = RoomStatus.Free;

    /// <summary>Rooms with stay history are deactivated instead of deleted (UC-08).</summary>
    public bool IsActive { get; set; } = true;
}
