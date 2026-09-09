namespace Zatyshok.Domain.Modules.Rooms;

/// <summary>
/// Base price per night for a (hotel, room category) pair (FR-08). Exactly one rate
/// exists per pair; seasonal periods override the base price for their date ranges.
/// </summary>
public class Rate
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid HotelId { get; set; }

    public Hotel Hotel { get; set; } = null!;

    public Guid RoomCategoryId { get; set; }

    public RoomCategory Category { get; set; } = null!;

    /// <summary>Base price per night in UAH.</summary>
    public decimal BasePricePerNight { get; set; }

    public ICollection<SeasonRate> SeasonRates { get; } = new List<SeasonRate>();
}
