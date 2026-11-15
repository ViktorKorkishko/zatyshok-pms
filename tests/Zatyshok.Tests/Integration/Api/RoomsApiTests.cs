using System.Data;
using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Zatyshok.Api.Common.Paging;
using Zatyshok.Api.Modules.Rooms;
using Zatyshok.Domain.Modules.Rooms;

namespace Zatyshok.Tests.Integration.Api;

/// <summary>/api/v1/rooms (FR-06, FR-07, UC-08): the inventory, manual state changes, the rate lock and the hotel scope.</summary>
[Trait("Category", "Integration")]
public class RoomsApiTests(ApiFixture api) : IClassFixture<ApiFixture>
{
    private const string Url = "/api/v1/rooms";

    [Fact]
    public async Task ListOfAHotelIsSortedByFloorAndNumber()
    {
        var page = await (await api.Client.GetAsync($"{Url}?hotelId={Seed.Kyiv}&pageSize=100")).ReadAsync<PagedResponse<RoomResponse>>();

        Assert.NotEmpty(page.Items);
        Assert.All(page.Items, r => Assert.Equal(Seed.Kyiv, r.HotelId));
        Assert.Equal(page.Items.OrderBy(r => r.Floor).ThenBy(r => r.Number, StringComparer.Ordinal).Select(r => r.Id), page.Items.Select(r => r.Id));
        Assert.Contains(page.Items, r => r.Id == Seed.KyivRoom101 && r.Number == "101" && r.RoomCategoryName == "Standard");
    }

    [Fact]
    public async Task ListFiltersByCategoryStatusAndFloor()
    {
        var hotel = await CreateHotelAsync(roomCount: 3);
        await (await api.Client.PostAsync($"{Url}/{hotel.Rooms[0].Id}/out-of-order", null)).AssertStatusAsync(HttpStatusCode.OK);

        var outOfOrder = await (await api.Client.GetAsync($"{Url}?hotelId={hotel.Hotel.Id}&status=OutOfOrder")).ReadAsync<PagedResponse<RoomResponse>>();
        var free = await (await api.Client.GetAsync($"{Url}?hotelId={hotel.Hotel.Id}&roomCategoryId={hotel.Category.Id}&status=Free&floor=1")).ReadAsync<PagedResponse<RoomResponse>>();
        var otherFloor = await (await api.Client.GetAsync($"{Url}?hotelId={hotel.Hotel.Id}&floor=7")).ReadAsync<PagedResponse<RoomResponse>>();

        Assert.Equal(hotel.Rooms[0].Id, Assert.Single(outOfOrder.Items).Id);
        Assert.Equal(2, free.TotalCount);
        Assert.Empty(otherFloor.Items);
    }

    [Fact]
    public async Task UnknownStatusInTheFilterGivesValidationProblem()
    {
        var problem = await Problem.ReadAsync(await api.Client.GetAsync($"{Url}?status=Broken"), HttpStatusCode.BadRequest);

        Assert.Equal("Значення «Broken» не підходить для цього поля.", Assert.Single(problem.Errors!["status"]));
    }

    [Fact]
    public async Task UnknownRoomGives404()
    {
        var problem = await Problem.ReadAsync(await api.Client.GetAsync($"{Url}/{Guid.NewGuid()}"), HttpStatusCode.NotFound);

        Assert.Equal("Номер не знайдено.", problem.Detail);
    }

    [Fact]
    public async Task CreatedRoomIsFreeAndTakesTheCapacityOfTheCategory()
    {
        var hotel = await CreateHotelAsync();

        var response = await api.Client.PostJsonAsync(Url, new
        {
            hotelId = hotel.Hotel.Id,
            number = " 305 ",
            floor = 3,
            roomCategoryId = Seed.Suite,
            amenities = "кондиціонер, сейф",
        });

        var room = await response.ReadAsync<RoomResponse>(HttpStatusCode.Created);
        Assert.Equal($"{Url}/{room.Id}", response.Headers.Location!.AbsolutePath);
        Assert.Equal("305", room.Number);
        Assert.Equal(4, room.Capacity); // Suite of the seed
        Assert.Equal("Suite", room.RoomCategoryName);
        Assert.Equal(RoomStatus.Free, room.Status);
        Assert.True(room.IsActive);
    }

