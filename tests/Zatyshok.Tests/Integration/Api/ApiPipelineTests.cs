using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Zatyshok.Api.Common.Errors;

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

    [Fact]
    public async Task SwaggerDescribesEveryEndpointWithItsRolesMediaTypesAndErrorExamples()
    {
        var response = await api.Client.GetAsync("/swagger/v1/swagger.json");
        await response.AssertStatusAsync(HttpStatusCode.OK);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        var operations = document.RootElement.GetProperty("paths").EnumerateObject()
            .SelectMany(path => path.Value.EnumerateObject().Select(operation => (Name: $"{operation.Name.ToUpperInvariant()} {path.Name}", operation.Value)))
            .ToList();

        // Every action of every controller is in the document.
        var actions = typeof(Program).Assembly.GetTypes()
            .Where(t => typeof(ControllerBase).IsAssignableFrom(t))
            .SelectMany(t => t.GetMethods())
            .Count(m => m.GetCustomAttributes(typeof(HttpMethodAttribute), inherit: true).Length > 0);
        Assert.Equal(actions, operations.Count);
        Assert.Contains(operations, o => o.Name == "POST /api/v1/rooms/{id}/out-of-order");
        foreach (var (name, operation) in operations)
        {
            Assert.False(string.IsNullOrWhiteSpace(operation.GetProperty("summary").GetString()), $"{name}: no summary");
            Assert.Contains("**Ролі (перевірка з T4):**", operation.GetProperty("description").GetString());
            foreach (var (code, content) in operation.GetProperty("responses").EnumerateObject()
                         .Where(r => r.Value.TryGetProperty("content", out _))
                         .Select(r => (r.Name, r.Value.GetProperty("content"))))
            {
                var expected = code.StartsWith('2') ? "application/json" : "application/problem+json";
                Assert.Equal([expected], content.EnumerateObject().Select(c => c.Name));

                // An error has an example of the real ProblemDetails, with the Ukrainian title of its code.
                if (!code.StartsWith('2'))
                {
                    Assert.True(content.GetProperty(expected).TryGetProperty("example", out var example), $"{name} {code}: no example");
                    Assert.Equal(ProblemDetailsSetup.TitleOf(int.Parse(code)), example.GetProperty("title").GetString());
                    Assert.Equal(int.Parse(code), example.GetProperty("status").GetInt32());
                    Assert.False(string.IsNullOrWhiteSpace(example.GetProperty("detail").GetString()), $"{name} {code}: no detail");
                }
            }
        }
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
