using Zatyshok.Infrastructure.Persistence;

namespace Zatyshok.Tests.Persistence;

/// <summary>
/// The seed builds ids from hotel and room indexes. Without a fixed width, a hotel with 10 or
/// more rooms gave a 13-digit last group (Guid.Parse failed) and (1, 12) collided with (11, 2).
/// </summary>
public class DevelopmentSeedIdTests
{
    [Fact]
    public void RoomIdsAreValidAndUniqueForTwoDigitIndexes()
    {
        var ids = new HashSet<Guid>();
        for (var hotel = 0; hotel < 12; hotel++)
        {
            for (var room = 0; room < 12; room++)
            {
                Assert.True(ids.Add(DevelopmentDataSeeder.RoomId(hotel, room)), $"duplicate id for ({hotel}, {room})");
            }
        }
    }

    [Fact]
    public void RateIdsAreValidAndUniqueForTwoDigitIndexes()
    {
        var ids = new HashSet<Guid>();
        for (var hotel = 0; hotel < 12; hotel++)
        {
            for (var category = 0; category < 12; category++)
            {
                Assert.True(ids.Add(DevelopmentDataSeeder.RateId(hotel, category)), $"duplicate id for ({hotel}, {category})");
            }
        }
    }

    [Fact]
    public void UserServiceAndGuestIdsAreValidAndUniqueForTwoDigitNumbers()
    {
        var ids = new HashSet<Guid>();
        for (var number = 1; number < 100; number++)
        {
            Assert.True(ids.Add(DevelopmentDataSeeder.UserId(number)), $"duplicate user id for {number}");
            Assert.True(ids.Add(DevelopmentDataSeeder.ServiceId(number)), $"duplicate service id for {number}");
            Assert.True(ids.Add(DevelopmentDataSeeder.GuestId(number)), $"duplicate guest id for {number}");
        }
    }
}
