using Microsoft.EntityFrameworkCore;
using Npgsql;
using Zatyshok.Domain.Modules.Audit;
using Zatyshok.Infrastructure.Persistence;

namespace Zatyshok.Tests.Integration;

/// <summary>
/// The append-only trigger of audit."AuditLogs" (listing 12.5, NFR-10). The trigger works for
/// every role, the superuser included, until someone disables it, so these tests run as the
/// superuser. What the application role may do is checked in <see cref="DatabaseRolesTests"/>.
/// The trigger raises an exception, SQLSTATE P0001 (raise_exception).
/// </summary>
[Trait("Category", "Integration")]
public class AuditLogTriggerTests(PostgresDatabaseFixture database) : IClassFixture<PostgresDatabaseFixture>
{
    [Fact]
    public async Task UpdateOfAnAuditRecordIsRejected()
    {
        await using var db = database.CreateDbContext();
        var record = await AddRecordAsync(db, DateTime.UtcNow);

        record.UserName = "інше ім'я";
        var error = await PostgresAssert.SaveFailsAsync(db, PostgresErrorCodes.RaiseException);

        Assert.Contains("Журнал аудиту незмінний", error.MessageText);
    }

    [Fact]
    public async Task UpdateIsRejectedEvenForARecordOlderThanThreeYears()
    {
        await using var db = database.CreateDbContext();
        var record = await AddRecordAsync(db, DateTime.UtcNow.AddYears(-4));

        record.UserName = "інше ім'я";
        await PostgresAssert.SaveFailsAsync(db, PostgresErrorCodes.RaiseException);
    }

    [Fact]
    public async Task DeleteOfAFreshAuditRecordIsRejected()
    {
        await using var db = database.CreateDbContext();
        var record = await AddRecordAsync(db, DateTime.UtcNow.AddYears(-2));

        db.AuditLogs.Remove(record);
        await PostgresAssert.SaveFailsAsync(db, PostgresErrorCodes.RaiseException);
    }

    [Fact]
    public async Task RecordOlderThanThreeYearsCanBeDeleted()
    {
        await using var db = database.CreateDbContext();
        var record = await AddRecordAsync(db, DateTime.UtcNow.AddYears(-3).AddDays(-1));

        db.AuditLogs.Remove(record);
        await db.SaveChangesAsync();

        await using var check = database.CreateDbContext();
        Assert.False(await check.AuditLogs.AnyAsync(a => a.Id == record.Id));
    }

    [Fact]
    public async Task TruncateIsRejected()
    {
        await using (var db = database.CreateDbContext())
        {
            await AddRecordAsync(db, DateTime.UtcNow);
        }

        await using var connection = await database.OpenConnectionAsync();
        await PostgresAssert.SqlFailsAsync(connection, """TRUNCATE audit."AuditLogs";""", PostgresErrorCodes.RaiseException);
    }

    private static async Task<AuditLog> AddRecordAsync(ZatyshokDbContext db, DateTime occurredAtUtc)
    {
        var record = new AuditLog
        {
            OccurredAtUtc = occurredAtUtc,
            UserName = "admin@zatyshok.ua",
            Action = AuditAction.Update,
            EntityType = "Room",
            EntityId = Guid.NewGuid(),
            Changes = """{"Status": {"old": "Free", "new": "OutOfOrder"}}""",
        };
        db.AuditLogs.Add(record);
        await db.SaveChangesAsync();
        return record;
    }
}