    [Fact]
    public async Task SecondRoomWithTheSameNumberGives409()
    {
        var hotel = await CreateHotelAsync();

        var response = await api.Client.PostJsonAsync(Url, NewRoom(hotel.Hotel.Id, "101", hotel.Category.Id));

        var problem = await Problem.ReadAsync(response, HttpStatusCode.Conflict);
        Assert.Equal("Номер «101» у цьому готелі вже є.", problem.Detail);
    }

    [Fact]
    public async Task InvalidFieldsGiveValidationProblem()
    {
        var response = await api.Client.PostJsonAsync(Url, new { number = "", capacity = 0, notes = new string('х', 2001) });

        var problem = await Problem.ReadAsync(response, HttpStatusCode.BadRequest);
        Assert.Equal(["capacity", "floor", "hotelId", "notes", "number", "roomCategoryId"], problem.Errors!.Keys.Order(StringComparer.Ordinal));
        Assert.Equal("Вкажіть готель.", Assert.Single(problem.Errors["hotelId"]));
    }

    [Fact]
    public async Task RoomSmallerThanTheBaseCapacityOfItsCategoryGives400()
    {
        // Guests book a category for up to its base capacity, so no room of it may hold fewer.
        var hotel = await CreateHotelAsync();
        var room = hotel.Rooms[0];

        var newRoom = await api.Client.PostJsonAsync(Url, new { hotelId = hotel.Hotel.Id, number = "900", floor = 1, roomCategoryId = Seed.Standard, capacity = 1 });
        var smaller = await api.Client.PutJsonAsync($"{Url}/{room.Id}", new { number = room.Number, floor = 1, roomCategoryId = hotel.Category.Id, capacity = 1 });
        var toSuite = await api.Client.PutJsonAsync($"{Url}/{room.Id}", new { number = room.Number, floor = 1, roomCategoryId = Seed.Suite, capacity = 2 });

        Assert.Equal(
            "Місткість номера не може бути меншою за базову місткість категорії «Standard» (2): гості бронюють категорію.",
            Assert.Single((await Problem.ReadAsync(newRoom, HttpStatusCode.BadRequest)).Errors!["capacity"]));
        await Problem.ReadAsync(smaller, HttpStatusCode.BadRequest);
        Assert.StartsWith("Місткість номера не може бути меншою за базову місткість категорії «Suite» (4)",
            Assert.Single((await Problem.ReadAsync(toSuite, HttpStatusCode.BadRequest)).Errors!["capacity"]));
        var unchanged = await GetAsync(room.Id);
        Assert.Equal((hotel.Category.Id, 2), (unchanged.RoomCategoryId, unchanged.Capacity));
    }

    [Fact]
    public async Task RoomOfAnUnknownHotelOrCategoryGives404()
    {
        var hotel = await CreateHotelAsync();

        var unknownHotel = await api.Client.PostJsonAsync(Url, NewRoom(Guid.NewGuid(), "900", hotel.Category.Id));
        var unknownCategory = await api.Client.PostJsonAsync(Url, NewRoom(hotel.Hotel.Id, "900", Guid.NewGuid()));

        Assert.Equal("Готель не знайдено.", (await Problem.ReadAsync(unknownHotel, HttpStatusCode.NotFound)).Detail);
        Assert.Equal("Категорію номерів не знайдено.", (await Problem.ReadAsync(unknownCategory, HttpStatusCode.NotFound)).Detail);
    }

    [Fact]
    public async Task RoomOfADeactivatedCategoryGives409()
    {
        var hotel = await CreateHotelAsync();
        await using (var db = api.CreateDbContext())
        {
            await db.RoomCategories.Where(c => c.Id == hotel.Category.Id).ExecuteUpdateAsync(s => s.SetProperty(c => c.IsActive, false));
        }

        var response = await api.Client.PostJsonAsync(Url, NewRoom(hotel.Hotel.Id, "900", hotel.Category.Id));

        Assert.StartsWith("Категорію «", (await Problem.ReadAsync(response, HttpStatusCode.Conflict)).Detail);
    }

