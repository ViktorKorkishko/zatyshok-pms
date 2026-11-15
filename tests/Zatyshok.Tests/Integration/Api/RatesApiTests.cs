using System.Data;
using System.Net;
using Microsoft.EntityFrameworkCore;
using Zatyshok.Api.Common.Paging;
using Zatyshok.Api.Modules.Rooms;

namespace Zatyshok.Tests.Integration.Api;

/// <summary>/api/v1/rates and /api/v1/season-rates (FR-08).</summary>
[Trait("Category", "Integration")]
public class RatesApiTests(ApiFixture api) : IClassFixture<ApiFixture>
{
    private const string RatesUrl = "/api/v1/rates";
    private const string SeasonsUrl = "/api/v1/season-rates";

    [Fact]
    public async Task ListOfAHotelHasOneRatePerCategory()
    {
        var page = await (await api.Client.GetAsync($"{RatesUrl}?hotelId={Seed.Kyiv}")).ReadAsync<PagedResponse<RateResponse>>();

        Assert.Equal(3, page.TotalCount);
        var standard = Assert.Single(page.Items, r => r.RoomCategoryId == Seed.Standard);
        Assert.Equal(Seed.KyivStandardRate, standard.Id);
        Assert.Equal(2500m, standard.BasePricePerNight);
    }

    [Fact]
    public async Task RateIsCreatedForAPairWithoutOne()
    {
        var (hotelId, categoryId) = await CreatePairWithoutRateAsync();

        var response = await api.Client.PostJsonAsync(RatesUrl, new { hotelId, roomCategoryId = categoryId, basePricePerNight = 1999.99m });

        var rate = await response.ReadAsync<RateResponse>(HttpStatusCode.Created);
        Assert.Equal(1999.99m, rate.BasePricePerNight);
        Assert.Equal(rate.Id, (await (await api.Client.GetAsync(response.Headers.Location)).ReadAsync<RateResponse>()).Id);
    }

    [Fact]
    public async Task SecondRateOfAPairGives409()
    {
        var response = await api.Client.PostJsonAsync(RatesUrl, new { hotelId = Seed.Kyiv, roomCategoryId = Seed.Standard, basePricePerNight = 100m });

        var problem = await Problem.ReadAsync(response, HttpStatusCode.Conflict);
        Assert.Equal("Тариф категорії «Standard» у цьому готелі вже є. Змініть його ціну.", problem.Detail);
    }

    [Fact]
    public async Task InvalidRateGivesValidationProblem()
    {
        var response = await api.Client.PostJsonAsync(RatesUrl, new { basePricePerNight = 0m });

        var problem = await Problem.ReadAsync(response, HttpStatusCode.BadRequest);
        Assert.Equal(["basePricePerNight", "hotelId", "roomCategoryId"], problem.Errors!.Keys.Order(StringComparer.Ordinal));
        Assert.Equal("Ціна за ніч має бути більшою за 0.", Assert.Single(problem.Errors["basePricePerNight"]));
    }

    [Fact]
    public async Task BasePriceIsChanged()
    {
        var hotel = await CreateHotelAsync();

        var updated = await (await api.Client.PutJsonAsync($"{RatesUrl}/{hotel.Rate.Id}", new { basePricePerNight = 2750.5m })).ReadAsync<RateResponse>();

        Assert.Equal(2750.5m, updated.BasePricePerNight);
        Assert.Equal(hotel.Category.Id, updated.RoomCategoryId);
    }

    [Fact]
    public async Task UnknownRateGives404()
    {
        var problem = await Problem.ReadAsync(await api.Client.PutJsonAsync($"{RatesUrl}/{Guid.NewGuid()}", new { basePricePerNight = 1m }), HttpStatusCode.NotFound);

        Assert.Equal("Тариф не знайдено.", problem.Detail);
    }

