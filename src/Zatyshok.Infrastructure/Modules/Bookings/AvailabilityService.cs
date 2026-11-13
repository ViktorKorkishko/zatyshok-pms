using Microsoft.EntityFrameworkCore;
using Zatyshok.Domain.Common;
using Zatyshok.Domain.Modules.Bookings;
using Zatyshok.Domain.Modules.Bookings.Contracts;
using Zatyshok.Domain.Modules.Rooms.Contracts;
using Zatyshok.Infrastructure.Persistence;

namespace Zatyshok.Infrastructure.Modules.Bookings;

/// <summary>
/// Availability and cost of a stay for the categories of the chosen hotels (FR-08 – FR-10). Hotels, rooms
/// and prices come from the contracts of «Номерний фонд і тарифи» (table 9.6), the bookings are this
/// module's own tables, and the formula itself is <see cref="AvailabilityCalculator"/> (12.8.2).
/// <para>
/// The search takes no lock: it shows the state at the moment of the request. Creating a booking (T5)
/// counts again under the rate lock, in one transaction with the insert (listing 12.1). The search is not
/// limited by the hotel scope: availability and prices are what the portal shows to anyone (FR-10).
/// </para>
/// </summary>
internal sealed class AvailabilityService(ZatyshokDbContext db, IRoomCatalog catalog, IRoomInventory inventory, TimeProvider time)
    : IAvailabilityService
{
    private static readonly IReadOnlyDictionary<DateOnly, int> NoBookings = new Dictionary<DateOnly, int>();

    public async Task<IReadOnlyList<HotelOffers>> SearchAsync(AvailabilitySearch search, CancellationToken cancellationToken)
    {
        var hotels = await FindHotelsAsync(search, cancellationToken);
        foreach (var hotel in hotels)
        {
            EnsureCheckInIsNotInThePast(hotel, search.CheckIn);
        }

        if (hotels.Count == 0)
        {
            EnsureCheckInIsNotInThePastAnywhere(search.CheckIn);
        }

        var categories = await FindCategoriesAsync(search, cancellationToken);
        if (hotels.Count == 0 || categories.Count == 0)
        {
            return hotels.Select(h => ToOffers(h, [])).ToList();
        }

        // Prices, rooms and bookings come in one query each for the whole search, not one per hotel,
        // category or night: the search over a whole city must answer within 3 s (NFR-01).
        var hotelIds = hotels.Select(h => h.Id).ToList();
        var categoryIds = categories.Select(c => c.Id).ToList();
        var prices = (await catalog.GetNightPricesAsync(hotelIds, categoryIds, search.CheckIn, search.CheckOut, cancellationToken))
            .ToDictionary(p => (p.HotelId, p.RoomCategoryId));
        var rooms = (await inventory.GetSellableRoomsAsync(hotelIds, categoryIds, cancellationToken))
            .ToDictionary(r => (r.HotelId, r.RoomCategoryId));
        var booked = await CountBookedRoomsAsync(hotelIds, categoryIds, search.CheckIn, search.CheckOut, cancellationToken);

        return hotels
            .Select(hotel => ToOffers(hotel, categories
                // A pair without a rate has no price for its nights, so it cannot be booked (FR-08).
                .Where(category => prices.ContainsKey((hotel.Id, category.Id)))
                .Select(category => ToOffer(
                    search,
                    category,
                    prices[(hotel.Id, category.Id)],
                    rooms.GetValueOrDefault((hotel.Id, category.Id)),
                    booked.GetValueOrDefault((hotel.Id, category.Id)) ?? NoBookings))
                .OrderBy(offer => offer.TotalPrice)
                .ThenBy(offer => offer.Name, StringComparer.Ordinal)
                .ToList()))
            .ToList();
    }

    private async Task<IReadOnlyList<HotelInfo>> FindHotelsAsync(AvailabilitySearch search, CancellationToken cancellationToken)
    {
        if (search.HotelId is { } hotelId)
        {
            var hotel = await catalog.FindHotelAsync(hotelId, cancellationToken) ?? throw new NotFoundException("Готель не знайдено.");
            if (!hotel.IsActive)
            {
                throw new DomainException($"Готель «{hotel.Name}» деактивовано: він не приймає бронювань.");
            }

            return [hotel];
        }

        // A city without active hotels is not an error: the search just finds nothing (UC-04 A2).
        var city = search.City ?? throw new ArgumentException("A search needs a hotel or a city.", nameof(search));
        return await catalog.FindActiveHotelsInCityAsync(city, cancellationToken);
    }

    // FR-10: the check-in date is not earlier than today, and "today" is the calendar date of the hotel (NFR-20).
    private static void EnsureCheckInIsNotInThePast(HotelInfo hotel, DateOnly checkIn)
    {
        if (checkIn < hotel.Today)
        {
            throw new InvalidRequestException(
                nameof(AvailabilitySearch.CheckIn),
                $"Дата заїзду не може бути ранішою за поточну дату готелю «{hotel.Name}» ({hotel.Today:dd.MM.yyyy}).");
        }
    }

    /// <summary>
    /// The same rule (FR-10, UC-04 step 2) for a city without active hotels, which has no hotel date to compare
    /// with: a date earlier than today at UTC−12, the last time zone to start a day, is in the past for every
    /// hotel anywhere, while a later date may still be today for some hotel and is not rejected.
    /// </summary>
    private void EnsureCheckInIsNotInThePastAnywhere(DateOnly checkIn)
    {
        var earliestToday = DateOnly.FromDateTime(time.GetUtcNow().UtcDateTime.AddHours(-12));
        if (checkIn < earliestToday)
        {
            throw new InvalidRequestException(nameof(AvailabilitySearch.CheckIn), $"Дата заїзду ({checkIn:dd.MM.yyyy}) уже минула.");
        }
    }

    /// <summary>
    /// The asked category, or every active category that holds the guests (its base capacity, FR-05). Every room
    /// of a category holds at least its base capacity (RoomService), so the guests fit any room given out at check-in.
    /// </summary>
    private async Task<IReadOnlyList<RoomCategoryInfo>> FindCategoriesAsync(AvailabilitySearch search, CancellationToken cancellationToken)
    {
        var all = await catalog.GetCategoriesAsync(cancellationToken);
        if (search.RoomCategoryId is { } categoryId)
        {
            var category = all.SingleOrDefault(c => c.Id == categoryId) ?? throw new NotFoundException("Категорію номерів не знайдено.");
            if (!category.IsActive)
            {
                throw new DomainException($"Категорію «{category.Name}» деактивовано: її номери не продаються.");
            }

            if (category.Capacity < search.Guests)
            {
                throw new InvalidRequestException(
                    nameof(AvailabilitySearch.Guests),
                    $"Кількість гостей перевищує місткість категорії «{category.Name}» ({category.Capacity}).");
            }

            return [category];
        }

        var active = all.Where(c => c.IsActive).ToList();
        var fitting = active.Where(c => c.Capacity >= search.Guests).ToList();
        if (active.Count > 0 && fitting.Count == 0)
        {
            throw new InvalidRequestException(
                nameof(AvailabilitySearch.Guests),
                $"Кількість гостей перевищує місткість кожної категорії номерів (найбільша — {active.Max(c => c.Capacity)}).");
        }

        return fitting;
    }

    /// <summary>
    /// The «booked» part of listing 12.1 for every pair at once: how many bookings of the pair in the
    /// statuses that take a room (confirmed, checked in) include each night of [checkIn, checkOut).
    /// The nights are counted from BookingNights, one row per night of a booking.
    /// </summary>
    private async Task<Dictionary<(Guid HotelId, Guid RoomCategoryId), IReadOnlyDictionary<DateOnly, int>>> CountBookedRoomsAsync(
        IReadOnlyCollection<Guid> hotelIds,
        IReadOnlyCollection<Guid> categoryIds,
        DateOnly checkIn,
        DateOnly checkOut,
        CancellationToken cancellationToken)
    {
        var hotels = hotelIds.ToArray();
        var categories = categoryIds.ToArray();
        var statuses = AvailabilityCalculator.StatusesThatTakeRooms.ToArray();

        var rows = await db.BookingNights.AsNoTracking()
            .Where(n => hotels.Contains(n.Booking.HotelId)
                        && categories.Contains(n.Booking.RoomCategoryId)
                        && statuses.Contains(n.Booking.Status)
                        // The period condition repeats the night condition below, but it lets PostgreSQL
                        // pick the bookings by the index IX_Bookings_Availability, as in listing 12.1.
                        && n.Booking.CheckInDate < checkOut && n.Booking.CheckOutDate > checkIn
                        && n.Night >= checkIn && n.Night < checkOut)
            .GroupBy(n => new { n.Booking.HotelId, n.Booking.RoomCategoryId, n.Night })
            .Select(g => new { g.Key.HotelId, g.Key.RoomCategoryId, g.Key.Night, Bookings = g.Count() })
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(r => (r.HotelId, r.RoomCategoryId))
            .ToDictionary(g => g.Key, g => (IReadOnlyDictionary<DateOnly, int>)g.ToDictionary(r => r.Night, r => r.Bookings));
    }

    private static CategoryOffer ToOffer(
        AvailabilitySearch search,
        RoomCategoryInfo category,
        StayPrices prices,
        SellableRooms? rooms,
        IReadOnlyDictionary<DateOnly, int> bookedRoomsPerNight)
    {
        var freeRooms = AvailabilityCalculator.FreeRoomsPerNight(rooms?.Count ?? 0, bookedRoomsPerNight, search.CheckIn, search.CheckOut);

        // Both lists have the nights check-in … check-out − 1 in order (StayNights), so they pair up one to one.
        // An overbooked night (negative, after a room went out of order) is shown as 0: the search is public,
        // and the deficit is a warning for the manager (UC-16 A2) from reporting.v_category_availability_daily.
        var nights = prices.Nights
            .Zip(freeRooms, (price, free) => new NightOffer(price.Night, price.Price, Math.Max(0, free.FreeRooms)))
            .ToList();

        return new CategoryOffer(
            category.Id,
            category.Name,
            category.Description,
            category.Capacity,
            rooms?.Amenities ?? [],
            AvailabilityCalculator.AvailableRooms(freeRooms),
            nights,
            prices.TotalPrice);
    }

    private static HotelOffers ToOffers(HotelInfo hotel, IReadOnlyList<CategoryOffer> categories)
        => new(hotel.Id, hotel.Name, hotel.City, hotel.Address, categories);
}