    [Fact]
    public async Task UpdateReplacesFieldsAndMovesAFreeRoomToAnotherCategory()
    {
        var hotel = await CreateHotelAsync();
        var room = hotel.Rooms[0];

        var updated = await (await api.Client.PutJsonAsync($"{Url}/{room.Id}", new
        {
            number = "101A",
            floor = 2,
            roomCategoryId = Seed.Superior,
            capacity = 3,
            notes = "Вид на парк",
        })).ReadAsync<RoomResponse>();

        Assert.Equal("101A", updated.Number);
        Assert.Equal(2, updated.Floor);
        Assert.Equal(Seed.Superior, updated.RoomCategoryId);
        Assert.Equal(3, updated.Capacity);
        Assert.Equal("Вид на парк", updated.Notes);
        Assert.Null(updated.Amenities);
    }

    [Fact]
    public async Task CategoryOfAReservedRoomDoesNotChange()
    {
        var hotel = await CreateHotelAsync();
        var room = await ReserveAsync(hotel);

        var response = await api.Client.PutJsonAsync($"{Url}/{room.Id}", new { number = room.Number, floor = 1, roomCategoryId = Seed.Superior, capacity = 2 });

        var problem = await Problem.ReadAsync(response, HttpStatusCode.Conflict);
        Assert.StartsWith("Номер заброньований", problem.Detail);
    }

    [Fact]
    public async Task UpdateWithoutCapacityGives400AndKeepsIt()
    {
        var hotel = await CreateHotelAsync();
        var room = hotel.Rooms[0];
        await (await api.Client.PutJsonAsync($"{Url}/{room.Id}", new { number = room.Number, floor = 1, roomCategoryId = hotel.Category.Id, capacity = 3 }))
            .AssertStatusAsync(HttpStatusCode.OK);

        // Before the fix an update without capacity reset it to the base capacity of the category (2).
        var response = await api.Client.PutJsonAsync($"{Url}/{room.Id}", new { number = room.Number, floor = 1, roomCategoryId = hotel.Category.Id, notes = "Вид на парк" });

        var problem = await Problem.ReadAsync(response, HttpStatusCode.BadRequest);
        Assert.Equal("Вкажіть місткість номера.", Assert.Single(problem.Errors!["capacity"]));
        Assert.Equal(3, (await GetAsync(room.Id)).Capacity);
    }

    [Fact]
    public async Task OutOfOrderAndReturnToServiceFollowFigure6_2()
    {
        var hotel = await CreateHotelAsync();
        var room = hotel.Rooms[0];

        var outOfOrder = await (await api.Client.PostAsync($"{Url}/{room.Id}/out-of-order", null)).ReadAsync<RoomResponse>();
        var returned = await (await api.Client.PostAsync($"{Url}/{room.Id}/return-to-service", null)).ReadAsync<RoomResponse>();

        Assert.Equal(RoomStatus.OutOfOrder, outOfOrder.Status);
        Assert.Equal(RoomStatus.Dirty, returned.Status); // a deep cleaning comes first (FR-30)
    }

    [Fact]
    public async Task OccupiedRoomIsNotPutOutOfOrder()
    {
        var hotel = await CreateHotelAsync();
        var room = hotel.Rooms[0];
        await ChangeInDatabaseAsync(room.Id, r => r.Occupy(reservedForThisBooking: false));

        var response = await api.Client.PostAsync($"{Url}/{room.Id}/out-of-order", null);

        var problem = await Problem.ReadAsync(response, HttpStatusCode.Conflict);
        Assert.StartsWith("Несправність у зайнятому номері записується в примітки", problem.Detail);
        Assert.Equal(RoomStatus.Occupied, (await GetAsync(room.Id)).Status);
    }

    [Fact]
    public async Task FreeRoomIsNotReturnedToService()
    {
        var hotel = await CreateHotelAsync();

        var response = await api.Client.PostAsync($"{Url}/{hotel.Rooms[0].Id}/return-to-service", null);

        var problem = await Problem.ReadAsync(response, HttpStatusCode.Conflict);
        Assert.Equal("Номер: дію «повернути в експлуатацію» заборонено в стані «вільний».", problem.Detail);
    }

