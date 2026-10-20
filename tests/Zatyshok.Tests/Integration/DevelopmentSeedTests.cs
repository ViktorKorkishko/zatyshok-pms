using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Logging.Abstractions;
using Zatyshok.Domain.Modules.Identity;
using Zatyshok.Infrastructure.Persistence;

namespace Zatyshok.Tests.Integration;

/// <summary>The Development seed on a freshly migrated database (12.8.5): contents and idempotency.</summary>
[Trait("Category", "Integration")]
public class DevelopmentSeedTests(PostgresDatabaseFixture database) : IClassFixture<PostgresDatabaseFixture>
{
    // One test for the whole class: its tests share one database, and only the first seed
    // starts from empty tables. A second test would depend on which of them runs first.
    [Fact]
    public async Task SeedFillsAnEmptyDatabaseOnceAndKeepsItOnTheNextStart()
    {
        await using (var db = database.CreateDbContext())
        {
            await DevelopmentDataSeeder.SeedAsync(db, NullLogger.Instance);
        }

        var first = await SeedCounts.ReadAsync(database);
        Assert.Equal(new SeedCounts(5, 3, 25, 15, 2, 3, 9, 9, 3, 1), first);
        await AssertEveryRoleHasOneUserWhoCannotSignInBeforeT4Async();

        // The API seeds on every Development start; a second run adds nothing.
        await using (var db = database.CreateDbContext())
        {
            await DevelopmentDataSeeder.SeedAsync(db, NullLogger.Instance);
        }

        Assert.Equal(first, await SeedCounts.ReadAsync(database));
    }

    private async Task AssertEveryRoleHasOneUserWhoCannotSignInBeforeT4Async()
    {
        await using var db = database.CreateDbContext();
        var users = await db.Users.Include(u => u.Role).ToListAsync();

        Assert.Equal(
            new[]
            {
                RoleCodes.Accountant, RoleCodes.Administrator, RoleCodes.Executive, RoleCodes.Guest, RoleCodes.HeadHousekeeper,
                RoleCodes.HotelManager, RoleCodes.Housekeeper, RoleCodes.ReservationAgent, RoleCodes.Receptionist,
            }.Order(StringComparer.Ordinal),
            users.Select(u => u.Role.Code).Order(StringComparer.Ordinal));
        Assert.All(users, u => Assert.Equal(DevelopmentDataSeeder.UnusablePasswordHash, u.PasswordHash));
        Assert.All(users, u => Assert.Equal(UserStatus.Active, u.Status));

        // Hotel-bound roles work in one hotel, the others see the whole network (FR-02).
        Assert.All(users, u => Assert.Equal(u.Role.IsHotelBound, u.HotelId is not null));

        // The guest account has its guest profile.
        var guestUser = users.Single(u => u.Role.Code == RoleCodes.Guest);
        Assert.True(await db.Guests.AnyAsync(g => g.UserId == guestUser.Id));
    }
}

/// <summary>
/// The Development seed on a database seeded in T1 and then migrated: the hotels part is
/// skipped, services and people are added (README, «Демо-дані»).
/// </summary>
[Trait("Category", "Integration")]
public class DevelopmentSeedOnT1DatabaseTests(EmptyPostgresDatabaseFixture database) : IClassFixture<EmptyPostgresDatabaseFixture>
{
    // «Затишок Київ» had this id already in the T1 seed; the seeded staff are bound to it.
    private static readonly Guid KyivId = Guid.Parse("22222222-2222-2222-2222-222222222201");

    [Fact]
    public async Task SeedAddsServicesAndPeopleButKeepsTheT1Hotels()
    {
        await using (var db = database.CreateDbContext())
        {
            var migrator = db.GetService<IMigrator>();
            await migrator.MigrateAsync("InitialRooms");
            await using (var connection = await database.OpenConnectionAsync())
            {
                await T1Data.InsertAsync(connection, KyivId);
            }

            await migrator.MigrateAsync();
        }

        await using (var db = database.CreateDbContext())
        {
            await DevelopmentDataSeeder.SeedAsync(db, NullLogger.Instance);
        }

        // The single T1 hotel with its room and rates stays as it was.
        Assert.Equal(new SeedCounts(1, 1, 1, 1, 1, 3, 9, 9, 3, 1), await SeedCounts.ReadAsync(database));

        await using var check = database.CreateDbContext();
        var hotelIds = await check.Users.Where(u => u.HotelId != null).Select(u => u.HotelId).Distinct().ToListAsync();
        Assert.Equal(KyivId, Assert.Single(hotelIds));
    }
}

/// <summary>Row counts of the tables the Development seed fills.</summary>
internal sealed record SeedCounts(
    int Hotels, int Categories, int Rooms, int Rates, int SeasonRates,
    int Services, int Roles, int Users, int Guests, int CorporateClients)
{
    public static async Task<SeedCounts> ReadAsync(PostgresDatabaseFixture database)
    {
        await using var db = database.CreateDbContext();
        return new SeedCounts(
            await db.Hotels.CountAsync(),
            await db.RoomCategories.CountAsync(),
            await db.Rooms.CountAsync(),
            await db.Rates.CountAsync(),
            await db.SeasonRates.CountAsync(),
            await db.Services.CountAsync(),
            await db.Roles.CountAsync(),
            await db.Users.CountAsync(),
            await db.Guests.CountAsync(),
            await db.CorporateClients.CountAsync());
    }
}
