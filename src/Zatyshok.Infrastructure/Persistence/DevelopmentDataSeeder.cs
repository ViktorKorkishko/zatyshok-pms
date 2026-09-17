using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Zatyshok.Domain.Modules.Rooms;

namespace Zatyshok.Infrastructure.Persistence;

/// <summary>
/// Fills an empty Development database with the «Затишок» network from docs/02
/// (table 5.1): 5 hotels, 3 network-wide categories, 5 rooms per hotel, a rate for
/// every (hotel, category) pair and two seasonal periods. Idempotent: does nothing
/// when hotels already exist. Ids are fixed so that manual Swagger checks and
/// integration tests can reference the same data across database resets.
/// </summary>
public static class DevelopmentDataSeeder
{
    private static readonly Guid StandardId = Guid.Parse("11111111-1111-1111-1111-111111111101");
    private static readonly Guid SuperiorId = Guid.Parse("11111111-1111-1111-1111-111111111102");
    private static readonly Guid SuiteId = Guid.Parse("11111111-1111-1111-1111-111111111103");

    private static readonly Guid KyivId = Guid.Parse("22222222-2222-2222-2222-222222222201");
    private static readonly Guid LvivId = Guid.Parse("22222222-2222-2222-2222-222222222202");
    private static readonly Guid OdesaId = Guid.Parse("22222222-2222-2222-2222-222222222203");
    private static readonly Guid DniproId = Guid.Parse("22222222-2222-2222-2222-222222222204");
    private static readonly Guid KarpatyId = Guid.Parse("22222222-2222-2222-2222-222222222205");

    public static async Task SeedAsync(ZatyshokDbContext db, ILogger logger, CancellationToken cancellationToken = default)
    {
        if (await db.Hotels.AnyAsync(cancellationToken))
        {
            logger.LogInformation("Development seed skipped: hotels already exist");
            return;
        }

        var categories = new[]
        {
            new RoomCategory { Id = StandardId, Name = "Standard", Capacity = 2, Description = "Стандартний номер з одним двоспальним або двома односпальними ліжками" },
            new RoomCategory { Id = SuperiorId, Name = "Superior", Capacity = 2, Description = "Покращений номер збільшеної площі з зоною відпочинку" },
            new RoomCategory { Id = SuiteId, Name = "Suite", Capacity = 4, Description = "Люкс із вітальнею та спальнею" },
        };

        // Hotels of the network per docs/02, table 5.1.
        var hotels = new[]
        {
            new Hotel { Id = KyivId, Name = "Затишок Київ", City = "Київ", Address = "вул. Хрещатик, 1", Stars = 4, TimeZoneId = "Europe/Kyiv", Phone = "+380441234567", Email = "kyiv@zatyshok.ua" },
            new Hotel { Id = LvivId, Name = "Затишок Львів", City = "Львів", Address = "пл. Ринок, 10", Stars = 4, TimeZoneId = "Europe/Kyiv", Phone = "+380321234567", Email = "lviv@zatyshok.ua" },
            new Hotel { Id = OdesaId, Name = "Затишок Одеса", City = "Одеса", Address = "вул. Дерибасівська, 5", Stars = 3, TimeZoneId = "Europe/Kyiv", Phone = "+380481234567", Email = "odesa@zatyshok.ua" },
            new Hotel { Id = DniproId, Name = "Затишок Дніпро", City = "Дніпро", Address = "просп. Яворницького, 20", Stars = 3, TimeZoneId = "Europe/Kyiv", Phone = "+380561234567", Email = "dnipro@zatyshok.ua" },
            new Hotel { Id = KarpatyId, Name = "Затишок Карпати", City = "Яремче", Address = "вул. Свободи, 3", Stars = 3, TimeZoneId = "Europe/Kyiv", Phone = "+380341234567", Email = "karpaty@zatyshok.ua" },
        };

        // Base prices per night in UAH per (hotel, category).
        var basePrices = new Dictionary<Guid, (decimal Standard, decimal Superior, decimal Suite)>
        {
            [KyivId] = (2500m, 3500m, 6000m),
            [LvivId] = (2200m, 3200m, 5500m),
            [OdesaId] = (1800m, 2600m, 4500m),
            [DniproId] = (1600m, 2300m, 4000m),
            [KarpatyId] = (1500m, 2200m, 3800m),
        };

        var rooms = new List<Room>();
        var rates = new List<Rate>();
        foreach (var hotel in hotels)
        {
            rooms.AddRange(new[]
            {
                new Room { HotelId = hotel.Id, RoomCategoryId = StandardId, Number = "101", Floor = 1, Capacity = 2, Amenities = "Wi-Fi, кондиціонер, телевізор" },
                new Room { HotelId = hotel.Id, RoomCategoryId = StandardId, Number = "102", Floor = 1, Capacity = 2, Amenities = "Wi-Fi, кондиціонер, телевізор" },
                new Room { HotelId = hotel.Id, RoomCategoryId = SuperiorId, Number = "201", Floor = 2, Capacity = 2, Amenities = "Wi-Fi, кондиціонер, телевізор, міні-бар" },
                new Room { HotelId = hotel.Id, RoomCategoryId = SuperiorId, Number = "202", Floor = 2, Capacity = 3, Amenities = "Wi-Fi, кондиціонер, телевізор, міні-бар" },
                new Room { HotelId = hotel.Id, RoomCategoryId = SuiteId, Number = "301", Floor = 3, Capacity = 4, Amenities = "Wi-Fi, кондиціонер, телевізор, міні-бар, ванна" },
            });

            var prices = basePrices[hotel.Id];
            rates.AddRange(new[]
            {
                new Rate { HotelId = hotel.Id, RoomCategoryId = StandardId, BasePricePerNight = prices.Standard },
                new Rate { HotelId = hotel.Id, RoomCategoryId = SuperiorId, BasePricePerNight = prices.Superior },
                new Rate { HotelId = hotel.Id, RoomCategoryId = SuiteId, BasePricePerNight = prices.Suite },
            });
        }

        // Two seasonal periods (FR-08): summer by the sea and winter holidays in the mountains.
        var odesaStandardRate = rates.Single(r => r.HotelId == OdesaId && r.RoomCategoryId == StandardId);
        var karpatyStandardRate = rates.Single(r => r.HotelId == KarpatyId && r.RoomCategoryId == StandardId);
        var seasonRates = new[]
        {
            new SeasonRate
            {
                RateId = odesaStandardRate.Id,
                Name = "Літній сезон 2026",
                StartDate = new DateOnly(2026, 6, 1),
                EndDate = new DateOnly(2026, 8, 31),
                PricePerNight = 2400m,
            },
            new SeasonRate
            {
                RateId = karpatyStandardRate.Id,
                Name = "Зимові свята 2026–2027",
                StartDate = new DateOnly(2026, 12, 20),
                EndDate = new DateOnly(2027, 1, 10),
                PricePerNight = 2100m,
            },
        };

        db.RoomCategories.AddRange(categories);
        db.Hotels.AddRange(hotels);
        db.Rooms.AddRange(rooms);
        db.Rates.AddRange(rates);
        db.SeasonRates.AddRange(seasonRates);
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Development seed done: {Hotels} hotels, {Categories} categories, {Rooms} rooms, {Rates} rates, {SeasonRates} season rates",
            hotels.Length, categories.Length, rooms.Count, rates.Count, seasonRates.Length);
    }
}
