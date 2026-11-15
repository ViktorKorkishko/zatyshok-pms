using Zatyshok.Domain.Common;

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

    /// <summary>
    /// Price of every night from <paramref name="checkIn"/> to the day before <paramref name="checkOut"/>.
    /// The cost of a stay is the sum of these prices (FR-08), and a booking keeps them as its nights.
    /// Reads <see cref="SeasonRates"/>, so the caller loads at least the periods that overlap the stay.
    /// </summary>
    public IReadOnlyList<NightPrice> GetNightPrices(DateOnly checkIn, DateOnly checkOut)
        => StayNights.Between(checkIn, checkOut).Select(night => new NightPrice(night, GetPrice(night))).ToList();

    /// <summary>
    /// Price of one night: the price of the seasonal period that includes the night, otherwise the base
    /// price (FR-08: seasonal periods take priority over the base price). Both bounds of a period are
    /// nights of it (table 12.1), so the period 20.12–10.01 prices the night of 10 January, not the 11th.
    /// </summary>
    public decimal GetPrice(DateOnly night)
    {
        // Periods of one rate do not overlap: SeasonRateService rejects an overlapping period under the
        // rate lock, so at most one period matches. Rows written past that service could still overlap;
        // then the period that starts later wins (then the shorter one), so the price never depends on
        // the order in which the rows were read.
        var season = SeasonRates
            .Where(s => s.Includes(night))
            .OrderByDescending(s => s.StartDate)
            .ThenBy(s => s.EndDate)
            .ThenBy(s => s.Id)
            .FirstOrDefault();

        return season?.PricePerNight ?? BasePricePerNight;
    }
}
