using Zatyshok.Domain.Common;

namespace Zatyshok.Domain.Modules.Rooms.Contracts;

/// <summary>A hotel of the network as other modules see it.</summary>
/// <param name="IsActive">A deactivated hotel takes no new bookings (FR-05).</param>
/// <param name="Today">Current calendar date of the hotel (NFR-20); a check-in may not be earlier (FR-10).</param>
public sealed record HotelInfo(Guid Id, string Name, string City, string Address, bool IsActive, DateOnly Today);

/// <summary>A room category of the network as other modules see it.</summary>
/// <param name="Capacity">Base number of guests of the category (FR-05); a booking may not have more guests.</param>
/// <param name="IsActive">Rooms of a deactivated category are not sold.</param>
public sealed record RoomCategoryInfo(Guid Id, string Name, string? Description, int Capacity, bool IsActive);

/// <summary>Prices of the nights of a stay for a pair «hotel – category» (FR-08).</summary>
public sealed record StayPrices(Guid HotelId, Guid RoomCategoryId, IReadOnlyList<NightPrice> Nights)
{
    /// <summary>Cost of the stay: the sum of the prices of its nights (FR-08).</summary>
    public decimal TotalPrice => Nights.Sum(n => n.Price);
}

/// <summary>
/// Public contract of the module «Номерний фонд і тарифи» (table 9.6): hotels, categories and night prices
/// for other modules (9.4.1, rule 1). T3 has the part that the availability search needs; the services of
/// the catalog come here with the invoice lines in T6.
/// </summary>
public interface IRoomCatalog
{
    /// <summary>The hotel, active or not, or null when there is no such hotel.</summary>
    Task<HotelInfo?> FindHotelAsync(Guid hotelId, CancellationToken cancellationToken);

    /// <summary>Active hotels of the city, by name; the city is compared ignoring case and the spaces around it.</summary>
    Task<IReadOnlyList<HotelInfo>> FindActiveHotelsInCityAsync(string city, CancellationToken cancellationToken);

    /// <summary>All room categories of the network, active or not, by name.</summary>
    Task<IReadOnlyList<RoomCategoryInfo>> GetCategoriesAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Prices of the nights from <paramref name="checkIn"/> to the day before <paramref name="checkOut"/> for
    /// every pair of <paramref name="hotelIds"/> × <paramref name="roomCategoryIds"/> that has a rate: the
    /// seasonal price on the nights of a seasonal period, the base price on the others (FR-08). A pair
    /// without a rate is not in the result: it cannot be booked.
    /// </summary>
    Task<IReadOnlyList<StayPrices>> GetNightPricesAsync(
        IReadOnlyCollection<Guid> hotelIds,
        IReadOnlyCollection<Guid> roomCategoryIds,
        DateOnly checkIn,
        DateOnly checkOut,
        CancellationToken cancellationToken);
}
