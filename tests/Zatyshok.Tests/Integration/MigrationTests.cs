using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace Zatyshok.Tests.Integration;

/// <summary>
/// The migration chain InitialRooms → FullDomainModel → ReportingViewsAndAuditGuard on a database
/// with T1 data, and the rollback to InitialRooms (П.6, items 1 and 2). Regression test for the
/// generated Down methods that left the T1 tables in "rooms" and the schemas in place
/// (both fixed by hand in the migration).
/// </summary>
[Trait("Category", "Integration")]
public class MigrationTests(EmptyPostgresDatabaseFixture database) : IClassFixture<EmptyPostgresDatabaseFixture>
{
    private static readonly string[] T1Tables = ["Hotels", "Rates", "RoomCategories", "Rooms", "SeasonRates"];

    private static readonly string[] CreatedSchemas =
        ["audit", "billing", "bookings", "guests", "housekeeping", "identity", "notifications", "reporting", "rooms"];

    [Fact]
    public async Task T1DataSurvivesUpgradeAndRollbackToInitialRooms()
    {
        await using var db = database.CreateDbContext();
        var migrator = db.GetService<IMigrator>();
        await using var connection = await database.OpenConnectionAsync();

        // A T1 database with data.
        await migrator.MigrateAsync("InitialRooms");
        var hotelId = Guid.NewGuid();
        await T1Data.InsertAsync(connection, hotelId);
        var t1Snapshot = await SnapshotAsync(connection, "public");

        // Upgrade: the T1 tables move to "rooms" with their rows, the new Hotels columns get
        // their defaults and the existing rows pass the new CHECK constraints.
        await migrator.MigrateAsync();
        Assert.Equal(CreatedSchemas, await GetSchemasAsync(connection));
        await AssertFullStructureAsync(connection);

        await using (var upgraded = database.CreateDbContext())
        {
            var hotel = await upgraded.Hotels.Include(h => h.Rooms).SingleAsync(h => h.Id == hotelId);
            Assert.Equal((1, new TimeOnly(18, 0), new TimeOnly(14, 0), new TimeOnly(12, 0)),
                (hotel.CancellationDeadlineDaysBefore, hotel.CancellationDeadlineTime, hotel.CheckInTime, hotel.CheckOutTime));
            Assert.Equal("101", Assert.Single(hotel.Rooms).Number);
        }

        // Rollback: the T1 tables are back in "public" with the same rows, no other schema is left.
        await migrator.MigrateAsync("InitialRooms");
        Assert.Empty(await GetSchemasAsync(connection));
        Assert.Equal(T1Tables.Append("__EFMigrationsHistory").Order(StringComparer.Ordinal),
            await QueryListAsync(connection, "SELECT table_name FROM information_schema.tables WHERE table_schema = 'public'"));
        Assert.Equal(t1Snapshot, await SnapshotAsync(connection, "public"));

        // And the chain can be applied again.
        await migrator.MigrateAsync();
        Assert.Equal(CreatedSchemas, await GetSchemasAsync(connection));
        await using var count = new NpgsqlCommand($"""SELECT count(*) FROM rooms."Hotels" WHERE "Id" = '{hotelId}'""", connection);
        Assert.Equal(1L, await count.ExecuteScalarAsync());
    }

    /// <summary>П.6, item 2: 22 tables, 2 sequences, 2 views and 2 triggers.</summary>
    private static async Task AssertFullStructureAsync(NpgsqlConnection connection)
    {
        var tables = await QueryListAsync(connection,
            "SELECT table_name FROM information_schema.tables WHERE table_type = 'BASE TABLE' AND table_schema NOT IN ('public', 'pg_catalog', 'information_schema')");
        Assert.Equal(22, tables.Count);

        Assert.Equal(new[] { "billing.InvoiceNumbers", "bookings.BookingNumbers" },
            await QueryListAsync(connection, "SELECT sequence_schema || '.' || sequence_name FROM information_schema.sequences"));
        Assert.Equal(new[] { "reporting.v_category_availability_daily", "reporting.v_hotel_occupancy_daily" },
            await QueryListAsync(connection, "SELECT table_schema || '.' || table_name FROM information_schema.views WHERE table_schema = 'reporting'"));
        Assert.Equal(new[] { "TR_AuditLogs_AppendOnly", "TR_AuditLogs_NoTruncate" },
            await QueryListAsync(connection, """SELECT tgname FROM pg_trigger WHERE tgrelid = 'audit."AuditLogs"'::regclass AND NOT tgisinternal"""));
    }

    /// <summary>All rows of the five T1 tables of a schema as JSON text, table by table.</summary>
    private static async Task<Dictionary<string, string>> SnapshotAsync(NpgsqlConnection connection, string schema)
    {
        var snapshot = new Dictionary<string, string>();
        foreach (var table in T1Tables)
        {
            await using var command = new NpgsqlCommand(
                $"""SELECT string_agg(row_to_json(t)::text, ',' ORDER BY t."Id") FROM {schema}."{table}" AS t""",
                connection);
            snapshot[table] = (string)(await command.ExecuteScalarAsync())!;
        }

        return snapshot;
    }

    /// <summary>Schemas other than the standard ones.</summary>
    private static Task<List<string>> GetSchemasAsync(NpgsqlConnection connection)
        => QueryListAsync(connection,
            "SELECT nspname FROM pg_namespace WHERE nspname NOT IN ('public', 'information_schema') AND nspname NOT LIKE 'pg\\_%'");

    /// <summary>The first column of every row, sorted by character codes (independent of the database collation).</summary>
    private static async Task<List<string>> QueryListAsync(NpgsqlConnection connection, string sql)
    {
        var values = new List<string>();
        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            values.Add(reader.GetString(0));
        }

        values.Sort(StringComparer.Ordinal);
        return values;
    }
}