    [Fact]
    public async Task OccupiedRoomIsNotDeactivated()
    {
        var hotel = await CreateHotelAsync();
        var room = hotel.Rooms[0];
        await ChangeInDatabaseAsync(room.Id, r => r.Occupy(reservedForThisBooking: false));

        var problem = await Problem.ReadAsync(await api.Client.PostAsync($"{Url}/{room.Id}/deactivate", null), HttpStatusCode.Conflict);

        Assert.Equal("Зайнятий номер не можна деактивувати (UC-08 А2).", problem.Detail);
    }

    [Fact]
    public async Task DeactivatedRoomCanBeActivatedAgain()
    {
        var hotel = await CreateHotelAsync();
        var room = hotel.Rooms[0];

        var deactivated = await (await api.Client.PostAsync($"{Url}/{room.Id}/deactivate", null)).ReadAsync<RoomResponse>();

        // UC-08: a deactivated room leaves the lists; it is found only when asked for, to activate it again.
        var byDefault = await (await api.Client.GetAsync($"{Url}?hotelId={hotel.Hotel.Id}")).ReadAsync<PagedResponse<RoomResponse>>();
        var inactive = await (await api.Client.GetAsync($"{Url}?hotelId={hotel.Hotel.Id}&isActive=false")).ReadAsync<PagedResponse<RoomResponse>>();
        var activated = await (await api.Client.PostAsync($"{Url}/{room.Id}/activate", null)).ReadAsync<RoomResponse>();

        Assert.False(deactivated.IsActive);
        Assert.Empty(byDefault.Items);
        Assert.Equal(room.Id, Assert.Single(inactive.Items).Id);
        Assert.True(activated.IsActive);
    }

