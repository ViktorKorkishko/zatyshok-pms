using System.Net;

namespace Zatyshok.Tests.Integration.Api;

/// <summary>What every request goes through: health check, ProblemDetails for errors, correlation id.</summary>
[Trait("Category", "Integration")]
public class ApiPipelineTests(ApiFixture api) : IClassFixture<ApiFixture>
{
    [Fact]
    public async Task HealthIsHealthyWithTheTestDatabase()
    {
        var response = await api.Client.GetAsync("/health");

        await response.AssertStatusAsync(HttpStatusCode.OK);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task UnknownPathGivesProblemDetailsInUkrainian()
    {
        var response = await api.Client.GetAsync("/api/v1/no-such-resource");

        var problem = await Problem.ReadAsync(response, HttpStatusCode.NotFound);
        Assert.Equal("Не знайдено", problem.Title);
    }

    [Fact]
    public async Task CorrelationIdOfTheClientIsReturnedInTheHeaderAndInTheError()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/no-such-resource");
        request.Headers.Add("X-Correlation-Id", "test-42");

        var response = await api.Client.SendAsync(request);

        Assert.Equal("test-42", Assert.Single(response.Headers.GetValues("X-Correlation-Id")));
        Assert.Equal("test-42", (await Problem.ReadAsync(response, HttpStatusCode.NotFound)).CorrelationId);
    }

    [Fact]
    public async Task RequestWithoutCorrelationIdGetsANewOne()
    {
        var first = await api.Client.GetAsync("/health");
        var second = await api.Client.GetAsync("/health");

        var firstId = Assert.Single(first.Headers.GetValues("X-Correlation-Id"));
        Assert.True(Guid.TryParse(firstId, out _));
        Assert.NotEqual(firstId, Assert.Single(second.Headers.GetValues("X-Correlation-Id")));
    }

    [Theory]
    [InlineData("contains spaces")]
    [InlineData("a-very-long-correlation-id-that-is-longer-than-sixty-four-characters-in-total")]
    public async Task UnsafeCorrelationIdIsReplaced(string unsafeId)
    {
        // The id goes into logs and the audit log, so text of any kind is not taken from the client.
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        request.Headers.TryAddWithoutValidation("X-Correlation-Id", unsafeId);

        var response = await api.Client.SendAsync(request);

        var returned = Assert.Single(response.Headers.GetValues("X-Correlation-Id"));
        Assert.NotEqual(unsafeId, returned);
        Assert.True(Guid.TryParse(returned, out _));
    }
}
