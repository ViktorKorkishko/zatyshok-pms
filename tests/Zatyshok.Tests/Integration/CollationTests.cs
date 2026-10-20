using Microsoft.EntityFrameworkCore;
using Npgsql;
using Zatyshok.Domain.Modules.Guests;
using Zatyshok.Infrastructure.Persistence;

namespace Zatyshok.Tests.Integration;

/// <summary>
/// The ICU collation uk-UA-x-icu for names of people and organizations (12.1.3). It comes with
/// PostgreSQL built with ICU, as in the postgres:16 image; without it the FullDomainModel migration
/// fails.
/// </summary>
[Trait("Category", "Integration")]
public class CollationTests(PostgresDatabaseFixture database) : IClassFixture<PostgresDatabaseFixture>
{
    [Fact]
    public async Task UkrainianIcuCollationIsAvailable()
    {
        await using var connection = await database.OpenConnectionAsync();
        await using var command = new NpgsqlCommand("SELECT collprovider::text FROM pg_collation WHERE collname = @name", connection);
        command.Parameters.AddWithValue("name", Collations.Ukrainian);

        // 'i' = ICU provider.
        Assert.Equal("i", await command.ExecuteScalarAsync());
    }

    [Fact]
    public async Task NameColumnsUseTheUkrainianCollation()
    {
        await using var connection = await database.OpenConnectionAsync();
        await using var command = new NpgsqlCommand(
            """
            SELECT table_schema || '.' || table_name || '.' || column_name
            FROM information_schema.columns
            WHERE collation_name = @name
            ORDER BY 1
            """,
            connection);
        command.Parameters.AddWithValue("name", Collations.Ukrainian);

        var columns = new List<string>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            columns.Add(reader.GetString(0));
        }

        Assert.Equal(
            new[]
            {
                "guests.CorporateClients.Name",
                "guests.Guests.FirstName",
                "guests.Guests.LastName",
                "guests.Guests.MiddleName",
                "identity.Users.FullName",
            },
            columns);
    }

    [Fact]
    public async Task GuestsAreSortedInUkrainianAlphabeticalOrder()
    {
        // Ґ, Є, І and Ї have character codes outside А…Я, so a byte-order ("C") sort puts them
        // before А or after Я; the Ukrainian alphabet has them right after Г, Е, И and І.
        var expected = new[] { "Андрієнко", "Гнатюк", "Ґонта", "Дорошенко", "Еллада", "Єрмоленко", "Иваничук", "Іваненко", "Їжакевич", "Яковенко" };
        var marker = TestData.Unique();

        await using (var db = database.CreateDbContext())
        {
            foreach (var lastName in expected.Reverse())
            {
                db.Guests.Add(new Guest { LastName = lastName, FirstName = "Тест", StaffNotes = marker });
            }

            await db.SaveChangesAsync();
        }

        await using var check = database.CreateDbContext();
        var sorted = await check.Guests
            .Where(g => g.StaffNotes == marker)
            .OrderBy(g => g.LastName)
            .Select(g => g.LastName)
            .ToListAsync();

        Assert.Equal(expected, sorted);
        Assert.NotEqual(expected, expected.Order(StringComparer.Ordinal));
    }
}
