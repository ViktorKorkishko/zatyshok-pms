using Microsoft.EntityFrameworkCore;
using Npgsql;
using Zatyshok.Domain.Modules.Rooms;
using Zatyshok.Infrastructure.Persistence;

namespace Zatyshok.Tests.Integration;

/// <summary>
/// Rights of the application role after deploy/sql/roles-and-grants.sql (12.8.4, listing 12.6):
/// DML on the module tables, only SELECT, INSERT and DELETE on the audit log (DELETE is limited by
/// the trigger), reading the reporting views, and the same rights on tables that the owner creates
/// after the script. A missing privilege is SQLSTATE 42501 (insufficient_privilege).
/// </summary>
[Trait("Category", "Integration")]
public class DatabaseRolesTests(DeployedDatabaseFixture database) : IClassFixture<DeployedDatabaseFixture>
{
    [Fact]
    public async Task AppRoleCanDeleteAuditRecordOlderThanThreeYears()
    {
        await using var app = await DeployedDatabaseFixture.OpenAsync(database.AppConnectionString);
        var id = await InsertAuditRecordAsync(app, DateTime.UtcNow.AddYears(-4));

        // The cleanup job of the Worker (table 12.33).
        Assert.Equal(1, await ExecuteAsync(app, $"""DELETE FROM audit."AuditLogs" WHERE "Id" = '{id}'"""));
    }

    [Fact]
    public async Task AppRoleCannotDeleteFreshAuditRecord()
    {
        await using var app = await DeployedDatabaseFixture.OpenAsync(database.AppConnectionString);
        var id = await InsertAuditRecordAsync(app, DateTime.UtcNow);

        // The role has the DELETE privilege, so the trigger is what stops it.
        await PostgresAssert.SqlFailsAsync(app, $"""DELETE FROM audit."AuditLogs" WHERE "Id" = '{id}'""", PostgresErrorCodes.RaiseException);
    }

    [Fact]
    public async Task AppRoleCannotUpdateTruncateOrUnprotectAuditLog()
    {
        await using var app = await DeployedDatabaseFixture.OpenAsync(database.AppConnectionString);
        var id = await InsertAuditRecordAsync(app, DateTime.UtcNow.AddYears(-4));

        await PostgresAssert.SqlFailsAsync(app, $"""UPDATE audit."AuditLogs" SET "UserName" = 'x' WHERE "Id" = '{id}'""", PostgresErrorCodes.InsufficientPrivilege);
        await PostgresAssert.SqlFailsAsync(app, """TRUNCATE audit."AuditLogs" """, PostgresErrorCodes.InsufficientPrivilege);

        // Only the owner can switch the trigger off ("must be owner of table AuditLogs").
        await PostgresAssert.SqlFailsAsync(app, """ALTER TABLE audit."AuditLogs" DISABLE TRIGGER "TR_AuditLogs_AppendOnly" """, PostgresErrorCodes.InsufficientPrivilege);
    }

    [Fact]
    public async Task AppRoleWorksWithModuleTablesSequencesAndViews()
    {
        // EF Core as the application role: INSERT and SELECT on a module table.
        await using (var db = new ZatyshokDbContext(
                         new DbContextOptionsBuilder<ZatyshokDbContext>().UseNpgsql(database.AppConnectionString).Options))
        {
            var service = new Service { Name = $"Послуга {TestData.Unique()}", Price = 100m };
            db.Services.Add(service);
            await db.SaveChangesAsync();
            Assert.True(await db.Services.AnyAsync(s => s.Id == service.Id));
        }

        await using var app = await DeployedDatabaseFixture.OpenAsync(database.AppConnectionString);

        // nextval() is the DEFAULT of Bookings.Number and Invoices.Number.
        await ExecuteAsync(app, """SELECT nextval('bookings."BookingNumbers"'), nextval('billing."InvoiceNumbers"')""");

        // The views run with the owner's rights; SELECT on the view is enough.
        await ExecuteAsync(app, "SELECT count(*) FROM reporting.v_category_availability_daily");
        await ExecuteAsync(app, "SELECT count(*) FROM reporting.v_hotel_occupancy_daily");
    }

    [Fact]
    public async Task TableThatOwnerCreatesAfterGrantsIsAvailableToAppRole()
    {
        // A later migration, applied by the owner after the script was run.
        var billingTable = $"billing.\"Later_{TestData.Unique()}\"";
        var auditTable = $"audit.\"Later_{TestData.Unique()}\"";
        await using (var owner = await DeployedDatabaseFixture.OpenAsync(database.OwnerConnectionString))
        {
            await ExecuteAsync(owner, $"""CREATE TABLE {billingTable} ("Id" integer PRIMARY KEY, "Note" text)""");
            await ExecuteAsync(owner, $"""CREATE TABLE {auditTable} ("Id" integer PRIMARY KEY, "Note" text)""");
        }

        // ALTER DEFAULT PRIVILEGES gave the application role full DML in the module schema ...
        await using var app = await DeployedDatabaseFixture.OpenAsync(database.AppConnectionString);
        Assert.Equal(1, await ExecuteAsync(app, $"""INSERT INTO {billingTable} VALUES (1, 'a')"""));
        Assert.Equal(1, await ExecuteAsync(app, $"""UPDATE {billingTable} SET "Note" = 'b' WHERE "Id" = 1"""));
        Assert.Equal(1, await ExecuteAsync(app, $"""DELETE FROM {billingTable} WHERE "Id" = 1"""));

        // ... and only SELECT and INSERT in the audit schema.
        Assert.Equal(1, await ExecuteAsync(app, $"""INSERT INTO {auditTable} VALUES (1, 'a')"""));
        await PostgresAssert.SqlFailsAsync(app, $"""UPDATE {auditTable} SET "Note" = 'b' WHERE "Id" = 1""", PostgresErrorCodes.InsufficientPrivilege);
    }

    private static async Task<Guid> InsertAuditRecordAsync(NpgsqlConnection connection, DateTime occurredAtUtc)
    {
        var id = Guid.NewGuid();
        await using var command = new NpgsqlCommand(
            """
            INSERT INTO audit."AuditLogs" ("Id", "OccurredAtUtc", "Action", "EntityType", "EntityId")
            VALUES (@id, @occurredAt, 'Update', 'Room', @entityId)
            """,
            connection);
        command.Parameters.AddWithValue("id", id);
        command.Parameters.AddWithValue("occurredAt", occurredAtUtc);
        command.Parameters.AddWithValue("entityId", Guid.NewGuid());
        await command.ExecuteNonQueryAsync();
        return id;
    }

    private static async Task<int> ExecuteAsync(NpgsqlConnection connection, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        return await command.ExecuteNonQueryAsync();
    }
}
