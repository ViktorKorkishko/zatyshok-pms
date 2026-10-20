using Microsoft.EntityFrameworkCore;
using Npgsql;
using Zatyshok.Domain.Common;
using Zatyshok.Domain.Modules.Bookings;
using Zatyshok.Domain.Modules.Guests;
using Zatyshok.Domain.Modules.Identity;
using Zatyshok.Domain.Modules.Rooms;
using Zatyshok.Infrastructure.Persistence;

namespace Zatyshok.Tests.Integration;

/// <summary>A saved hotel in Europe/Kyiv with one category, its rate, rooms and a guest.</summary>
internal sealed record TestHotel(Hotel Hotel, RoomCategory Category, Rate Rate, IReadOnlyList<Room> Rooms, Guest Guest)
{
    /// <summary>
    /// Current calendar date of the hotel (NFR-20), read from the clock on every call. A test reads it
    /// once into a variable: two reads may fall on both sides of midnight in Kyiv.
    /// </summary>
    public DateOnly Today => Hotel.GetLocalDate(DateTime.UtcNow);
}

/// <summary>
/// Builds rows for integration tests through the domain model, the way the application will.
/// Every call creates new rows with unique names, because tests of a class share one database.
/// </summary>
internal static class TestData
{
    public const decimal Price = 2500m;

    /// <summary>Saves a hotel with one category of <paramref name="roomCount"/> rooms, its rate and a guest.</summary>
    public static async Task<TestHotel> CreateHotelAsync(ZatyshokDbContext db, int roomCount = 1)
    {
        var hotel = NewHotel();
        var category = NewCategory();
        var rate = new Rate { HotelId = hotel.Id, RoomCategoryId = category.Id, BasePricePerNight = Price };
        var rooms = Enumerable.Range(1, roomCount).Select(n => NewRoom(hotel, category, $"{100 + n}")).ToList();
        var guest = new Guest { LastName = "Петренко", FirstName = "Іван" };

        db.Hotels.Add(hotel);
        db.RoomCategories.Add(category);
        db.Rates.Add(rate);
        db.Rooms.AddRange(rooms);
        db.Guests.Add(guest);
        await db.SaveChangesAsync();

        return new TestHotel(hotel, category, rate, rooms, guest);
    }

    public static Hotel NewHotel() => new()
    {
        Name = $"Затишок Тест {Unique()}",
        City = "Київ",
        Address = "вул. Тестова, 1",
        Stars = 3,
        TimeZoneId = "Europe/Kyiv",
    };

    /// <summary>Category names are unique in the network (IX_RoomCategories_Name).</summary>
    public static RoomCategory NewCategory(string name = "Standard") => new() { Name = $"{name} {Unique()}", Capacity = 2 };

    public static Room NewRoom(Hotel hotel, RoomCategory category, string number) => new()
    {
        HotelId = hotel.Id,
        RoomCategoryId = category.Id,
        Number = number,
        Floor = 1,
        Capacity = 2,
    };

    /// <summary>A New booking of the hotel's category for <paramref name="nights"/> nights from <paramref name="checkIn"/>.</summary>
    public static Booking NewBooking(TestHotel hotel, DateOnly checkIn, int nights = 1)
        => NewBooking(hotel.Hotel, hotel.Category, hotel.Guest, checkIn, nights);

    public static Booking NewBooking(Hotel hotel, RoomCategory category, Guest guest, DateOnly checkIn, int nights = 1)
    {
        var checkOut = checkIn.AddDays(nights);
        return new Booking(
            hotel.Id,
            category.Id,
            guest.Id,
            corporateClientId: null,
            checkIn,
            checkOut,
            guestsCount: 1,
            BookingSource.Phone,
            wishes: null,
            Nights(checkIn, nights),
            hotel.GetFreeCancellationUntilUtc(checkIn),
            authorUserId: null,
            DateTime.UtcNow);
    }

    /// <summary>A Confirmed booking with the room pre-assigned; <paramref name="hotel"/>'s today is the check-in date (FR-19).</summary>
    public static Booking ConfirmedWithRoom(TestHotel hotel, Room room)
    {
        var today = hotel.Today;
        var booking = NewBooking(hotel, today);
        booking.Confirm(null, DateTime.UtcNow);
        booking.AssignRoom(room.Id, room.RoomCategoryId, today, null, DateTime.UtcNow);
        return booking;
    }

    /// <summary>Saves a staff account of a network-wide role (the roles come with the migration).</summary>
    public static async Task<User> CreateStaffAsync(ZatyshokDbContext db, string roleCode = RoleCodes.Accountant)
    {
        var roleId = await db.Roles.Where(r => r.Code == roleCode).Select(r => r.Id).SingleAsync();
        var user = User.CreateStaff($"staff.{Unique()}@example.com", "!test", "Тестова Особа", roleId, hotelId: null, DateTime.UtcNow);
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    public static List<NightPrice> Nights(DateOnly checkIn, int count, decimal price = Price)
        => Enumerable.Range(0, count).Select(i => new NightPrice(checkIn.AddDays(i), price)).ToList();

    public static string Unique() => Guid.NewGuid().ToString("N")[..8];
}

/// <summary>Helpers for tests in which a second transaction waits for a row lock (12.8.2).</summary>
internal static class RowLocks
{
    public static Task<int> GetBackendPidAsync(ZatyshokDbContext db)
        => db.Database.SqlQuery<int>($"SELECT pg_backend_pid() AS \"Value\"").SingleAsync();

    /// <summary>Waits until the session <paramref name="pid"/> is blocked on a lock (pg_stat_activity).</summary>
    public static async Task WaitUntilWaitingForLockAsync(PostgresDatabaseFixture database, int pid)
    {
        await using var monitor = await database.OpenConnectionAsync();
        await using var command = new NpgsqlCommand("SELECT wait_event_type FROM pg_stat_activity WHERE pid = @pid", monitor);
        command.Parameters.AddWithValue("pid", pid);

        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (DateTime.UtcNow < deadline)
        {
            if (await command.ExecuteScalarAsync() is "Lock")
            {
                return;
            }

            await Task.Delay(20);
        }

        Assert.Fail("The second transaction did not start waiting for the row lock.");
    }
}

/// <summary>Checks for errors that PostgreSQL itself raises (constraint, trigger, privilege).</summary>
internal static class PostgresAssert
{
    /// <summary>
    /// EF Core wraps database errors of SaveChanges in <see cref="DbUpdateException"/>; the
    /// <see cref="PostgresException"/> inside has the SQLSTATE and the constraint name.
    /// </summary>
    public static async Task<PostgresException> SaveFailsAsync(DbContext db, string sqlState)
    {
        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        var postgres = Assert.IsType<PostgresException>(exception.InnerException);
        Assert.Equal(sqlState, postgres.SqlState);
        return postgres;
    }

    /// <summary>Runs SQL that must fail with <paramref name="sqlState"/>.</summary>
    public static async Task<PostgresException> SqlFailsAsync(NpgsqlConnection connection, string sql, string sqlState)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        var exception = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());
        Assert.Equal(sqlState, exception.SqlState);
        return exception;
    }
}
