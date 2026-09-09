namespace Zatyshok.Domain.Modules.Rooms;

/// <summary>
/// Seasonal price period of a <see cref="Rate"/> (FR-08). Takes priority over the base
/// price for nights within [StartDate, EndDate]. Periods of one rate must not overlap;
/// this is enforced by the rates service (T3), not by the database.
/// </summary>
public class SeasonRate
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid RateId { get; set; }

    public Rate Rate { get; set; } = null!;

    /// <summary>Display name of the period, e.g. "Літній сезон 2026".</summary>
    public required string Name { get; set; }

    /// <summary>First night the seasonal price applies to (hotel calendar date, inclusive).</summary>
    public DateOnly StartDate { get; set; }

    /// <summary>Last night the seasonal price applies to (hotel calendar date, inclusive).</summary>
    public DateOnly EndDate { get; set; }

    /// <summary>Price per night in UAH within the period.</summary>
    public decimal PricePerNight { get; set; }
}
