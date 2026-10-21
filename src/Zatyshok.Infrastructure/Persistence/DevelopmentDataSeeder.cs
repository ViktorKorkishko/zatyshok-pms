using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Zatyshok.Domain.Modules.Guests;
using Zatyshok.Domain.Modules.Identity;
using Zatyshok.Domain.Modules.Identity.Contracts;
using Zatyshok.Domain.Modules.Rooms;

namespace Zatyshok.Infrastructure.Persistence;

/// <summary>
/// Fills an empty Development database with demo data:
/// <list type="bullet">
/// <item>the «Затишок» network from docs/02 (table 5.1): 5 hotels, 3 network-wide categories,
/// 5 rooms per hotel, a rate for every (hotel, category) pair and two seasonal periods;</item>
/// <item>3 services of the catalog;</item>
/// <item>one user for each of the 9 roles, 3 guests and 1 corporate client.</item>
/// </list>
/// The roles themselves are not seeded here: they come with the migration (HasData in RoleConfiguration).
/// Idempotent: each part does nothing when its first table already has rows (Hotels, Services,
/// Users), so a database seeded in T1 gets only the new parts. Every id is deterministic so that
/// manual Swagger checks and integration tests can reference the same data across database resets.
/// </summary>
public static class DevelopmentDataSeeder
{
    /// <summary>
    /// Placeholder for Users.PasswordHash until password hashing comes in T4. It is not a valid
    /// hash of any password, so no seeded account can sign in yet.
    /// </summary>
    public const string UnusablePasswordHash = "!unusable: password hashing comes in T4";

    // Internal, like the id helpers below, so that the API tests can refer to the seeded rows.
    internal static readonly Guid StandardId = Guid.Parse("11111111-1111-1111-1111-111111111101");
    internal static readonly Guid SuperiorId = Guid.Parse("11111111-1111-1111-1111-111111111102");
    internal static readonly Guid SuiteId = Guid.Parse("11111111-1111-1111-1111-111111111103");

    internal static readonly Guid KyivId = Guid.Parse("22222222-2222-2222-2222-222222222201");
    internal static readonly Guid LvivId = Guid.Parse("22222222-2222-2222-2222-222222222202");
    internal static readonly Guid OdesaId = Guid.Parse("22222222-2222-2222-2222-222222222203");
    internal static readonly Guid DniproId = Guid.Parse("22222222-2222-2222-2222-222222222204");
    internal static readonly Guid KarpatyId = Guid.Parse("22222222-2222-2222-2222-222222222205");

    private static readonly Guid CorporateClientId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa01");

    // Deterministic ids for the seeded rows: the last four digits are <hotel index:2><ordinal:2>.
    // Both parts have a fixed width (D2), so the last group always has 12 digits, even for a
    // hotel with 10 or more rooms (without D2 Guid.Parse failed there).
    internal static Guid RoomId(int hotelIndex, int roomIndex)
        => Guid.Parse($"33333333-3333-3333-3333-33333333{hotelIndex:D2}{roomIndex:D2}");

    internal static Guid RateId(int hotelIndex, int categoryIndex)
        => Guid.Parse($"44444444-4444-4444-4444-44444444{hotelIndex:D2}{categoryIndex:D2}");

    // The same fixed width for the other seeded rows: the last two digits are the ordinal.
    // A user gets the number of its role (01 Guest ... 09 Administrator, as in RoleConfiguration).
    internal static Guid UserId(int number) => Guid.Parse($"77777777-7777-7777-7777-7777777777{number:D2}");

    internal static Guid ServiceId(int number) => Guid.Parse($"88888888-8888-8888-8888-8888888888{number:D2}");

    internal static Guid GuestId(int number) => Guid.Parse($"99999999-9999-9999-9999-9999999999{number:D2}");

    public static async Task SeedAsync(ZatyshokDbContext db, ILogger logger, CancellationToken cancellationToken = default)
    {
        await SeedRoomsAsync(db, logger, cancellationToken);
        await SeedServicesAsync(db, logger, cancellationToken);
        await SeedPeopleAsync(db, logger, cancellationToken);
    }

