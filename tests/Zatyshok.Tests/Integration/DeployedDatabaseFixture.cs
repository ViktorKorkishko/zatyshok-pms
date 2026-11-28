using Microsoft.EntityFrameworkCore;
using Npgsql;
using Zatyshok.Infrastructure.Persistence;

namespace Zatyshok.Tests.Integration;

/// <summary>
/// A temporary database set up like a deployed environment, in the order described at the top of
/// deploy/sql/roles-and-grants.sql (12.8.4):
/// <list type="number">
/// <item>the superuser creates the owner role and a database owned by it;</item>
/// <item>the migrations are applied under the owner;</item>
/// <item>the superuser runs deploy/sql/roles-and-grants.sql;</item>
/// <item>the application role gets a password.</item>
/// </list>
/// Roles are shared by all databases of a server, so on a developer's server (ZATYSHOK_TEST_PG)
/// real zatyshok_owner/zatyshok_app roles may exist. The test therefore runs the script with the
/// role names replaced by unique ones and drops those roles afterwards.
/// </summary>
public sealed class DeployedDatabaseFixture : IAsyncLifetime
{
    private readonly string _suffix = Guid.NewGuid().ToString("N")[..12];
    private readonly string _ownerPassword = Guid.NewGuid().ToString("N");
    private readonly string _appPassword = Guid.NewGuid().ToString("N");
    private bool _rolesCreated;

    public DeployedDatabaseFixture()
    {
        DatabaseName = $"zatyshok_test_{_suffix}";
        OwnerRole = $"zatyshok_test_{_suffix}_owner";
        AppRole = $"zatyshok_test_{_suffix}_app";
    }

    public string DatabaseName { get; }

    /// <summary>Plays zatyshok_owner: owns the database and the schemas.</summary>
    public string OwnerRole { get; }

    /// <summary>Plays zatyshok_app: the role the API and the Worker connect under.</summary>
    public string AppRole { get; }

    public string SuperuserConnectionString { get; private set; } = string.Empty;

    public string OwnerConnectionString { get; private set; } = string.Empty;

    public string AppConnectionString { get; private set; } = string.Empty;

    public static async Task<NpgsqlConnection> OpenAsync(string connectionString)
    {
        var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        return connection;
    }

    public async Task InitializeAsync()
    {
        // 1. Owner role and its database.
        await TestPostgresServer.ExecuteAsync($"CREATE ROLE \"{OwnerRole}\" LOGIN PASSWORD '{_ownerPassword}'");
        _rolesCreated = true;
        await TestPostgresServer.ExecuteAsync($"CREATE DATABASE \"{DatabaseName}\" OWNER \"{OwnerRole}\"");

        SuperuserConnectionString = await TestPostgresServer.GetConnectionStringAsync(DatabaseName);
        OwnerConnectionString = WithUser(SuperuserConnectionString, OwnerRole, _ownerPassword);
        AppConnectionString = WithUser(SuperuserConnectionString, AppRole, _appPassword);

        // 2. Migrations under the owner, so the owner owns every table, view and sequence.
        await using (var db = new ZatyshokDbContext(
                         new DbContextOptionsBuilder<ZatyshokDbContext>().UseNpgsql(OwnerConnectionString).Options))
        {
            await db.Database.MigrateAsync();
        }

        // 3. The grants script, as a superuser. It has no psql meta-commands, so Npgsql runs it as is.
        var script = (await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "deploy", "sql", "roles-and-grants.sql")))
            .Replace("zatyshok_owner", OwnerRole)
            .Replace("zatyshok_app", AppRole);

        await using (var connection = await OpenAsync(SuperuserConnectionString))
        {
            await using var command = new NpgsqlCommand(script, connection);
            await command.ExecuteNonQueryAsync();

            // 4. The script creates the application role without a password (it comes from secrets).
            await using var password = new NpgsqlCommand($"ALTER ROLE \"{AppRole}\" PASSWORD '{_appPassword}'", connection);
            await password.ExecuteNonQueryAsync();
        }
    }

    public async Task DisposeAsync()
    {
        // The server was not reachable: nothing was created.
        if (!_rolesCreated)
        {
            return;
        }

        foreach (var connectionString in new[] { SuperuserConnectionString, OwnerConnectionString, AppConnectionString })
        {
            if (connectionString.Length > 0)
            {
                NpgsqlConnection.ClearPool(new NpgsqlConnection(connectionString));
            }
        }

        // Privileges of the roles live in the database itself, so after DROP DATABASE nothing
        // depends on the roles and they can be dropped.
        await TestPostgresServer.ExecuteAsync($"DROP DATABASE IF EXISTS \"{DatabaseName}\" WITH (FORCE)");
        await TestPostgresServer.ExecuteAsync($"DROP ROLE IF EXISTS \"{AppRole}\"");
        await TestPostgresServer.ExecuteAsync($"DROP ROLE IF EXISTS \"{OwnerRole}\"");
    }

    private static string WithUser(string connectionString, string user, string password)
        => new NpgsqlConnectionStringBuilder(connectionString) { Username = user, Password = password }.ConnectionString;
}
