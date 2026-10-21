using System.Globalization;
using System.Net;
using Zatyshok.Api.Common.Paging;
using Zatyshok.Api.Modules.Rooms;

namespace Zatyshok.Tests.Integration.Api;

/// <summary>/api/v1/services (FR-05, UC-11).</summary>
[Trait("Category", "Integration")]
public class ServicesApiTests(ApiFixture api) : IClassFixture<ApiFixture>
{
    private const string Url = "/api/v1/services";

    [Fact]
    public async Task ListContainsTheSeededServices()
    {
        var page = await (await api.Client.GetAsync($"{Url}?pageSize=100")).ReadAsync<PagedResponse<ServiceResponse>>();

        Assert.Contains(page.Items, s => s.Id == Seed.Breakfast && s.Name == "Сніданок" && s.Price == 350m);
    }

    [Fact]
    public async Task CreatedServiceCanBeReadAndChanged()
    {
        var response = await api.Client.PostJsonAsync(Url, new { name = UniqueName(), description = "За кілограм", price = 150.5m });
        var created = await response.ReadAsync<ServiceResponse>(HttpStatusCode.Created);

        var updated = await (await api.Client.PutJsonAsync($"{Url}/{created.Id}", new { name = created.Name, price = 0m })).ReadAsync<ServiceResponse>();

        Assert.Equal(150.5m, created.Price);
        Assert.Equal("За кілограм", created.Description);
        var read = await (await api.Client.GetAsync(response.Headers.Location)).ReadAsync<ServiceResponse>();
        Assert.Equal(0m, read.Price);
        Assert.Null(read.Description);
        Assert.Equal(updated.Price, read.Price);
    }

    [Theory]
    [InlineData("-1", "Ціна не може бути від'ємною.")]
    [InlineData("1.234", "Ціна має бути з точністю до копійки (не більше двох знаків після коми).")]
    [InlineData("10000000000", "Ціна не може перевищувати 9 999 999 999,99 грн.")]
    public async Task InvalidPriceGivesValidationProblem(string price, string message)
    {
        // Attributes cannot hold decimal values, hence the text.
        var response = await api.Client.PostJsonAsync(Url, new { name = UniqueName(), price = decimal.Parse(price, CultureInfo.InvariantCulture) });

        var problem = await Problem.ReadAsync(response, HttpStatusCode.BadRequest);
        Assert.Equal(message, Assert.Single(problem.Errors!["price"]));
    }

    [Fact]
    public async Task MissingPriceGivesValidationProblem()
    {
        // 0 is a valid price, so a missing price must not silently become a free service.
        var response = await api.Client.PostJsonAsync(Url, new { name = UniqueName() });

        var problem = await Problem.ReadAsync(response, HttpStatusCode.BadRequest);
        Assert.Equal("Вкажіть ціну послуги.", Assert.Single(problem.Errors!["price"]));
    }

    [Fact]
    public async Task DuplicateNameGives409()
    {
        var response = await api.Client.PostJsonAsync(Url, new { name = "Сніданок", price = 1m });

        var problem = await Problem.ReadAsync(response, HttpStatusCode.Conflict);
        Assert.Equal("Послуга «Сніданок» уже існує.", problem.Detail);
    }

    [Fact]
    public async Task DeactivatedServiceIsListedAsInactive()
    {
        var service = await CreateServiceAsync();

        var deactivated = await (await api.Client.PostAsync($"{Url}/{service.Id}/deactivate", null)).ReadAsync<ServiceResponse>();

        Assert.False(deactivated.IsActive);
        var active = await (await api.Client.GetAsync($"{Url}?isActive=true&pageSize=100")).ReadAsync<PagedResponse<ServiceResponse>>();
        Assert.DoesNotContain(active.Items, s => s.Id == service.Id);
    }

    [Fact]
    public async Task UnusedServiceIsDeleted()
    {
        var service = await CreateServiceAsync();

        await (await api.Client.DeleteAsync($"{Url}/{service.Id}")).AssertStatusAsync(HttpStatusCode.NoContent);

        var problem = await Problem.ReadAsync(await api.Client.DeleteAsync($"{Url}/{service.Id}"), HttpStatusCode.NotFound);
        Assert.Equal("Послугу не знайдено.", problem.Detail);
    }

    private static string UniqueName() => $"Послуга {Guid.NewGuid():N}";

    private async Task<ServiceResponse> CreateServiceAsync()
        => await (await api.Client.PostJsonAsync(Url, new { name = UniqueName(), price = 100m })).ReadAsync<ServiceResponse>(HttpStatusCode.Created);
}
