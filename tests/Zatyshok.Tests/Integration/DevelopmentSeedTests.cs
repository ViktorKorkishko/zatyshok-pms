using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Zatyshok.Domain.Modules.Identity;
using Zatyshok.Infrastructure.Persistence;

namespace Zatyshok.Tests.Integration;

/// <summary>The Development seed on a freshly migrated database (12.8.5): contents and idempotency.</summary>
[Trait("Category", "Integration")]
public class DevelopmentSeedTests(PostgresDatabaseFixture database) : IClassFixture<PostgresDatabaseFixture>
{
    [Fact]
    public async Task SeedFillsAnEmptyDatabaseOnceAndKeepsItOnTheNextStart()
    {
        await using (var db = database.CreateDbContext())
        {
            await DevelopmentDataSeeder.SeedAsync(db, NullLogger.Instance);
        }

        var first = await CountAsync();
        Assert.Equal(new SeedCounts(5, 3, 25, 15, 2, 3, 9, 9, 3, 1), first);

        // The API seeds on every Development start; a second run adds nothing.
        await using (var db = database.CreateDbContext())
        {
            await DevelopmentDataSeeder.SeedAsync(db, NullLogger.Instance);
        }

        Assert.Equal(first, await CountAsync());
    }

    [Fact]
    public async Task EveryRoleHasOneSeededUserWhoCannotSignInBeforeT4()
    {
        await using (var db = database.CreateDbContext())
        {
            await DevelopmentDataSeeder.SeedAsync(db, NullLogger.Instance);
        }

        await using var check = database.CreateDbContext();
        var users = await check.Users.Include(u => u.Role).ToListAsync();

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
        Assert.True(await check.Guests.AnyAsync(g => g.UserId == guestUser.Id));
    }

    private async Task<SeedCounts> CountAsync()
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

    private sealed record SeedCounts(
        int Hotels, int Categories, int Rooms, int Rates, int SeasonRates,
        int Services, int Roles, int Users, int Guests, int CorporateClients);
}
