using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Zatyshok.Infrastructure.Persistence;

namespace Zatyshok.Tests.Integration.Api;

/// <summary>Ids of rows of the Development seed, which the API host creates on start (README, «Демо-дані»).</summary>
internal static class Seed
{
    public static readonly Guid Kyiv = DevelopmentDataSeeder.KyivId;
    public static readonly Guid Lviv = DevelopmentDataSeeder.LvivId;
    public static readonly Guid Odesa = DevelopmentDataSeeder.OdesaId;
    public static readonly Guid Karpaty = DevelopmentDataSeeder.KarpatyId;

    public static readonly Guid Standard = DevelopmentDataSeeder.StandardId;
    public static readonly Guid Superior = DevelopmentDataSeeder.SuperiorId;
    public static readonly Guid Suite = DevelopmentDataSeeder.SuiteId;

    /// <summary>Room 101 (Standard) of «Затишок Київ»: hotel index 0, room index 0.</summary>
    public static readonly Guid KyivRoom101 = DevelopmentDataSeeder.RoomId(0, 0);

    /// <summary>Rate Standard of «Затишок Київ», 2500 UAH per night.</summary>
    public static readonly Guid KyivStandardRate = DevelopmentDataSeeder.RateId(0, 0);

    public static readonly Guid Breakfast = DevelopmentDataSeeder.ServiceId(1);
}

/// <summary>JSON as the API writes it: camelCase names and enums as strings.</summary>
internal static class ApiJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public static Task<HttpResponseMessage> PostJsonAsync(this HttpClient client, string url, object body)
        => client.PostAsJsonAsync(url, body, Options);

    public static Task<HttpResponseMessage> PutJsonAsync(this HttpClient client, string url, object body)
        => client.PutAsJsonAsync(url, body, Options);

    /// <summary>Checks the status code and reads the body; on another status the message shows the body.</summary>
    public static async Task<T> ReadAsync<T>(this HttpResponseMessage response, HttpStatusCode expected = HttpStatusCode.OK)
    {
        await AssertStatusAsync(response, expected);
        return (await response.Content.ReadFromJsonAsync<T>(Options))!;
    }

    public static async Task AssertStatusAsync(this HttpResponseMessage response, HttpStatusCode expected)
    {
        if (response.StatusCode != expected)
        {
            Assert.Fail($"Expected {(int)expected} {expected}, got {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        }
    }
}

/// <summary>A ProblemDetails body (RFC 9457) as the API returns it for every error.</summary>
internal sealed record Problem(
    string? Type,
    string? Title,
    int? Status,
    string? Detail,
    string? CorrelationId,
    Dictionary<string, string[]>? Errors)
{
    /// <summary>
    /// Checks that the response is a ProblemDetails with the expected status, the media type of
    /// RFC 9457 and a Ukrainian title (NFR-13), and returns it.
    /// </summary>
    public static async Task<Problem> ReadAsync(HttpResponseMessage response, HttpStatusCode expected)
    {
        await response.AssertStatusAsync(expected);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var problem = (await response.Content.ReadFromJsonAsync<Problem>(ApiJson.Options))!;
        Assert.Equal((int)expected, problem.Status);
        Assert.Matches("^[А-ЯІЇЄҐ]", problem.Title);
        Assert.False(string.IsNullOrEmpty(problem.CorrelationId));
        return problem;
    }
}
