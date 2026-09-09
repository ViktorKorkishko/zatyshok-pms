using Microsoft.EntityFrameworkCore;
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
    }
}
