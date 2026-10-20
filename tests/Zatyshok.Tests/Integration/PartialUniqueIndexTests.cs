using Npgsql;
using Zatyshok.Domain.Modules.Billing;
using Zatyshok.Domain.Modules.Housekeeping;

namespace Zatyshok.Tests.Integration;

/// <summary>
/// Partial unique indexes (table 12.32): uniqueness holds only for the rows of the WHERE filter.
/// The domain cannot see other rows, so these rules are the database's job; a second row is
/// rejected with SQLSTATE 23505 (unique_violation), which the API will turn into HTTP 409.
/// </summary>
[Trait("Category", "Integration")]
public class PartialUniqueIndexTests(PostgresDatabaseFixture database) : IClassFixture<PostgresDatabaseFixture>
{
    [Fact]
    public async Task SecondActiveBookingWithTheSameRoomIsRejected()
    {
        await using var db = database.CreateDbContext();
        var hotel = await TestData.CreateHotelAsync(db);
        var room = hotel.Rooms[0];

        db.Bookings.Add(TestData.ConfirmedWithRoom(hotel, room));
        await db.SaveChangesAsync();

        // Each booking alone is valid for the domain; only the database sees both.
        db.Bookings.Add(TestData.ConfirmedWithRoom(hotel, room));
        var error = await PostgresAssert.SaveFailsAsync(db, PostgresErrorCodes.UniqueViolation);

        Assert.Equal("UX_Bookings_RoomId_Active", error.ConstraintName);
    }

    [Fact]
    public async Task RoomOfACompletedBookingCanBeAssignedAgain()
    {
        await using var db = database.CreateDbContext();
        var hotel = await TestData.CreateHotelAsync(db);
        var room = hotel.Rooms[0];

        // A completed booking keeps its RoomId for the history but is outside the index filter.
        var completed = TestData.ConfirmedWithRoom(hotel, room);
        completed.CheckIn(room.Id, room.RoomCategoryId, hotel.Today, null, DateTime.UtcNow);
        completed.CheckOut(null, DateTime.UtcNow);
        db.Bookings.Add(completed);
        await db.SaveChangesAsync();

        db.Bookings.Add(TestData.ConfirmedWithRoom(hotel, room));
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task SecondInvoiceOfABookingIsRejectedUntilTheFirstIsVoided()
    {
        await using var db = database.CreateDbContext();
        var hotel = await TestData.CreateHotelAsync(db);
        var accountant = await TestData.CreateStaffAsync(db);
        var booking = TestData.NewBooking(hotel, hotel.Today.AddDays(10), nights: 2);
        db.Bookings.Add(booking);

        var first = new Invoice(booking.Id, null, TestData.Nights(booking.CheckInDate, 2), DateTime.UtcNow);
        db.Invoices.Add(first);
        await db.SaveChangesAsync();

        db.Invoices.Add(new Invoice(booking.Id, null, TestData.Nights(booking.CheckInDate, 2), DateTime.UtcNow));
        var error = await PostgresAssert.SaveFailsAsync(db, PostgresErrorCodes.UniqueViolation);
        Assert.Equal("UX_Invoices_BookingId_NotVoided", error.ConstraintName);

        // A fresh context: the failed invoice stays tracked as Added in the old one.
        await using var retry = database.CreateDbContext();
        retry.Attach(first);
        first.Void("Помилкові реквізити", accountant.Id, DateTime.UtcNow);
        retry.Invoices.Add(new Invoice(booking.Id, null, TestData.Nights(booking.CheckInDate, 2), DateTime.UtcNow));
        await retry.SaveChangesAsync();
    }

    [Fact]
    public async Task SecondActiveAfterDepartureTaskForARoomIsRejected()
    {
        await using var db = database.CreateDbContext();
        var hotel = await TestData.CreateHotelAsync(db);
        var room = hotel.Rooms[0];
        var booking = TestData.NewBooking(hotel, hotel.Today);
        db.Bookings.Add(booking);

        var first = new HousekeepingTask(room.Id, HousekeepingTaskType.AfterDeparture, DateTime.UtcNow, bookingId: booking.Id);
        db.HousekeepingTasks.Add(first);

        // A routine cleaning of the same room is outside the filter.
        db.HousekeepingTasks.Add(new HousekeepingTask(room.Id, HousekeepingTaskType.Routine, DateTime.UtcNow));
        await db.SaveChangesAsync();

        db.HousekeepingTasks.Add(new HousekeepingTask(room.Id, HousekeepingTaskType.AfterDeparture, DateTime.UtcNow, bookingId: booking.Id));
        var error = await PostgresAssert.SaveFailsAsync(db, PostgresErrorCodes.UniqueViolation);
        Assert.Equal("UX_HousekeepingTasks_RoomId_ActiveAfterDeparture", error.ConstraintName);

        await using var retry = database.CreateDbContext();
        retry.Attach(first);
        first.Cancel(DateTime.UtcNow);
        retry.HousekeepingTasks.Add(new HousekeepingTask(room.Id, HousekeepingTaskType.AfterDeparture, DateTime.UtcNow, bookingId: booking.Id));
        await retry.SaveChangesAsync();
    }

    [Fact]
    public async Task SecondOpenMaintenanceIssueForARoomIsRejected()
    {
        await using var db = database.CreateDbContext();
        var hotel = await TestData.CreateHotelAsync(db);
        var room = hotel.Rooms[0];
        var reporter = await TestData.CreateStaffAsync(db);

        var first = new MaintenanceIssue(room.Id, "Протікає кран", reporter.Id, DateTime.UtcNow);
        db.MaintenanceIssues.Add(first);
        await db.SaveChangesAsync();

        db.MaintenanceIssues.Add(new MaintenanceIssue(room.Id, "Не працює кондиціонер", reporter.Id, DateTime.UtcNow));
        var error = await PostgresAssert.SaveFailsAsync(db, PostgresErrorCodes.UniqueViolation);
        Assert.Equal("UX_MaintenanceIssues_RoomId_Open", error.ConstraintName);

        await using var retry = database.CreateDbContext();
        retry.Attach(first);
        first.Resolve(reporter.Id, "Замінено змішувач", DateTime.UtcNow.AddMinutes(1));
        retry.MaintenanceIssues.Add(new MaintenanceIssue(room.Id, "Не працює кондиціонер", reporter.Id, DateTime.UtcNow.AddMinutes(2)));
        await retry.SaveChangesAsync();
    }
}
