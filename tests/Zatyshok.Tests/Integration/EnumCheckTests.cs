using Microsoft.EntityFrameworkCore;
using Npgsql;
using Zatyshok.Domain.Modules.Bookings;

namespace Zatyshok.Tests.Integration;

/// <summary>
/// Enums are stored as text (HasConversion&lt;string&gt;), so only the CHECK constraints of
/// HasEnumCheck stop a value that is not a member of the enum (12.1.3, listing П.1).
/// A violated CHECK is SQLSTATE 23514 (check_violation).
/// </summary>
[Trait("Category", "Integration")]
public class EnumCheckTests(PostgresDatabaseFixture database) : IClassFixture<PostgresDatabaseFixture>
{
    [Fact]
    public async Task UnknownRoomStatusIsRejected()
    {
        await using var db = database.CreateDbContext();
        var hotel = await TestData.CreateHotelAsync(db);

        await using var connection = await database.OpenConnectionAsync();
        var error = await PostgresAssert.SqlFailsAsync(
            connection,
            $"""UPDATE rooms."Rooms" SET "Status" = 'Broken' WHERE "Id" = '{hotel.Rooms[0].Id}'""",
            PostgresErrorCodes.CheckViolation);

        Assert.Equal("CK_Rooms_Status", error.ConstraintName);
    }

    [Fact]
    public async Task EnumValueOutsideTheMembersIsRejectedOnSave()
    {
        await using var db = database.CreateDbContext();
        var hotel = await TestData.CreateHotelAsync(db);

        // A bad cast in code, e.g. (BookingStatus)42, is stored as the text "42".
        var booking = TestData.NewBooking(hotel, hotel.Today.AddDays(3));
        db.Bookings.Add(booking);
        db.Entry(booking).Property(b => b.Status).CurrentValue = (BookingStatus)42;

        var error = await PostgresAssert.SaveFailsAsync(db, PostgresErrorCodes.CheckViolation);
        Assert.Equal("CK_Bookings_Status", error.ConstraintName);
    }

    /// <summary>
    /// Every enum property of the EF model has its CHECK in the database, and the CHECK lists
    /// every member. Fails when a new enum column is configured without HasEnumCheck, or when an
    /// enum gets a new member but no migration that recreates the constraint.
    /// </summary>
    [Fact]
    public async Task EveryEnumColumnHasACheckWithAllMembers()
    {
        await using var db = database.CreateDbContext();
        var enumColumns = db.Model.GetEntityTypes()
            .SelectMany(entity => entity.GetProperties().Select(property => (Entity: entity, Property: property)))
            .Where(x => (Nullable.GetUnderlyingType(x.Property.ClrType) ?? x.Property.ClrType).IsEnum)
            .Select(x => (
                Schema: x.Entity.GetSchema()!,
                Table: x.Entity.GetTableName()!,
                Column: x.Property.GetColumnName(),
                Members: Enum.GetNames(Nullable.GetUnderlyingType(x.Property.ClrType) ?? x.Property.ClrType)))
            .ToList();

        // The 15 enums of table 12.24, one column each.
        Assert.Equal(15, enumColumns.Count);

        await using var connection = await database.OpenConnectionAsync();
        foreach (var column in enumColumns)
        {
            var constraint = $"CK_{column.Table}_{column.Column}";
            await using var command = new NpgsqlCommand(
                """
                SELECT pg_get_constraintdef(c.oid)
                FROM pg_constraint AS c
                JOIN pg_class AS t ON t.oid = c.conrelid
                JOIN pg_namespace AS n ON n.oid = t.relnamespace
                WHERE c.contype = 'c' AND n.nspname = @schema AND t.relname = @table AND c.conname = @name
                """,
                connection);
            command.Parameters.AddWithValue("schema", column.Schema);
            command.Parameters.AddWithValue("table", column.Table);
            command.Parameters.AddWithValue("name", constraint);

            var definition = await command.ExecuteScalarAsync() as string;
            Assert.True(definition is not null, $"{column.Schema}.{column.Table}: no CHECK {constraint}");
            foreach (var member in column.Members)
            {
                Assert.True(definition.Contains($"'{member}'"), $"{constraint} does not allow '{member}': {definition}");
            }
        }
    }
}
