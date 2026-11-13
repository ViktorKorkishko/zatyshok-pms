using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Zatyshok.Domain.Modules.Bookings;
using Zatyshok.Infrastructure.Persistence;

namespace Zatyshok.Tests.Integration;

/// <summary>
/// Two transactions book the last free room of a category at the same time (12.8.2, risk R-01).
/// The test plays the steps of listing 12.1 with a minimal query instead of the T5 services:
/// lock the rate row with SELECT … FOR UPDATE, count free rooms per night, insert the booking.
/// The order of the steps is fixed by the test, so the result does not depend on timing.
/// </summary>
[Trait("Category", "Integration")]
public class RateLockConcurrencyTests(PostgresDatabaseFixture database) : IClassFixture<PostgresDatabaseFixture>
{
    [Fact]
    public async Task OnlyOneOfTwoConcurrentTransactionsBooksTheLastRoom()
    {
        var hotel = await CreateHotelWithOneRoomAsync();
        var checkIn = hotel.Today.AddDays(30);

        await using var first = database.CreateDbContext();
        await using var second = database.CreateDbContext();

        // 1. The first transaction locks the rate row of the pair (hotel, category).
        await using var firstTransaction = await first.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
        await LockRateAsync(first, hotel);

        // 2. The second transaction asks for the same lock in the background and has to wait.
        await using var secondTransaction = await second.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
        var secondPid = await RowLocks.GetBackendPidAsync(second);
        var secondAttempt = Task.Run(async () =>
        {
            await LockRateAsync(second, hotel);
            return await TryBookAsync(second, secondTransaction, hotel, checkIn);
        });
        await RowLocks.WaitUntilWaitingForLockAsync(database, secondPid);

        // 3. The first transaction sees one free room, books it and commits; this releases the lock.
        Assert.True(await TryBookAsync(first, firstTransaction, hotel, checkIn));

        // 4. The second transaction counts only now, under READ COMMITTED it sees the committed
        //    booking, finds no free room and rolls back (the API will answer 409).
        Assert.False(await secondAttempt);
        Assert.Equal(1, await CountBookingsAsync(hotel));
    }

    /// <summary>
    /// Control for the test above: without the rate lock both transactions count before either
    /// inserts, both see one free room and the room is sold twice. This is the race the lock
    /// prevents; if this test ever fails, the scenario above no longer proves anything.
    /// </summary>
    [Fact]
    public async Task WithoutTheRateLockBothTransactionsBookTheLastRoom()
    {
        var hotel = await CreateHotelWithOneRoomAsync();
        var checkIn = hotel.Today.AddDays(30);

        await using var first = database.CreateDbContext();
        await using var second = database.CreateDbContext();
        await using var firstTransaction = await first.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
        await using var secondTransaction = await second.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);

        Assert.Equal(1, await CountFreeRoomsAsync(first, hotel, checkIn, checkIn.AddDays(2)));
        Assert.Equal(1, await CountFreeRoomsAsync(second, hotel, checkIn, checkIn.AddDays(2)));

        // Both insert on the strength of their earlier count.
        await BookAsync(first, firstTransaction, hotel, checkIn);
        await BookAsync(second, secondTransaction, hotel, checkIn);
        Assert.Equal(2, await CountBookingsAsync(hotel));
    }

    private async Task<TestHotel> CreateHotelWithOneRoomAsync()
    {
        await using var db = database.CreateDbContext();
        return await TestData.CreateHotelAsync(db, roomCount: 1);
    }

    /// <summary>Step 1 of listing 12.1 as in listing П.3: Rates has no xmin token, so SELECT * is enough.</summary>
    private static async Task LockRateAsync(ZatyshokDbContext db, TestHotel hotel)
    {
        // No LINQ operators after FromSql: EF would wrap the SQL in a subquery.
        var rates = await db.Rates
            .FromSql($"""SELECT * FROM rooms."Rates" WHERE "HotelId" = {hotel.Hotel.Id} AND "RoomCategoryId" = {hotel.Category.Id} FOR UPDATE""")
            .ToListAsync();
        Assert.Single(rates);
    }

    /// <summary>
    /// Step 2 of listing 12.1: sellable rooms minus the busiest night of [checkIn; checkOut).
    /// The condition on the booking being changed (UC-06) is left out: here nothing is changed.
    /// </summary>
    private static Task<long> CountFreeRoomsAsync(ZatyshokDbContext db, TestHotel hotel, DateOnly checkIn, DateOnly checkOut)
    {
        var hotelId = hotel.Hotel.Id;
        var categoryId = hotel.Category.Id;
        return db.Database.SqlQuery<long>($"""
            WITH inventory AS (
                SELECT count(*) AS rooms_total
                FROM rooms."Rooms"
                WHERE "HotelId" = {hotelId} AND "RoomCategoryId" = {categoryId}
                  AND "IsActive" AND "Status" <> 'OutOfOrder'
            ),
            booked AS (
                SELECT n."Night", count(*) AS rooms_booked
                FROM bookings."Bookings" AS b
                JOIN bookings."BookingNights" AS n ON n."BookingId" = b."Id"
                WHERE b."HotelId" = {hotelId} AND b."RoomCategoryId" = {categoryId}
                  AND b."Status" IN ('Confirmed', 'CheckedIn')
                  AND b."CheckInDate" < {checkOut} AND b."CheckOutDate" > {checkIn}
                  AND n."Night" >= {checkIn} AND n."Night" < {checkOut}
                GROUP BY n."Night"
            )
            SELECT (SELECT rooms_total FROM inventory)
                 - COALESCE((SELECT max(rooms_booked) FROM booked), 0) AS "Value"
            """).SingleAsync();
    }

    /// <summary>Steps 2 and 3 of listing 12.1: book two nights if a room is free, otherwise roll back.</summary>
    private static async Task<bool> TryBookAsync(ZatyshokDbContext db, IDbContextTransaction transaction, TestHotel hotel, DateOnly checkIn)
    {
        if (await CountFreeRoomsAsync(db, hotel, checkIn, checkIn.AddDays(2)) < 1)
        {
            await transaction.RollbackAsync();
            return false;
        }

        await BookAsync(db, transaction, hotel, checkIn);
        return true;
    }

    /// <summary>Step 3 of listing 12.1: insert a confirmed two-night booking (with its nights) and commit.</summary>
    private static async Task BookAsync(ZatyshokDbContext db, IDbContextTransaction transaction, TestHotel hotel, DateOnly checkIn)
    {
        var booking = TestData.NewBooking(hotel, checkIn, nights: 2);
        booking.Confirm(null, DateTime.UtcNow);
        db.Bookings.Add(booking);
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
    }

    private async Task<int> CountBookingsAsync(TestHotel hotel)
    {
        await using var db = database.CreateDbContext();
        return await db.Bookings.CountAsync(b => b.HotelId == hotel.Hotel.Id && b.Status == BookingStatus.Confirmed);
    }
}
