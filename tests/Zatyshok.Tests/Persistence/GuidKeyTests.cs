using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Zatyshok.Domain.Modules.Rooms;
using Zatyshok.Infrastructure.Persistence;

namespace Zatyshok.Tests.Persistence;

/// <summary>
/// Ids are created in code (<c>Guid.NewGuid()</c> in entity initializers), so EF must never
/// treat a Guid key as store-generated.
/// </summary>
public class GuidKeyTests
{
    // Building the model and tracking changes need no database connection.
    private static ZatyshokDbContext CreateContext()
        => new(new DbContextOptionsBuilder<ZatyshokDbContext>()
            .UseNpgsql("Host=localhost;Database=unused")
            .Options);

    [Fact]
    public void GuidIdsAreNotGeneratedByEf()
    {
        using var db = CreateContext();

        var generated = db.Model.GetEntityTypes()
            .Select(e => e.FindProperty("Id"))
            .Where(id => id?.ClrType == typeof(Guid) && id.ValueGenerated != ValueGenerated.Never)
            .Select(id => id!.DeclaringType.DisplayName())
            .ToList();

        Assert.Empty(generated);
    }

    [Fact]
    public void ChildAddedThroughNavigationIsTrackedAsAdded()
    {
        using var db = CreateContext();
        var hotel = new Hotel { Name = "Test", City = "Київ", Address = "вул. Тестова, 1", TimeZoneId = "Europe/Kyiv" };
        db.Hotels.Attach(hotel);

        var room = new Room { Number = "101", RoomCategoryId = Guid.NewGuid() };
        hotel.Rooms.Add(room);
        db.ChangeTracker.DetectChanges();

        Assert.Equal(EntityState.Added, db.Entry(room).State);
    }
}
