using System.Net;
using Zatyshok.Api.Common.Paging;
using Zatyshok.Api.Modules.Rooms;

namespace Zatyshok.Tests.Integration.Api;

/// <summary>/api/v1/room-categories (FR-05, UC-11).</summary>
[Trait("Category", "Integration")]
public class RoomCategoriesApiTests(ApiFixture api) : IClassFixture<ApiFixture>
{
    private const string Url = "/api/v1/room-categories";

    [Fact]
    public async Task ListContainsTheSeededCategories()
    {
        var page = await (await api.Client.GetAsync($"{Url}?pageSize=100&isActive=true")).ReadAsync<PagedResponse<RoomCategoryResponse>>();

        Assert.Contains(page.Items, c => c.Id == Seed.Standard && c.Name == "Standard" && c.Capacity == 2);
        Assert.Contains(page.Items, c => c.Id == Seed.Suite && c.Capacity == 4);
    }

    [Fact]
    public async Task CreateReturns201AndTheCategoryCanBeRead()
    {
        var response = await api.Client.PostJsonAsync(Url, new { name = UniqueName(), description = "Дві кімнати", capacity = 4 });

        var created = await response.ReadAsync<RoomCategoryResponse>(HttpStatusCode.Created);
        var read = await (await api.Client.GetAsync(response.Headers.Location)).ReadAsync<RoomCategoryResponse>();
        Assert.Equal(created.Name, read.Name);
        Assert.Equal("Дві кімнати", read.Description);
        Assert.Equal(4, read.Capacity);
        Assert.True(read.IsActive);
    }

    [Fact]
    public async Task InvalidFieldsGiveValidationProblem()
    {
        var response = await api.Client.PostJsonAsync(Url, new { name = " ", capacity = 0 });

        var problem = await Problem.ReadAsync(response, HttpStatusCode.BadRequest);
        Assert.Equal("Вкажіть назву категорії.", Assert.Single(problem.Errors!["name"]));
        Assert.Equal("Місткість має бути більшою за 0.", Assert.Single(problem.Errors["capacity"]));
    }

    [Fact]
    public async Task DuplicateNameGives409()
    {
        // The unique index IX_RoomCategories_Name gives 23505, which the service turns into a message.
        var response = await api.Client.PostJsonAsync(Url, new { name = "Standard", capacity = 2 });

        var problem = await Problem.ReadAsync(response, HttpStatusCode.Conflict);
        Assert.Equal("Категорія «Standard» уже існує.", problem.Detail);
    }

    [Fact]
    public async Task RenameToAnExistingNameGives409()
    {
        var category = await CreateCategoryAsync();

        var response = await api.Client.PutJsonAsync($"{Url}/{category.Id}", new { name = "Suite", capacity = 2 });

        await Problem.ReadAsync(response, HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task UpdateReplacesFields()
    {
        var category = await CreateCategoryAsync();
        var newName = UniqueName();

        var updated = await (await api.Client.PutJsonAsync($"{Url}/{category.Id}", new { name = newName, capacity = 3 })).ReadAsync<RoomCategoryResponse>();

        Assert.Equal(newName, updated.Name);
        Assert.Null(updated.Description);
        Assert.Equal(3, updated.Capacity);
    }

    [Fact]
    public async Task UnknownCategoryGives404()
    {
        var problem = await Problem.ReadAsync(await api.Client.GetAsync($"{Url}/{Guid.NewGuid()}"), HttpStatusCode.NotFound);

        Assert.Equal("Категорію номерів не знайдено.", problem.Detail);
    }

    [Fact]
    public async Task DeactivatedCategoryIsListedAsInactive()
    {
        var category = await CreateCategoryAsync();

        await (await api.Client.PostAsync($"{Url}/{category.Id}/deactivate", null)).AssertStatusAsync(HttpStatusCode.OK);

        var inactive = await (await api.Client.GetAsync($"{Url}?isActive=false&pageSize=100")).ReadAsync<PagedResponse<RoomCategoryResponse>>();
        Assert.Contains(inactive.Items, c => c.Id == category.Id);
        var activated = await (await api.Client.PostAsync($"{Url}/{category.Id}/activate", null)).ReadAsync<RoomCategoryResponse>();
        Assert.True(activated.IsActive);
    }

    [Fact]
    public async Task UnusedCategoryIsDeleted()
    {
        var category = await CreateCategoryAsync();

        await (await api.Client.DeleteAsync($"{Url}/{category.Id}")).AssertStatusAsync(HttpStatusCode.NoContent);

        await Problem.ReadAsync(await api.Client.GetAsync($"{Url}/{category.Id}"), HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CategoryWithRoomsIsNotDeleted()
    {
        var problem = await Problem.ReadAsync(await api.Client.DeleteAsync($"{Url}/{Seed.Standard}"), HttpStatusCode.Conflict);

        Assert.EndsWith("Деактивуйте її.", problem.Detail);
    }

    private static string UniqueName() => $"Категорія {Guid.NewGuid():N}"[..40];

    private async Task<RoomCategoryResponse> CreateCategoryAsync()
        => await (await api.Client.PostJsonAsync(Url, new { name = UniqueName(), capacity = 2 })).ReadAsync<RoomCategoryResponse>(HttpStatusCode.Created);
}
