using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Zatyshok.Infrastructure.Persistence;

namespace Zatyshok.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddZatyshokInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = BuildConnectionString(configuration);

        // An invoice is always read with two collections, Items and Payments (AutoInclude in
        // InvoiceConfiguration). EF then warns on every such query until the query splitting is set
        // explicitly. SingleQuery is EF's default anyway: one round trip and one consistent snapshot,
        // and an invoice has only tens of lines, so the joined result stays small.
        services.AddDbContext<ZatyshokDbContext>(options => options.UseNpgsql(
            connectionString,
            npgsql => npgsql.UseQuerySplittingBehavior(QuerySplittingBehavior.SingleQuery)));
        return services;
    }

    private static string BuildConnectionString(IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException(
                "Connection string 'Default' is not configured (appsettings.json).");

        var builder = new NpgsqlConnectionStringBuilder(connectionString);

        // The password is intentionally absent from appsettings.json; it comes from
        // user-secrets or the DB_PASSWORD environment variable (see README).
        var password = configuration["DB_PASSWORD"];
        if (!string.IsNullOrEmpty(password))
        {
            builder.Password = password;
        }

        return builder.ConnectionString;
    }
}