    /// <summary>Hotels, categories, rooms, rates and seasonal periods (the T1 seed).</summary>
    private static async Task SeedRoomsAsync(ZatyshokDbContext db, ILogger logger, CancellationToken cancellationToken)
    {
        if (await db.Hotels.AnyAsync(cancellationToken))
        {
            logger.LogInformation("Development seed of hotels skipped: hotels already exist");
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
        for (var h = 0; h < hotels.Length; h++)
        {
            var hotel = hotels[h];
            rooms.AddRange(new[]
            {
                new Room { Id = RoomId(h, 0), HotelId = hotel.Id, RoomCategoryId = StandardId, Number = "101", Floor = 1, Capacity = 2, Amenities = "Wi-Fi, кондиціонер, телевізор" },
                new Room { Id = RoomId(h, 1), HotelId = hotel.Id, RoomCategoryId = StandardId, Number = "102", Floor = 1, Capacity = 2, Amenities = "Wi-Fi, кондиціонер, телевізор" },
                new Room { Id = RoomId(h, 2), HotelId = hotel.Id, RoomCategoryId = SuperiorId, Number = "201", Floor = 2, Capacity = 2, Amenities = "Wi-Fi, кондиціонер, телевізор, міні-бар" },
                new Room { Id = RoomId(h, 3), HotelId = hotel.Id, RoomCategoryId = SuperiorId, Number = "202", Floor = 2, Capacity = 3, Amenities = "Wi-Fi, кондиціонер, телевізор, міні-бар" },
                new Room { Id = RoomId(h, 4), HotelId = hotel.Id, RoomCategoryId = SuiteId, Number = "301", Floor = 3, Capacity = 4, Amenities = "Wi-Fi, кондиціонер, телевізор, міні-бар, ванна" },
            });

            var prices = basePrices[hotel.Id];
            rates.AddRange(new[]
            {
                new Rate { Id = RateId(h, 0), HotelId = hotel.Id, RoomCategoryId = StandardId, BasePricePerNight = prices.Standard },
                new Rate { Id = RateId(h, 1), HotelId = hotel.Id, RoomCategoryId = SuperiorId, BasePricePerNight = prices.Superior },
                new Rate { Id = RateId(h, 2), HotelId = hotel.Id, RoomCategoryId = SuiteId, BasePricePerNight = prices.Suite },
            });
        }

        // Two seasonal periods (FR-08): summer by the sea and winter holidays in the mountains.
        // Keep both in the future: search rejects check-in before today (FR-10), so a past season is never priced.
        var odesaStandardRate = rates.Single(r => r.HotelId == OdesaId && r.RoomCategoryId == StandardId);
        var karpatyStandardRate = rates.Single(r => r.HotelId == KarpatyId && r.RoomCategoryId == StandardId);
        var seasonRates = new[]
        {
            new SeasonRate
            {
                Id = Guid.Parse("55555555-5555-5555-5555-555555555501"),
                RateId = odesaStandardRate.Id,
                Name = "Літній сезон 2027",
                StartDate = new DateOnly(2027, 6, 1),
                EndDate = new DateOnly(2027, 8, 31),
                PricePerNight = 2400m,
            },
            new SeasonRate
            {
                Id = Guid.Parse("55555555-5555-5555-5555-555555555502"),
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
            "Development seed of hotels done: {Hotels} hotels, {Categories} categories, {Rooms} rooms, {Rates} rates, {SeasonRates} season rates",
            hotels.Length, categories.Length, rooms.Count, rates.Count, seasonRates.Length);
    }

    /// <summary>Extra services of the network-wide catalog (FR-32).</summary>
    private static async Task SeedServicesAsync(ZatyshokDbContext db, ILogger logger, CancellationToken cancellationToken)
    {
        if (await db.Services.AnyAsync(cancellationToken))
        {
            logger.LogInformation("Development seed of services skipped: services already exist");
            return;
        }

        var services = new[]
        {
            new Service { Id = ServiceId(1), Name = "Сніданок", Description = "Сніданок «шведський стіл» для одного гостя", Price = 350m },
            new Service { Id = ServiceId(2), Name = "Паркування", Description = "Місце на охоронюваній парковці готелю, за добу", Price = 200m },
            new Service { Id = ServiceId(3), Name = "Трансфер", Description = "Трансфер з вокзалу або аеропорту до готелю в один бік", Price = 900m },
        };

        db.Services.AddRange(services);
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Development seed of services done: {Services} services", services.Length);
    }

    /// <summary>
    /// One user for each role, three guests and a corporate client. They are saved together:
    /// the first guest is linked to the guest user. Only Users is checked, so the whole part is
    /// skipped when users exist, even if Guests or CorporateClients are empty.
    /// </summary>
    private static async Task SeedPeopleAsync(ZatyshokDbContext db, ILogger logger, CancellationToken cancellationToken)
    {
        if (await db.Users.AnyAsync(cancellationToken))
        {
            logger.LogInformation("Development seed of people skipped: users already exist");
            return;
        }

        // The roles come from the migration; their ids are looked up by code.
        var roleIds = await db.Roles.ToDictionaryAsync(r => r.Code, r => r.Id, cancellationToken);
        var now = DateTime.UtcNow;

        // A guest registered on the portal who has already confirmed the email (UC-01).
        var guestUser = User.RegisterGuest("ivan.petrenko@example.com", UnusablePasswordHash, roleIds[RoleCodes.Guest], now);
        guestUser.Id = UserId(1);
        guestUser.ConfirmEmail();

        // Creates a staff account with a fixed id.
        User Staff(int number, string email, string fullName, string roleCode, Guid? hotelId)
        {
            var user = User.CreateStaff(email, UnusablePasswordHash, fullName, roleIds[roleCode], hotelId, now);
            user.Id = UserId(number);

            // Demo accounts are not asked to change the password at the first login.
            user.MustChangePassword = false;
            return user;
        }

        // Staff: roles bound to a hotel work in Kyiv, the others see the whole network (FR-02).
        var users = new List<User>
        {
            guestUser,
            Staff(2, "receptionist@zatyshok.ua", "Мельник Оксана Петрівна", RoleCodes.Receptionist, KyivId),
            Staff(3, "reservations@zatyshok.ua", "Шевчук Андрій Іванович", RoleCodes.ReservationAgent, null),
            Staff(4, "housekeeper@zatyshok.ua", "Бондар Галина Степанівна", RoleCodes.Housekeeper, KyivId),
            Staff(5, "head.housekeeper@zatyshok.ua", "Кравчук Наталія Василівна", RoleCodes.HeadHousekeeper, KyivId),
            Staff(6, "manager@zatyshok.ua", "Ткаченко Дмитро Олегович", RoleCodes.HotelManager, KyivId),
            Staff(7, "accountant@zatyshok.ua", "Савченко Ірина Михайлівна", RoleCodes.Accountant, null),
            Staff(8, "executive@zatyshok.ua", "Лисенко Сергій Володимирович", RoleCodes.Executive, null),
            Staff(9, "admin@zatyshok.ua", "Романенко Тарас Юрійович", RoleCodes.Administrator, null),
        };

        var guests = new[]
        {
            // Has an account but no document yet: check-in must ask for it (FR-23, UC-12 A3).
            new Guest
            {
                Id = GuestId(1),
                UserId = guestUser.Id,
                LastName = "Петренко",
                FirstName = "Іван",
                MiddleName = "Олександрович",
                Phone = "+380671234567",
                Email = guestUser.Email,
                PersonalDataConsentAtUtc = now,
                CreatedAtUtc = now,
            },

            // A regular guest created by an operator, with a document and wishes.
            new Guest
            {
                Id = GuestId(2),
                LastName = "Коваленко",
                FirstName = "Олена",
                MiddleName = "Миколаївна",
                BirthDate = new DateOnly(1988, 3, 14),
                Citizenship = "UA",
                DocumentType = DocumentType.Passport,
                DocumentNumber = "МК123456",
                Phone = "+380501112233",
                Email = "olena.kovalenko@example.com",
                Preferences = "Високий поверх, тихий номер",
                StaffNotes = "Постійна гостя, бронює через корпоративного клієнта",
                PersonalDataConsentAtUtc = now,
                CreatedAtUtc = now,
            },

            // A foreign guest: no middle name, a foreign passport.
            new Guest
            {
                Id = GuestId(3),
                LastName = "Smith",
                FirstName = "John",
                BirthDate = new DateOnly(1975, 11, 2),
                Citizenship = "GB",
                DocumentType = DocumentType.ForeignPassport,
                DocumentNumber = "533380006",
                Phone = "+447700900123",
                Email = "john.smith@example.com",
                PersonalDataConsentAtUtc = now,
                CreatedAtUtc = now,
            },
        };

        var corporateClient = new CorporateClient
        {
            Id = CorporateClientId,
            Name = "ТОВ «Приклад Консалт»",
            Edrpou = "12345678",
            ContractNumber = "КД-2026/001",
            ContractStartDate = new DateOnly(2026, 1, 1),
            ContractEndDate = new DateOnly(2027, 12, 31),
            PaymentDeferralDays = 14,
            ContactPerson = "Гончаренко Марія Сергіївна",
            ContactPhone = "+380442223344",
            ContactEmail = "booking@pryklad-consult.example.com",
            CreatedAtUtc = now,
        };

        db.Users.AddRange(users);
        db.Guests.AddRange(guests);
        db.CorporateClients.Add(corporateClient);
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Development seed of people done: {Users} users, {Guests} guests, 1 corporate client",
            users.Count, guests.Length);
    }
}
