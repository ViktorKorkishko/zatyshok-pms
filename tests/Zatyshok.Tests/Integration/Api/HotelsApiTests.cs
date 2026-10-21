using System.Net;
using System.Text;
using Zatyshok.Api.Common.Paging;
using Zatyshok.Api.Modules.Rooms;

namespace Zatyshok.Tests.Integration.Api;

/// <summary>/api/v1/hotels (FR-05, UC-11), and through it the errors every endpoint shares.</summary>
[Trait("Category", "Integration")]
public class HotelsApiTests(ApiFixture api) : IClassFixture<ApiFixture>
{
    private const string Url = "/api/v1/hotels";

    [Fact]
    public async Task ListIsPagedAndSortedByName()
    {
        var first = await (await api.Client.GetAsync($"{Url}?page=1&pageSize=2")).ReadAsync<PagedResponse<HotelResponse>>();
        var second = await (await api.Client.GetAsync($"{Url}?page=2&pageSize=2")).ReadAsync<PagedResponse<HotelResponse>>();

        Assert.Equal(2, first.Items.Count);
        Assert.True(first.TotalCount >= 5, "the seed has five hotels");
        Assert.Equal((first.TotalCount + 1) / 2, first.TotalPages);
        var names = first.Items.Concat(second.Items).Select(h => h.Name).ToList();
        Assert.Equal(names.Order(StringComparer.Ordinal), names);
    }

    [Fact]
    public async Task ListFiltersByCityAndActivity()
    {
        var hotel = await CreateHotelAsync(city: "Ужгород");
        await (await api.Client.PostAsync($"{Url}/{hotel.Id}/deactivate", null)).AssertStatusAsync(HttpStatusCode.OK);

        var lviv = await (await api.Client.GetAsync($"{Url}?city=Львів")).ReadAsync<PagedResponse<HotelResponse>>();
        var activeInUzhhorod = await (await api.Client.GetAsync($"{Url}?city=Ужгород&isActive=true")).ReadAsync<PagedResponse<HotelResponse>>();
        var inactiveInUzhhorod = await (await api.Client.GetAsync($"{Url}?city=Ужгород&isActive=false")).ReadAsync<PagedResponse<HotelResponse>>();

        Assert.Equal(Seed.Lviv, Assert.Single(lviv.Items).Id);
        Assert.Empty(activeInUzhhorod.Items);
        Assert.Equal(hotel.Id, Assert.Single(inactiveInUzhhorod.Items).Id);
    }

    [Theory]
    [InlineData("pageSize=101", "pageSize")]
    [InlineData("pageSize=0", "pageSize")]
    [InlineData("page=0", "page")]
    public async Task InvalidPageGivesValidationProblem(string queryString, string field)
    {
        var response = await api.Client.GetAsync($"{Url}?{queryString}");

        var problem = await Problem.ReadAsync(response, HttpStatusCode.BadRequest);
        Assert.Matches("^[А-ЯІЇЄҐ]", Assert.Single(problem.Errors![field]));
    }

    [Fact]
    public async Task QueryValueOfTheWrongTypeGivesUkrainianMessage()
    {
        var response = await api.Client.GetAsync($"{Url}?page=abc");

        var problem = await Problem.ReadAsync(response, HttpStatusCode.BadRequest);
        Assert.Equal("Значення «abc» не підходить для цього поля.", Assert.Single(problem.Errors!["page"]));
    }

    [Fact]
    public async Task UnknownHotelGives404()
    {
        var response = await api.Client.GetAsync($"{Url}/{Guid.NewGuid()}");

        var problem = await Problem.ReadAsync(response, HttpStatusCode.NotFound);
        Assert.Equal("Готель не знайдено.", problem.Detail);
    }

    [Fact]
    public async Task CreateReturns201WithLocationAndTakesDefaultRules()
    {
        var response = await api.Client.PostJsonAsync(Url, new
        {
            name = "  Затишок Ужгород  ",
            city = "Ужгород",
            address = "вул. Корзо, 7",
            stars = 3,
            timeZoneId = "Europe/Kyiv",
            phone = "  ",
        });

        var created = await response.ReadAsync<HotelResponse>(HttpStatusCode.Created);
        Assert.Equal($"{Url}/{created.Id}", response.Headers.Location!.AbsolutePath);
        Assert.Equal("Затишок Ужгород", created.Name);
        Assert.Null(created.Phone);
        Assert.True(created.IsActive);
        Assert.Equal(1, created.CancellationDeadlineDaysBefore);
        Assert.Equal(new TimeOnly(18, 0), created.CancellationDeadlineTime);
        Assert.Equal(new TimeOnly(14, 0), created.CheckInTime);
        Assert.Equal(new TimeOnly(12, 0), created.CheckOutTime);

        var read = await (await api.Client.GetAsync(response.Headers.Location)).ReadAsync<HotelResponse>();
        Assert.Equal(created.Name, read.Name);
    }

    [Fact]
    public async Task InvalidFieldsGiveOneUkrainianMessagePerField()
    {
        var response = await api.Client.PostJsonAsync(Url, new
        {
            name = "",
            city = "Ужгород",
            address = new string('а', 301),
            stars = 7,
            timeZoneId = "Mars/Olympus",
            email = "not-an-email",
            cancellationDeadlineDaysBefore = -1,
        });

        var problem = await Problem.ReadAsync(response, HttpStatusCode.BadRequest);
        Assert.Equal("Запит містить помилки", problem.Title);
        Assert.Equal(
            ["address", "cancellationDeadlineDaysBefore", "email", "name", "stars", "timeZoneId"],
            problem.Errors!.Keys.Order(StringComparer.Ordinal));
        Assert.Equal("Вкажіть назву готелю.", Assert.Single(problem.Errors["name"]));
        Assert.StartsWith("Невідомий часовий пояс", Assert.Single(problem.Errors["timeZoneId"]));
    }

