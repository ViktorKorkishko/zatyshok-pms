using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Zatyshok.Api.Modules.Bookings;
using Zatyshok.Domain.Modules.Bookings;
using Zatyshok.Domain.Modules.Rooms;

namespace Zatyshok.Tests.Integration.Api;

/// <summary>
/// GET /api/v1/availability (FR-08 – FR-10): the seed hotels with their seasons, bookings that overlap the
/// period in part (created through the domain model: the bookings service comes in T5), rooms out of order,
/// validation and errors. Tests that change data use their own hotel, so the seed stays as it is.
/// </summary>
[Trait("Category", "Integration")]
public class AvailabilityApiTests(ApiFixture api) : IClassFixture<ApiFixture>
{
    private const string Url = "/api/v1/availability";

    /// <summary>A fixed "now" for the tests on the seed: 1 November 2026, noon in Kyiv, before both seasons of the seed.</summary>
    private static readonly DateTimeOffset November1 = new(2026, 11, 1, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task SeedHotelOffersEveryCategoryWithItsRoomsAndPrices()
    {
        var response = await Search(ClientAt(November1), $"hotelId={Seed.Kyiv}&checkIn=2026-11-10&checkOut=2026-11-13&guests=2");

        Assert.Equal((new DateOnly(2026, 11, 10), new DateOnly(2026, 11, 13), 3, 2), (response.CheckIn, response.CheckOut, response.NightCount, response.Guests));
        var hotel = Assert.Single(response.Hotels);
        Assert.Equal((Seed.Kyiv, "Затишок Київ", "Київ"), (hotel.HotelId, hotel.Name, hotel.City));

        // 5 rooms in «Затишок Київ»: 2 Standard, 2 Superior, 1 Suite; no bookings; the cheapest first.
        Assert.Equal(["Standard", "Superior", "Suite"], hotel.Categories.Select(c => c.Name));
        Assert.Equal([2, 2, 1], hotel.Categories.Select(c => c.AvailableRooms));
        Assert.Equal([7500m, 10500m, 18000m], hotel.Categories.Select(c => c.TotalPrice));

        var standard = hotel.Categories[0];
        Assert.Equal(Seed.Standard, standard.RoomCategoryId);
        Assert.Equal(2, standard.Capacity);
        Assert.Equal(["Wi-Fi", "кондиціонер", "телевізор"], standard.Amenities);
        Assert.Equal([new DateOnly(2026, 11, 10), new DateOnly(2026, 11, 11), new DateOnly(2026, 11, 12)], standard.Nights.Select(n => n.Night));
        Assert.All(standard.Nights, n => Assert.Equal((2500m, 2), (n.Price, n.FreeRooms)));
    }

    [Fact]
    public async Task StayAcrossTheStartOfTheWinterSeasonPaysBothPrices()
    {
        // «Затишок Карпати» (Яремче), Standard: 1500 UAH, «Зимові свята 2026–2027» 20.12–10.01 — 2100 UAH.
        var response = await Search(ClientAt(November1), "city=Яремче&checkIn=2026-12-18&checkOut=2026-12-22&guests=2");

        var standard = Assert.Single(response.Hotels).Categories.Single(c => c.Name == "Standard");
        Assert.Equal([1500m, 1500m, 2100m, 2100m], standard.Nights.Select(n => n.Price));
        Assert.Equal(7200m, standard.TotalPrice);

        // The season is set for Standard only; Superior keeps its base price.
        var superior = response.Hotels[0].Categories.Single(c => c.Name == "Superior");
        Assert.Equal(4 * 2200m, superior.TotalPrice);
    }

    [Fact]
    public async Task LastNightOfTheSeasonIsStillSeasonal()
    {
        var response = await Search(
            ClientAt(November1), $"hotelId={Seed.Karpaty}&roomCategoryId={Seed.Standard}&checkIn=2027-01-09&checkOut=2027-01-12&guests=1");

        var standard = Assert.Single(Assert.Single(response.Hotels).Categories);
        Assert.Equal([2100m, 2100m, 1500m], standard.Nights.Select(n => n.Price));
        Assert.Equal(5700m, standard.TotalPrice);
    }

    [Fact]
    public async Task CitySearchIgnoresCaseAndOffersOnlyCategoriesForTheGuests()
    {
        // Only Suite holds 3 guests (capacity 4); Standard and Superior hold 2.
        var response = await Search(ClientAt(November1), "city=%20одеса%20&checkIn=2026-11-20&checkOut=2026-11-22&guests=3");

        var hotel = Assert.Single(response.Hotels);
        Assert.Equal(Seed.Odesa, hotel.HotelId);
        var suite = Assert.Single(hotel.Categories);
        Assert.Equal(("Suite", 1, 9000m), (suite.Name, suite.AvailableRooms, suite.TotalPrice));
    }

    [Fact]
    public async Task CityWithoutHotelsFindsNothing()
    {
        var response = await Search(api.Client, "city=Ужгород&checkIn=2030-01-10&checkOut=2030-01-12&guests=2");

        Assert.Empty(response.Hotels);
    }

    [Fact]
    public async Task ExampleOf12_8_2GivesOneRoom()
    {
        // 2 rooms; confirmed bookings 10–12 and 12–14 (days from today); a search 10–14 gets one room
        // for the whole stay, while the overlap formula of report 1 would give 2 − 2 = 0.
        var hotel = await CreateHotelAsync(roomCount: 2);
        var day10 = hotel.Today.AddDays(10);
        await SaveBookingsAsync(Confirmed(hotel, day10, nights: 2), Confirmed(hotel, day10.AddDays(2), nights: 2));

        var category = await SearchOneCategoryAsync(hotel, day10, day10.AddDays(4));

        Assert.Equal([1, 1, 1, 1], category.Nights.Select(n => n.FreeRooms));
        Assert.Equal(1, category.AvailableRooms);
        Assert.Equal(4 * TestData.Price, category.TotalPrice);
    }

    [Fact]
    public async Task BookingsOverlappingThePeriodInPartTakeOnlyTheirOwnNights()
    {
        var hotel = await CreateHotelAsync(roomCount: 2);
        var start = hotel.Today.AddDays(5);
        var end = start.AddDays(4);

        // Confirmed bookings: one from before the period takes its first night, one running past its end
        // takes its last night, one takes the second night. New and Cancelled bookings for the whole period
        // take no room (FR-09, FR-13).
        var cancelled = Confirmed(hotel, start, nights: 4);
        cancelled.Cancel("Гість передумав", byGuest: false, authorUserId: null, DateTime.UtcNow);
        await SaveBookingsAsync(
            Confirmed(hotel, start.AddDays(-2), nights: 3),
            Confirmed(hotel, start.AddDays(3), nights: 3),
            Confirmed(hotel, start.AddDays(1), nights: 1),
            TestData.NewBooking(hotel, start, nights: 4),
            cancelled);

        var category = await SearchOneCategoryAsync(hotel, start, end);

        Assert.Equal([1, 1, 2, 1], category.Nights.Select(n => n.FreeRooms));
        Assert.Equal(1, category.AvailableRooms);

        // Two more bookings on the third night leave it without rooms, and the worst night decides.
        await SaveBookingsAsync(Confirmed(hotel, start.AddDays(2), nights: 1), Confirmed(hotel, start.AddDays(2), nights: 1));

        category = await SearchOneCategoryAsync(hotel, start, end);
        Assert.Equal([1, 1, 0, 1], category.Nights.Select(n => n.FreeRooms));
        Assert.Equal(0, category.AvailableRooms);
    }

    [Fact]
    public async Task CheckedInBookingCountsButCompletedAndNoShowDoNot()
    {
        var hotel = await CreateHotelAsync(roomCount: 2);
        var today = hotel.Today;
        var now = DateTime.UtcNow;

        await using (var db = api.CreateDbContext())
        {
            var rooms = await db.Rooms.Where(r => r.HotelId == hotel.Hotel.Id).OrderBy(r => r.Number).ToListAsync();

            // Checked in today for 2 nights: takes a room (FR-09).
            var checkedIn = Confirmed(hotel, today, nights: 2);
            checkedIn.CheckIn(rooms[0].Id, rooms[0].RoomCategoryId, today, null, now);
            rooms[0].Occupy(reservedForThisBooking: false);

            // Checked out early on the first day; its nights stay, but a completed booking holds no room,
            // and the room waiting for cleaning is still sold (it is not out of order).
            var completed = Confirmed(hotel, today, nights: 2);
            completed.CheckIn(rooms[1].Id, rooms[1].RoomCategoryId, today, null, now);
            rooms[1].Occupy(reservedForThisBooking: false);
            completed.CheckOut(null, now);
            rooms[1].MarkDirty();

            // The guest did not arrive yesterday.
            var noShow = Confirmed(hotel, today.AddDays(-1), nights: 3);
            noShow.MarkNoShow(today, now);

            db.Bookings.AddRange(checkedIn, completed, noShow);
            await db.SaveChangesAsync();
        }

        var category = await SearchOneCategoryAsync(hotel, today, today.AddDays(3));

        Assert.Equal([1, 1, 2], category.Nights.Select(n => n.FreeRooms));
        Assert.Equal(1, category.AvailableRooms);
    }

    [Fact]
    public async Task RoomsOutOfOrderOrDeactivatedAreNotSold()
    {
        var hotel = await CreateHotelAsync(roomCount: 3);
        var start = hotel.Today.AddDays(3);
        await SaveBookingsAsync(Confirmed(hotel, start, nights: 1), Confirmed(hotel, start, nights: 1));

        Assert.Equal(1, (await SearchOneCategoryAsync(hotel, start, start.AddDays(2))).AvailableRooms);

        // The manual state change of FR-07 takes a room out of the formula at once.
        await (await api.Client.PostAsync($"/api/v1/rooms/{hotel.Rooms[0].Id}/out-of-order", null)).AssertStatusAsync(HttpStatusCode.OK);
        await (await api.Client.PostAsync($"/api/v1/rooms/{hotel.Rooms[1].Id}/deactivate", null)).AssertStatusAsync(HttpStatusCode.OK);

        // 1 room left for 2 bookings on the first night: a deficit of one room (12.8.3, UC-16 A2).
        var category = await SearchOneCategoryAsync(hotel, start, start.AddDays(2));
        Assert.Equal([-1, 1], category.Nights.Select(n => n.FreeRooms));
        Assert.Equal(0, category.AvailableRooms);

        // Back in service the room waits for cleaning (Dirty), but it is sold again.
        await (await api.Client.PostAsync($"/api/v1/rooms/{hotel.Rooms[0].Id}/return-to-service", null)).AssertStatusAsync(HttpStatusCode.OK);
        category = await SearchOneCategoryAsync(hotel, start, start.AddDays(2));
        Assert.Equal([0, 2], category.Nights.Select(n => n.FreeRooms));
    }

    [Fact]
    public async Task CategoryWithoutRateIsNotOfferedAndAmenitiesAreThoseOfEveryRoom()
    {
        var hotel = await CreateHotelAsync(roomCount: 2);
        await using (var db = api.CreateDbContext())
        {
            // A second category with a room but no rate: it has no price, so it cannot be booked (FR-08).
            var withoutRate = TestData.NewCategory("Без тарифу");
            db.RoomCategories.Add(withoutRate);
            db.Rooms.Add(TestData.NewRoom(hotel.Hotel, withoutRate, "901"));

            var rooms = await db.Rooms.Where(r => r.HotelId == hotel.Hotel.Id).OrderBy(r => r.Number).ToListAsync();
            rooms[0].Amenities = "Wi-Fi, сейф, кондиціонер";
            rooms[1].Amenities = "кондиціонер, wi-fi";
            await db.SaveChangesAsync();
        }

        var response = await Search(api.Client, $"hotelId={hotel.Hotel.Id}&checkIn={hotel.Today.AddDays(1):yyyy-MM-dd}&checkOut={hotel.Today.AddDays(2):yyyy-MM-dd}&guests=1");

        var category = Assert.Single(Assert.Single(response.Hotels).Categories);
        Assert.Equal(hotel.Category.Id, category.RoomCategoryId);
        Assert.Equal(["Wi-Fi", "кондиціонер"], category.Amenities);
    }

    [Theory]
    [InlineData("checkIn=2030-01-10&checkOut=2030-01-12&guests=2", "hotelId", "Вкажіть готель або місто.")]
    [InlineData("hotelId=22222222-2222-2222-2222-222222222201&city=Київ&checkIn=2030-01-10&checkOut=2030-01-12&guests=2", "hotelId", "Вкажіть або готель, або місто, а не обидва.")]
    [InlineData("city=Київ&checkIn=2030-01-10&checkOut=2030-01-10&guests=2", "checkOut", "Дата виїзду має бути пізнішою за дату заїзду.")]
    [InlineData("city=Київ&checkIn=2030-01-10&checkOut=2030-02-10&guests=2", "checkOut", "Період проживання — не більше 30 ночей.")]
    [InlineData("city=Київ&checkIn=2030-01-10&checkOut=2030-01-12&guests=0", "guests", "Кількість гостей має бути не меншою за 1.")]
    [InlineData("city=Київ&checkOut=2030-01-12&guests=1", "checkIn", "Вкажіть дату заїзду.")]
    [InlineData("city=Київ&checkIn=10.01.2030x&checkOut=2030-01-12&guests=1", "checkIn", "Значення «10.01.2030x» не підходить для цього поля.")]
    public async Task InvalidQueryGivesValidationProblem(string query, string field, string message)
    {
        var problem = await Problem.ReadAsync(await api.Client.GetAsync($"{Url}?{query}"), HttpStatusCode.BadRequest);

        Assert.Equal(message, Assert.Single(problem.Errors![field]));
    }

    [Fact]
    public async Task CheckInBeforeTheHotelsTodayGives400()
    {
        // 22:30 UTC on 31 October is already 00:30 on 1 November in Kyiv (UTC+2): the hotel's date decides (NFR-20).
        var client = ClientAt(new DateTimeOffset(2026, 10, 31, 22, 30, 0, TimeSpan.Zero));

        var yesterday = await client.GetAsync($"{Url}?hotelId={Seed.Kyiv}&checkIn=2026-10-31&checkOut=2026-11-02&guests=2");
        var today = await client.GetAsync($"{Url}?hotelId={Seed.Kyiv}&checkIn=2026-11-01&checkOut=2026-11-02&guests=2");

        var problem = await Problem.ReadAsync(yesterday, HttpStatusCode.BadRequest);
        Assert.Equal(
            "Дата заїзду не може бути ранішою за поточну дату готелю «Затишок Київ» (01.11.2026).",
            Assert.Single(problem.Errors!["checkIn"]));
        await today.AssertStatusAsync(HttpStatusCode.OK);
    }

    [Fact]
    public async Task MoreGuestsThanTheCategoryHoldsGives400()
    {
        var client = ClientAt(November1);

        var standard = await client.GetAsync($"{Url}?hotelId={Seed.Kyiv}&roomCategoryId={Seed.Standard}&checkIn=2026-11-10&checkOut=2026-11-11&guests=3");
        var anyCategory = await client.GetAsync($"{Url}?hotelId={Seed.Kyiv}&checkIn=2026-11-10&checkOut=2026-11-11&guests=5");

        Assert.Equal(
            "Кількість гостей перевищує місткість категорії «Standard» (2).",
            Assert.Single((await Problem.ReadAsync(standard, HttpStatusCode.BadRequest)).Errors!["guests"]));
        Assert.Equal(
            "Кількість гостей перевищує місткість кожної категорії номерів (найбільша — 4).",
            Assert.Single((await Problem.ReadAsync(anyCategory, HttpStatusCode.BadRequest)).Errors!["guests"]));
    }

    [Fact]
    public async Task UnknownHotelOrCategoryGives404()
    {
        var hotel = await api.Client.GetAsync($"{Url}?hotelId={Guid.NewGuid()}&checkIn=2030-01-10&checkOut=2030-01-12&guests=1");
        var category = await api.Client.GetAsync($"{Url}?hotelId={Seed.Kyiv}&roomCategoryId={Guid.NewGuid()}&checkIn=2030-01-10&checkOut=2030-01-12&guests=1");

        Assert.Equal("Готель не знайдено.", (await Problem.ReadAsync(hotel, HttpStatusCode.NotFound)).Detail);
        Assert.Equal("Категорію номерів не знайдено.", (await Problem.ReadAsync(category, HttpStatusCode.NotFound)).Detail);
    }

    [Fact]
    public async Task DeactivatedHotelOrCategoryGives409()
    {
        var hotel = await CreateHotelAsync(roomCount: 1);
        var category = TestData.NewCategory("Закрита");
        await using (var db = api.CreateDbContext())
        {
            category.IsActive = false;
            db.RoomCategories.Add(category);
            var saved = await db.Hotels.SingleAsync(h => h.Id == hotel.Hotel.Id);
            saved.IsActive = false;
            await db.SaveChangesAsync();
        }

        var dates = "checkIn=2030-01-10&checkOut=2030-01-12&guests=1";
        var inactiveHotel = await api.Client.GetAsync($"{Url}?hotelId={hotel.Hotel.Id}&{dates}");
        var inactiveCategory = await api.Client.GetAsync($"{Url}?hotelId={Seed.Kyiv}&roomCategoryId={category.Id}&{dates}");

        Assert.Equal(
            $"Готель «{hotel.Hotel.Name}» деактивовано: він не приймає бронювань.",
            (await Problem.ReadAsync(inactiveHotel, HttpStatusCode.Conflict)).Detail);
        Assert.Equal(
            $"Категорію «{category.Name}» деактивовано: її номери не продаються.",
            (await Problem.ReadAsync(inactiveCategory, HttpStatusCode.Conflict)).Detail);
    }

    private static async Task<AvailabilityResponse> Search(HttpClient client, string query)
        => await (await client.GetAsync($"{Url}?{query}")).ReadAsync<AvailabilityResponse>();

    /// <summary>The only category of a <see cref="TestData"/> hotel for the stay.</summary>
    private async Task<CategoryAvailabilityResponse> SearchOneCategoryAsync(TestHotel hotel, DateOnly checkIn, DateOnly checkOut)
    {
        var response = await Search(api.Client, $"hotelId={hotel.Hotel.Id}&checkIn={checkIn:yyyy-MM-dd}&checkOut={checkOut:yyyy-MM-dd}&guests=1");
        var category = Assert.Single(Assert.Single(response.Hotels).Categories);
        Assert.Equal(hotel.Category.Id, category.RoomCategoryId);
        return category;
    }

    /// <summary>A client of a host whose clock shows <paramref name="now"/>, so that "today" of the hotels is fixed.</summary>
    private HttpClient ClientAt(DateTimeOffset now)
        => api.CreateClient(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(new FixedClock(now));
        });

    private async Task<TestHotel> CreateHotelAsync(int roomCount)
    {
        await using var db = api.CreateDbContext();
        return await TestData.CreateHotelAsync(db, roomCount);
    }

    /// <summary>A Confirmed booking without a room: confirmed bookings take a room on each night (FR-09).</summary>
    private static Booking Confirmed(TestHotel hotel, DateOnly checkIn, int nights)
    {
        var booking = TestData.NewBooking(hotel, checkIn, nights);
        booking.Confirm(null, DateTime.UtcNow);
        return booking;
    }

    private async Task SaveBookingsAsync(params Booking[] bookings)
    {
        await using var db = api.CreateDbContext();
        db.Bookings.AddRange(bookings);
        await db.SaveChangesAsync();
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
