using Zatyshok.Domain.Common;

namespace Zatyshok.Domain.Modules.Bookings.Contracts;

/// <summary>A search of available rooms (FR-10). The API has already checked the form of the values.</summary>
/// <param name="HotelId">The hotel; exactly one of <paramref name="HotelId"/> and <paramref name="City"/> is given.</param>
/// <param name="City">The city: every active hotel of it.</param>
/// <param name="RoomCategoryId">Only this category; null for every category that holds the guests.</param>
/// <param name="CheckIn">Calendar date of the hotel (NFR-20).</param>
/// <param name="CheckOut">Later than <paramref name="CheckIn"/>; the check-out day is not a night of the stay.</param>
/// <param name="Guests">At least 1.</param>
public sealed record AvailabilitySearch(
    Guid? HotelId, string? City, Guid? RoomCategoryId, DateOnly CheckIn, DateOnly CheckOut, int Guests);

/// <summary>One night of a stay in a category: its price and how many rooms of the category are free that night.</summary>
public sealed record NightOffer(DateOnly Night, decimal Price, int FreeRooms);

/// <summary>A category of a hotel that holds the guests: rooms available for the whole stay and its cost.</summary>
/// <param name="AvailableRooms">The smallest number of free rooms over the nights (12.8.2); 0 when the stay cannot be booked.</param>
/// <param name="TotalPrice">The sum of the night prices (FR-08).</param>
public sealed record CategoryOffer(
    Guid RoomCategoryId,
    string Name,
    string? Description,
    int Capacity,
    IReadOnlyList<string> Amenities,
    int AvailableRooms,
    IReadOnlyList<NightOffer> Nights,
    decimal TotalPrice);

/// <summary>A hotel of the search with the categories it offers for the stay.</summary>
public sealed record HotelOffers(Guid HotelId, string Name, string City, string Address, IReadOnlyList<CategoryOffer> Categories);

/// <summary>
/// Public contract of the module «Бронювання» (table 9.6): availability of the categories for a stay (FR-09)
/// and its cost (FR-08). FR-09 belongs to this module because availability is counted from the nights of its
/// bookings (table 9.4). Booking creation in T5 runs the same formula under the rate lock (12.8.2).
/// </summary>
public interface IAvailabilityService
{
    /// <summary>
    /// Every chosen hotel with the active categories that hold the guests and have a rate there, cheapest
    /// first. A category without free rooms is listed too, with <see cref="CategoryOffer.AvailableRooms"/> = 0.
    /// </summary>
    /// <exception cref="InvalidRequestException">The check-in is before the hotel's current date, or the category holds fewer guests.</exception>
    /// <exception cref="NotFoundException">The hotel or the category does not exist.</exception>
    /// <exception cref="DomainException">The hotel or the category is deactivated.</exception>
    Task<IReadOnlyList<HotelOffers>> SearchAsync(AvailabilitySearch search, CancellationToken cancellationToken);
}
