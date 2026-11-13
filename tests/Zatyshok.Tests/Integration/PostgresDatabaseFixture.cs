using Microsoft.EntityFrameworkCore;
using Npgsql;
using Zatyshok.Infrastructure.Persistence;

namespace Zatyshok.Tests.Integration;

/// <summary>
/// A temporary database for one test class (<c>IClassFixture&lt;PostgresDatabaseFixture&gt;</c>):
/// created on <see cref="TestPostgresServer"/> before the first test of the class, migrated with
/// all EF Core migrations and dropped after the last test. Tests of one class run one after
/// another and share the database, so each test creates its own hotel, rooms and guests.
/// </summary>
public class PostgresDatabaseFixture : IAsyncLifetime
{
    public string DatabaseName { get; } = $"zatyshok_test_{Guid.NewGuid():N}";

    /// <summary>Superuser connection to the temporary database.</summary>
    public string ConnectionString { get; private set; } = string.Empty;

    /// <summary>False for tests that apply the migrations themselves (MigrationTests).</summary>
    protected virtual bool ApplyMigrations => true;

    public ZatyshokDbContext CreateDbContext() => new(
        new DbContextOptionsBuilder<ZatyshokDbContext>()
            .UseNpgsql(ConnectionString)
            .Options);

    /// <summary>A plain ADO.NET connection for SQL that EF does not model (triggers, views, catalogs).</summary>
    public async Task<NpgsqlConnection> OpenConnectionAsync()
    {
        var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        return connection;
    }

    public async Task InitializeAsync()
    {
        await TestPostgresServer.ExecuteAsync($"CREATE DATABASE \"{DatabaseName}\"");
        ConnectionString = await TestPostgresServer.GetConnectionStringAsync(DatabaseName);

        if (ApplyMigrations)
        {
            await using var db = CreateDbContext();
            await db.Database.MigrateAsync();
        }
    }

    public async Task DisposeAsync()
    {
        // The database was not created (e.g. no server): nothing to clean up.
        if (ConnectionString.Length == 0)
        {
            return;
        }

        // Idle pooled connections would keep the database open; FORCE also ends any other session.
        NpgsqlConnection.ClearPool(new NpgsqlConnection(ConnectionString));
        await TestPostgresServer.ExecuteAsync($"DROP DATABASE IF EXISTS \"{DatabaseName}\" WITH (FORCE)");
    }
}

/// <summary>An empty temporary database without migrations.</summary>
public class EmptyPostgresDatabaseFixture : PostgresDatabaseFixture
{
    protected override bool ApplyMigrations => false;
}
