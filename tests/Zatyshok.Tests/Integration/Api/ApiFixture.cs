using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Zatyshok.Infrastructure;
using Zatyshok.Infrastructure.Persistence;

namespace Zatyshok.Tests.Integration.Api;

/// <summary>
/// The API in a test host (WebApplicationFactory) on top of a temporary database of
/// <see cref="PostgresDatabaseFixture"/>: one database and one host per test class
/// (<c>IClassFixture&lt;ApiFixture&gt;</c>). Requests go through the whole ASP.NET Core pipeline
/// in memory, without a network port. The host runs in Development like <c>dotnet run</c>, so the
/// Development seed fills the database on start (README, «Демо-дані»): tests may read the seeded
/// rows (<see cref="Seed"/>), but each test changes only the rows it creates, because the tests of
/// a class share the database.
/// </summary>
public class ApiFixture : PostgresDatabaseFixture
{
    private WebApplicationFactory<Program>? _factory;

    /// <summary>Client of the host; created in <see cref="InitializeAsync"/>.</summary>
    public HttpClient Client { get; private set; } = null!;

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();

        _factory = new ZatyshokApiFactory(ConnectionString);

        // Creating the first client starts the host: Program runs and seeds the database.
        Client = _factory.CreateClient();
    }

    /// <summary>
    /// A client of another host on the same database, with some services replaced, e.g. the hotel
    /// scope of a user of one hotel. The host is disposed together with this fixture.
    /// </summary>
    public HttpClient CreateClient(Action<IServiceCollection> replaceServices)
        => _factory!
            .WithWebHostBuilder(builder => builder.ConfigureTestServices(replaceServices))
            .CreateClient();

    public override async Task DisposeAsync()
    {
        // The host first: its pooled connections would keep the database open.
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }

        await base.DisposeAsync();
    }

    private sealed class ZatyshokApiFactory(string connectionString) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");

            // The SQL of every command and the warning that a test host has no HTTPS port would
            // fill the test output; the API's own records stay.
            builder.ConfigureLogging(logging => logging
                .AddFilter("Microsoft.EntityFrameworkCore", LogLevel.Warning)
                .AddFilter("Microsoft.AspNetCore.HttpsPolicy", LogLevel.Error));

            // The context of Program reads ConnectionStrings:Default and DB_PASSWORD, and a
            // developer's user-secrets (README, step 4) would override a password set here through
            // configuration. Replacing the options after Program's registrations avoids both.
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<DbContextOptions<ZatyshokDbContext>>();
                services.AddDbContext<ZatyshokDbContext>(options => options.UseZatyshokNpgsql(connectionString));
            });
        }
    }
}
