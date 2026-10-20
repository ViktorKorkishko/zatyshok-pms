using Npgsql;
using Zatyshok.Domain.Modules.Billing;
using Zatyshok.Domain.Modules.Rooms;

namespace Zatyshok.Tests.Integration;

/// <summary>
/// The views of the "reporting" schema (12.8.3, listings 12.3 and 12.4), created by the migration
/// ReportingViewsAndAuditGuard as SQL because the EF model does not describe them.
/// </summary>
[Trait("Category", "Integration")]
public class ReportingViewTests(PostgresDatabaseFixture database) : IClassFixture<PostgresDatabaseFixture>
{
    [Theory]
    [InlineData("reporting.v_category_availability_daily")]
    [InlineData("reporting.v_hotel_occupancy_daily")]
    public async Task ViewCanBeQueried(string view)
    {
        await using var connection = await database.OpenConnectionAsync();
        await using var command = new NpgsqlCommand($"SELECT * FROM {view} LIMIT 10", connection);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
        }
    }

    [Fact]
    public async Task AvailabilityStartsAtTheHotelsDateAndKeepsACategoryWithAllRoomsOutOfOrder()
    {
        await using var db = database.CreateDbContext();

        // Standard: two working rooms. Suite: its only room is out of order, but the category is
        // still sold (it has a rate) and has a confirmed booking for tonight.
        var hotel = await TestData.CreateHotelAsync(db, roomCount: 2);
        var suite = TestData.NewCategory("Suite");
        var suiteRoom = TestData.NewRoom(hotel.Hotel, suite, "301");
        suiteRoom.MarkOutOfOrder();
        db.RoomCategories.Add(suite);
        db.Rates.Add(new Rate { HotelId = hotel.Hotel.Id, RoomCategoryId = suite.Id, BasePricePerNight = 6000m });
        db.Rooms.Add(suiteRoom);

        var kyivToday = hotel.Today;
        var suiteBooking = TestData.NewBooking(hotel.Hotel, suite, hotel.Guest, kyivToday);
        suiteBooking.Confirm(null, DateTime.UtcNow);
        db.Bookings.Add(suiteBooking);
        await db.SaveChangesAsync();

        var rows = await ReadAvailabilityAsync(hotel.Hotel.Id);

        // 366 nights per category, starting at the current date in Europe/Kyiv (NFR-20). The
        // date is read again after the query in case the test ran across local midnight.
        var firstNight = rows.Min(r => r.Night);
        Assert.Contains(firstNight, new[] { kyivToday, hotel.Today });
        Assert.Equal(366, rows.Count(r => r.CategoryId == hotel.Category.Id));
        Assert.Equal(366, rows.Count(r => r.CategoryId == suite.Id));

        var standardTonight = rows.Single(r => r.CategoryId == hotel.Category.Id && r.Night == firstNight);
        Assert.Equal((2L, 0L, 2L), (standardTonight.RoomsTotal, standardTonight.RoomsBooked, standardTonight.RoomsFree));

        // No sellable rooms and one booking: a shortage the manager must see (UC-16 A2).
        var suiteTonight = rows.Single(r => r.CategoryId == suite.Id && r.Night == firstNight);
        Assert.Equal((0L, 1L, -1L), (suiteTonight.RoomsTotal, suiteTonight.RoomsBooked, suiteTonight.RoomsFree));
    }

    [Fact]
    public async Task OccupancyCountsCheckedInRoomsAndInvoiceRevenue()
    {
        await using var db = database.CreateDbContext();
        var hotel = await TestData.CreateHotelAsync(db, roomCount: 2);
        var room = hotel.Rooms[0];

        var booking = TestData.NewBooking(hotel, hotel.Today, nights: 2);
        booking.Confirm(null, DateTime.UtcNow);
        booking.CheckIn(room.Id, room.RoomCategoryId, hotel.Today, null, DateTime.UtcNow);
        room.Occupy();
        db.Bookings.Add(booking);

        var invoice = new Invoice(booking.Id, null, TestData.Nights(booking.CheckInDate, 2), DateTime.UtcNow);
        var service = new Service { Name = $"Сніданок {TestData.Unique()}", Price = 350m };
        db.Services.Add(service);
        invoice.AddItem(service.Id, service.Name, booking.CheckInDate, quantity: 2, service.Price, null, DateTime.UtcNow);
        db.Invoices.Add(invoice);
        await db.SaveChangesAsync();

        await using var connection = await database.OpenConnectionAsync();
        await using var command = new NpgsqlCommand(
            """
            SELECT rooms_in_inventory, rooms_occupied, occupancy_pct, room_revenue, service_revenue
            FROM reporting.v_hotel_occupancy_daily
            WHERE hotel_id = @hotelId AND night = @night
            """,
            connection);
        command.Parameters.AddWithValue("hotelId", hotel.Hotel.Id);
        command.Parameters.AddWithValue("night", booking.CheckInDate);
        await using var reader = await command.ExecuteReaderAsync();

        Assert.True(await reader.ReadAsync());
        Assert.Equal(2L, reader.GetInt64(0));
        Assert.Equal(1L, reader.GetInt64(1));
        Assert.Equal(50.00m, reader.GetDecimal(2));
        Assert.Equal(TestData.Price, reader.GetDecimal(3));
        Assert.Equal(700m, reader.GetDecimal(4));
        Assert.False(await reader.ReadAsync());
    }

    private async Task<List<AvailabilityRow>> ReadAvailabilityAsync(Guid hotelId)
    {
        await using var connection = await database.OpenConnectionAsync();
        await using var command = new NpgsqlCommand(
            """
            SELECT room_category_id, night, rooms_total, rooms_booked, rooms_free
            FROM reporting.v_category_availability_daily
            WHERE hotel_id = @hotelId
            """,
            connection);
        command.Parameters.AddWithValue("hotelId", hotelId);

        var rows = new List<AvailabilityRow>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            rows.Add(new AvailabilityRow(
                reader.GetGuid(0),
                reader.GetFieldValue<DateOnly>(1),
                reader.GetInt64(2),
                reader.GetInt64(3),
                reader.GetInt64(4)));
        }

        return rows;
    }

    private sealed record AvailabilityRow(Guid CategoryId, DateOnly Night, long RoomsTotal, long RoomsBooked, long RoomsFree);
}