    [Fact]
    public async Task EmptyTimeZoneGivesOnlyTheFirstMessage()
    {
        // RequestValidator stops a field at its first broken rule.
        var response = await api.Client.PostJsonAsync(Url, new { name = "Х", city = "Х", address = "Х", stars = 3, timeZoneId = "" });

        var problem = await Problem.ReadAsync(response, HttpStatusCode.BadRequest);
        Assert.Equal("Вкажіть часовий пояс готелю, наприклад Europe/Kyiv.", Assert.Single(problem.Errors!["timeZoneId"]));
    }

    [Fact]
    public async Task MalformedJsonGivesValidationProblem()
    {
        using var content = new StringContent("{ \"name\": ", Encoding.UTF8, "application/json");

        var response = await api.Client.PostAsync(Url, content);

        var problem = await Problem.ReadAsync(response, HttpStatusCode.BadRequest);
        Assert.NotEmpty(problem.Errors!);
        Assert.All(problem.Errors!.Values.SelectMany(m => m), message => Assert.Matches("^[А-ЯІЇЄҐ]", message));
    }

    [Fact]
    public async Task FieldOfTheWrongTypeGivesUkrainianMessage()
    {
        var response = await api.Client.PostJsonAsync(Url, new { name = "Х", city = "Х", address = "Х", stars = "три", timeZoneId = "Europe/Kyiv" });

        var problem = await Problem.ReadAsync(response, HttpStatusCode.BadRequest);
        Assert.Equal("Некоректне значення.", Assert.Single(problem.Errors!["$.stars"]));
    }

    [Fact]
    public async Task UpdateReplacesAllFields()
    {
        var hotel = await CreateHotelAsync();

        var updated = await (await api.Client.PutJsonAsync($"{Url}/{hotel.Id}", new
        {
            name = "Затишок Мукачево",
            city = "Мукачево",
            address = "вул. Миру, 1",
            stars = 4,
            timeZoneId = "Europe/Kyiv",
            email = "mukachevo@zatyshok.ua",
            cancellationDeadlineDaysBefore = 2,
            cancellationDeadlineTime = "12:00:00",
            checkInTime = "15:00:00",
            checkOutTime = "11:00:00",
        })).ReadAsync<HotelResponse>();

        var read = await (await api.Client.GetAsync($"{Url}/{hotel.Id}")).ReadAsync<HotelResponse>();
        Assert.Equal("Мукачево", read.City);
        Assert.Equal(4, read.Stars);
        Assert.Equal(2, read.CancellationDeadlineDaysBefore);
        Assert.Equal(new TimeOnly(12, 0), read.CancellationDeadlineTime);
        Assert.Equal(new TimeOnly(15, 0), read.CheckInTime);
        Assert.Equal(new TimeOnly(11, 0), read.CheckOutTime);
        Assert.Equal(updated.Email, read.Email);
    }

    [Fact]
    public async Task UpdateOfUnknownHotelGives404()
    {
        var response = await api.Client.PutJsonAsync($"{Url}/{Guid.NewGuid()}", ValidHotel());

        await Problem.ReadAsync(response, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeactivatedHotelCanBeActivatedAgain()
    {
        var hotel = await CreateHotelAsync();

        var deactivated = await (await api.Client.PostAsync($"{Url}/{hotel.Id}/deactivate", null)).ReadAsync<HotelResponse>();
        var activated = await (await api.Client.PostAsync($"{Url}/{hotel.Id}/activate", null)).ReadAsync<HotelResponse>();

        Assert.False(deactivated.IsActive);
        Assert.True(activated.IsActive);
    }

    [Fact]
    public async Task UnusedHotelIsDeleted()
    {
        var hotel = await CreateHotelAsync();

        var response = await api.Client.DeleteAsync($"{Url}/{hotel.Id}");

        await response.AssertStatusAsync(HttpStatusCode.NoContent);
        await Problem.ReadAsync(await api.Client.GetAsync($"{Url}/{hotel.Id}"), HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task HotelWithRoomsIsNotDeleted()
    {
        var response = await api.Client.DeleteAsync($"{Url}/{Seed.Kyiv}");

        var problem = await Problem.ReadAsync(response, HttpStatusCode.Conflict);
        Assert.Equal("Конфлікт із поточним станом даних", problem.Title);
        Assert.EndsWith("Деактивуйте його.", problem.Detail);
        await (await api.Client.GetAsync($"{Url}/{Seed.Kyiv}")).AssertStatusAsync(HttpStatusCode.OK);
    }

    private static object ValidHotel(string city = "Берегове") => new
    {
        name = $"Тестовий готель {Guid.NewGuid():N}",
        city,
        address = "вул. Тестова, 1",
        stars = 3,
        timeZoneId = "Europe/Kyiv",
    };

    private async Task<HotelResponse> CreateHotelAsync(string city = "Берегове")
        => await (await api.Client.PostJsonAsync(Url, ValidHotel(city))).ReadAsync<HotelResponse>(HttpStatusCode.Created);
}
