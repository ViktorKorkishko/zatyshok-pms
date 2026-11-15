using Npgsql;
using Testcontainers.PostgreSql;

namespace Zatyshok.Tests.Integration;

/// <summary>
/// The PostgreSQL server that integration tests create their temporary databases on, shared by
/// all test classes of one <c>dotnet test</c> run:
/// <list type="bullet">
/// <item>if the environment variable <c>ZATYSHOK_TEST_PG</c> is set, it is a connection string to an
/// existing server with superuser rights and without a database name, for example
/// <c>Host=localhost;Port=5432;Username=postgres;Password=postgres</c>;</item>
/// <item>otherwise a <c>postgres:16</c> container is started through Testcontainers (Docker Desktop
/// must be running). Testcontainers removes the container when the test process exits.</item>
/// </list>
/// </summary>
internal static class TestPostgresServer
{
    public const string EnvironmentVariable = "ZATYSHOK_TEST_PG";

    // The same major version as in docker-compose.yml (report 2, 12.1.3).
    private const string Image = "postgres:16";

    // Started once for the whole run: test classes run in parallel, and Lazy lets only the first
    // one start the container while the others wait for the same task (a failure is kept too).
    private static readonly Lazy<Task<string>> AdminConnectionString = new(ResolveAsync);

    // Kept in a field so that the container lives until the end of the test run.
    private static PostgreSqlContainer? _container;

    /// <summary>Connection string of a superuser to the maintenance database "postgres".</summary>
    public static Task<string> GetAdminConnectionStringAsync() => AdminConnectionString.Value;

    /// <summary>Connection string to another database of the same server with the same superuser.</summary>
    public static async Task<string> GetConnectionStringAsync(string database)
        => new NpgsqlConnectionStringBuilder(await GetAdminConnectionStringAsync()) { Database = database }.ConnectionString;

    /// <summary>Runs SQL in the maintenance database, e.g. CREATE DATABASE (it cannot run inside a transaction).</summary>
    public static async Task ExecuteAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(await GetAdminConnectionStringAsync());
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<string> ResolveAsync()
    {
        var fromEnvironment = Environment.GetEnvironmentVariable(EnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(fromEnvironment))
        {
            return new NpgsqlConnectionStringBuilder(fromEnvironment) { Database = "postgres" }.ConnectionString;
        }

        try
        {
            // Build() already fails when Docker is not running.
            _container = new PostgreSqlBuilder(Image).Build();
            await _container.StartAsync();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Integration tests need PostgreSQL: start Docker Desktop (a {Image} container is created) "
                + $"or set {EnvironmentVariable} to a superuser connection string without a database name. "
                + "Unit tests only: dotnet test --filter \"Category!=Integration\".",
                ex);
        }

        // The container's default user "postgres" is a superuser; its database is "postgres".
        return _container.GetConnectionString();
    }
}
