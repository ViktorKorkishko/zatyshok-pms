namespace Zatyshok.Domain.Modules.Rooms;

/// <summary>
/// Network-wide room category (Standard, Superior, Suite...). Bookings are made
/// for a category, not a concrete room; prices are set per hotel via <see cref="Rate"/>.
/// </summary>
public class RoomCategory
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public required string Name { get; set; }

    public string? Description { get; set; }

    /// <summary>Default number of guests the category accommodates (FR-05).</summary>
    public int Capacity { get; set; }

    /// <summary>Categories referenced by rooms or bookings are deactivated instead of deleted.</summary>
    public bool IsActive { get; set; } = true;
}