    [Fact]
    public async Task RoomWithoutHistoryIsDeleted()
    {
        var hotel = await CreateHotelAsync();

        await (await api.Client.DeleteAsync($"{Url}/{hotel.Rooms[0].Id}")).AssertStatusAsync(HttpStatusCode.NoContent);

        await Problem.ReadAsync(await api.Client.GetAsync($"{Url}/{hotel.Rooms[0].Id}"), HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task RoomWithABookingIsNotDeleted()
    {
        var hotel = await CreateHotelAsync();
        var room = await ReserveAsync(hotel);

        var problem = await Problem.ReadAsync(await api.Client.DeleteAsync($"{Url}/{room.Id}"), HttpStatusCode.Conflict);

        Assert.EndsWith("Деактивуйте його.", problem.Detail);
        Assert.Equal(RoomStatus.Reserved, (await GetAsync(room.Id)).Status);
    }

    [Fact]
    public async Task OutOfOrderWaitsForTheRateLockOfItsCategory()
    {
        // 12.8.2: a booking of the pair holds the rate row; putting a room of the pair out of order
        // must wait until the booking's transaction ends, so the booking counts the rooms correctly.
        var hotel = await CreateHotelAsync();
        await using var booking = api.CreateDbContext();
        await using var transaction = await booking.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
        await booking.Database.ExecuteSqlAsync($"""SELECT 1 FROM rooms."Rates" WHERE "Id" = {hotel.Rate.Id} FOR UPDATE""");

        var request = api.Client.PostAsync($"{Url}/{hotel.Rooms[0].Id}/out-of-order", null);
        await RowLocks.WaitUntilSomeSessionWaitsForLockAsync(api);

        Assert.False(request.IsCompleted);
        await transaction.CommitAsync();
        Assert.Equal(RoomStatus.OutOfOrder, (await (await request).ReadAsync<RoomResponse>()).Status);
    }

    [Theory]
    [InlineData("deactivate")]
    [InlineData("delete")]
    [InlineData("change category")]
    public async Task OtherChangesThatReduceAvailabilityWaitForTheRateLock(string change)
    {
        // The same rule of 12.8.2 for the other operations that take a room away from its pair; a new
        // category takes it away from the old pair, so the old rate is the one locked.
        var hotel = await CreateHotelAsync();
        var room = hotel.Rooms[0];
        await using var booking = api.CreateDbContext();
        await using var transaction = await booking.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
        await booking.Database.ExecuteSqlAsync($"""SELECT 1 FROM rooms."Rates" WHERE "Id" = {hotel.Rate.Id} FOR UPDATE""");

        var request = change switch
        {
            "deactivate" => api.Client.PostAsync($"{Url}/{room.Id}/deactivate", null),
            "delete" => api.Client.DeleteAsync($"{Url}/{room.Id}"),
            _ => api.Client.PutJsonAsync($"{Url}/{room.Id}", new { number = room.Number, floor = 1, roomCategoryId = Seed.Superior, capacity = 2 }),
        };
        await RowLocks.WaitUntilSomeSessionWaitsForLockAsync(api);

        Assert.False(request.IsCompleted);
        await transaction.CommitAsync();
        await (await request).AssertStatusAsync(change == "delete" ? HttpStatusCode.NoContent : HttpStatusCode.OK);
    }

    [Fact]
    public async Task UserOfOneHotelSeesOnlyItsRooms()
    {
        var kyivOnly = api.CreateClientForHotel(Seed.Kyiv);
        var lvivRoom = Seed.LvivRoom101;

        var all = await (await kyivOnly.GetAsync($"{Url}?pageSize=100")).ReadAsync<PagedResponse<RoomResponse>>();
        var foreignRoom = await kyivOnly.GetAsync($"{Url}/{lvivRoom}");
        var foreignFilter = await kyivOnly.GetAsync($"{Url}?hotelId={Seed.Lviv}");
        var foreignNewRoom = await kyivOnly.PostJsonAsync(Url, NewRoom(Seed.Lviv, "999", Seed.Standard));
        var foreignChange = await kyivOnly.PostAsync($"{Url}/{lvivRoom}/out-of-order", null);

        Assert.All(all.Items, r => Assert.Equal(Seed.Kyiv, r.HotelId));
        await Problem.ReadAsync(foreignRoom, HttpStatusCode.NotFound);
        Assert.Equal("Доступ заборонено", (await Problem.ReadAsync(foreignFilter, HttpStatusCode.Forbidden)).Title);
        await Problem.ReadAsync(foreignNewRoom, HttpStatusCode.Forbidden);
        await Problem.ReadAsync(foreignChange, HttpStatusCode.NotFound);
        Assert.Equal(RoomStatus.Free, (await GetAsync(lvivRoom)).Status);
    }

    [Fact]
    public async Task StateChangeIsLoggedWithTheCorrelationIdOfTheRequest()
    {
        var logs = new CapturedLogs();
        var client = api.CreateClient(services => services.AddSingleton<ILoggerProvider>(logs));
        var hotel = await CreateHotelAsync();
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{Url}/{hotel.Rooms[0].Id}/out-of-order");
        request.Headers.Add("X-Correlation-Id", "room-log-1");

        await (await client.SendAsync(request)).AssertStatusAsync(HttpStatusCode.OK);

        var record = Assert.Single(logs.Records, r => r.Category.EndsWith(".RoomService") && r.Message.Contains(hotel.Rooms[0].Id.ToString()));
        Assert.Contains("Free -> OutOfOrder", record.Message);
        Assert.Equal("room-log-1", record.ScopeValues["CorrelationId"]);
    }

    private static object NewRoom(Guid hotelId, string number, Guid categoryId)
        => new { hotelId, number, floor = 1, roomCategoryId = categoryId };

    private async Task<TestHotel> CreateHotelAsync(int roomCount = 1)
    {
        await using var db = api.CreateDbContext();
        return await TestData.CreateHotelAsync(db, roomCount);
    }

    private async Task<RoomResponse> GetAsync(Guid roomId)
        => await (await api.Client.GetAsync($"{Url}/{roomId}")).ReadAsync<RoomResponse>();

    /// <summary>Brings a room to a state the API of this step does not set (check-in is T5).</summary>
    private async Task ChangeInDatabaseAsync(Guid roomId, Action<Room> change)
    {
        await using var db = api.CreateDbContext();
        var room = await db.Rooms.SingleAsync(r => r.Id == roomId);
        change(room);
        await db.SaveChangesAsync();
    }

    /// <summary>Pre-assigns the first room of the hotel to a booking arriving today, as reception will in T5.</summary>
    private async Task<Room> ReserveAsync(TestHotel hotel)
    {
        await using var db = api.CreateDbContext();
        var room = await db.Rooms.SingleAsync(r => r.Id == hotel.Rooms[0].Id);
        var booking = TestData.ConfirmedWithRoom(hotel, room);
        room.Reserve();
        db.Bookings.Add(booking);
        await db.SaveChangesAsync();
        return room;
    }
}