    [Fact]
    public async Task UserOfOneHotelSeesOnlyItsRates()
    {
        var kyivOnly = api.CreateClientForHotel(Seed.Kyiv);
        var lvivStandardRate = Seed.LvivStandardRate;

        var all = await (await kyivOnly.GetAsync($"{RatesUrl}?pageSize=100")).ReadAsync<PagedResponse<RateResponse>>();

        Assert.All(all.Items, r => Assert.Equal(Seed.Kyiv, r.HotelId));
        await Problem.ReadAsync(await kyivOnly.GetAsync($"{RatesUrl}/{lvivStandardRate}"), HttpStatusCode.NotFound);
        await Problem.ReadAsync(await kyivOnly.GetAsync($"{RatesUrl}?hotelId={Seed.Lviv}"), HttpStatusCode.Forbidden);
        await Problem.ReadAsync(
            await kyivOnly.PostJsonAsync(SeasonsUrl, NewSeason(lvivStandardRate, "2030-01-01", "2030-01-10")), HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task SeasonIsCreatedChangedAndDeleted()
    {
        var hotel = await CreateHotelAsync();

        var response = await api.Client.PostJsonAsync(SeasonsUrl, NewSeason(hotel.Rate.Id, "2030-06-01", "2030-08-31", price: 3100m));
        var created = await response.ReadAsync<SeasonRateResponse>(HttpStatusCode.Created);
        var updated = await (await api.Client.PutJsonAsync($"{SeasonsUrl}/{created.Id}", new
        {
            name = "Літо 2030 (продовжене)",
            startDate = "2030-06-01",
            endDate = "2030-09-15",
            pricePerNight = 3000m,
        })).ReadAsync<SeasonRateResponse>();
        var list = await (await api.Client.GetAsync($"{SeasonsUrl}?rateId={hotel.Rate.Id}")).ReadAsync<PagedResponse<SeasonRateResponse>>();
        var deleted = await api.Client.DeleteAsync($"{SeasonsUrl}/{created.Id}");

        Assert.Equal(hotel.Hotel.Id, created.HotelId);
        Assert.Equal(hotel.Category.Id, created.RoomCategoryId);
        Assert.Equal(new DateOnly(2030, 9, 15), updated.EndDate);
        Assert.Equal(3000m, updated.PricePerNight);
        Assert.Equal(created.Id, Assert.Single(list.Items).Id);
        await deleted.AssertStatusAsync(HttpStatusCode.NoContent);
        await Problem.ReadAsync(await api.Client.GetAsync($"{SeasonsUrl}/{created.Id}"), HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData("2030-05-20", "2030-06-01")] // shares the first night
    [InlineData("2030-06-30", "2030-07-05")] // shares the last night
    [InlineData("2030-06-10", "2030-06-12")] // inside
    public async Task OverlappingSeasonOfTheSameRateGives409(string start, string end)
    {
        var hotel = await CreateHotelAsync();
        await CreateSeasonAsync(hotel.Rate.Id, "2030-06-01", "2030-06-30");

        var response = await api.Client.PostJsonAsync(SeasonsUrl, NewSeason(hotel.Rate.Id, start, end));

        var problem = await Problem.ReadAsync(response, HttpStatusCode.Conflict);
        Assert.Equal("Період перетинається із сезоном «Сезон» (01.06.2030–30.06.2030).", problem.Detail);
    }

    [Fact]
    public async Task AdjacentSeasonsAndSeasonsOfOtherRatesDoNotOverlap()
    {
        var hotel = await CreateHotelAsync();
        var other = await CreateHotelAsync();
        await CreateSeasonAsync(hotel.Rate.Id, "2030-06-01", "2030-06-30");

        await CreateSeasonAsync(hotel.Rate.Id, "2030-07-01", "2030-07-31");
        await CreateSeasonAsync(other.Rate.Id, "2030-06-01", "2030-06-30");
    }

    [Fact]
    public async Task ChangedSeasonIsNotComparedWithItself()
    {
        var hotel = await CreateHotelAsync();
        var season = await CreateSeasonAsync(hotel.Rate.Id, "2030-06-01", "2030-06-30");
        await CreateSeasonAsync(hotel.Rate.Id, "2030-08-01", "2030-08-31");

        var shifted = await api.Client.PutJsonAsync($"{SeasonsUrl}/{season.Id}", new { name = "Сезон", startDate = "2030-06-15", endDate = "2030-07-15", pricePerNight = 100m });
        var intoTheOther = await api.Client.PutJsonAsync($"{SeasonsUrl}/{season.Id}", new { name = "Сезон", startDate = "2030-06-15", endDate = "2030-08-01", pricePerNight = 100m });

        await shifted.AssertStatusAsync(HttpStatusCode.OK);
        await Problem.ReadAsync(intoTheOther, HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task InvalidSeasonGivesValidationProblem()
    {
        var response = await api.Client.PostJsonAsync(SeasonsUrl, new { name = "", startDate = "2030-06-10", endDate = "2030-06-01", pricePerNight = 10.001m });

        var problem = await Problem.ReadAsync(response, HttpStatusCode.BadRequest);
        Assert.Equal(["endDate", "name", "pricePerNight", "rateId"], problem.Errors!.Keys.Order(StringComparer.Ordinal));
        Assert.Equal("Остання ніч періоду не може бути раніше за першу.", Assert.Single(problem.Errors["endDate"]));
    }

    [Fact]
    public async Task SeasonChangeWaitsForTheRateLock()
    {
        // The overlap check is safe only under the rate lock: a second request waits for the first.
        var hotel = await CreateHotelAsync();
        await using var other = api.CreateDbContext();
        await using var transaction = await other.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
        await other.Database.ExecuteSqlAsync($"""SELECT 1 FROM rooms."Rates" WHERE "Id" = {hotel.Rate.Id} FOR UPDATE""");

        var request = api.Client.PostJsonAsync(SeasonsUrl, NewSeason(hotel.Rate.Id, "2030-01-01", "2030-01-10"));
        await RowLocks.WaitUntilSomeSessionWaitsForLockAsync(api);

        Assert.False(request.IsCompleted);
        await transaction.CommitAsync();
        await (await request).AssertStatusAsync(HttpStatusCode.Created);
    }

    private static object NewSeason(Guid rateId, string start, string end, decimal price = 2000m)
        => new { rateId, name = "Сезон", startDate = start, endDate = end, pricePerNight = price };

    private async Task<SeasonRateResponse> CreateSeasonAsync(Guid rateId, string start, string end)
        => await (await api.Client.PostJsonAsync(SeasonsUrl, NewSeason(rateId, start, end))).ReadAsync<SeasonRateResponse>(HttpStatusCode.Created);

    private async Task<TestHotel> CreateHotelAsync()
    {
        await using var db = api.CreateDbContext();
        return await TestData.CreateHotelAsync(db);
    }

    private async Task<(Guid HotelId, Guid CategoryId)> CreatePairWithoutRateAsync()
    {
        await using var db = api.CreateDbContext();
        var hotel = TestData.NewHotel();
        var category = TestData.NewCategory();
        db.Hotels.Add(hotel);
        db.RoomCategories.Add(category);
        await db.SaveChangesAsync();
        return (hotel.Id, category.Id);
    }
}
