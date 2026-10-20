using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Zatyshok.Domain.Modules.Rooms;

namespace Zatyshok.Infrastructure.Persistence;

/// <summary>
/// Single database context of the modular monolith. Entity configurations live in
/// Infrastructure/Modules/&lt;Name&gt; next to the module they belong to.
/// </summary>
public class ZatyshokDbContext(DbContextOptions<ZatyshokDbContext> options) : DbContext(options)
{
    public DbSet<Hotel> Hotels => Set<Hotel>();

    public DbSet<RoomCategory> RoomCategories => Set<RoomCategory>();

    public DbSet<Room> Rooms => Set<Room>();

    public DbSet<Rate> Rates => Set<Rate>();

    public DbSet<SeasonRate> SeasonRates => Set<SeasonRate>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ZatyshokDbContext).Assembly);

        // Ids are created in code (Guid.NewGuid() in the entity initializer). By convention EF
        // treats Guid keys as generated on add and takes a non-empty key for an already saved row,
        // so a child added through a navigation (hotel.Rooms.Add(room)) would be saved as an UPDATE.
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var id = entityType.FindProperty("Id");
            if (id?.ClrType == typeof(Guid))
            {
                id.ValueGenerated = ValueGenerated.Never;
            }
        }
    }
}
