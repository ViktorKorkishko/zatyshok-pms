using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Zatyshok.Domain.Modules.Billing;
using Zatyshok.Domain.Modules.Bookings;
using Zatyshok.Domain.Modules.Guests;
using Zatyshok.Domain.Modules.Housekeeping;
using Zatyshok.Domain.Modules.Identity;
using Zatyshok.Domain.Modules.Rooms;

namespace Zatyshok.Infrastructure.Persistence;

/// <summary>
/// Single database context of the modular monolith. Entity configurations live in
/// Infrastructure/Modules/&lt;Name&gt; next to the module they belong to; each module keeps its
/// tables in its own PostgreSQL schema (<see cref="Schemas"/>, report 2, table 12.1).
/// </summary>
public class ZatyshokDbContext(DbContextOptions<ZatyshokDbContext> options) : DbContext(options)
{
    public DbSet<Hotel> Hotels => Set<Hotel>();

    public DbSet<RoomCategory> RoomCategories => Set<RoomCategory>();

    public DbSet<Room> Rooms => Set<Room>();

    public DbSet<Rate> Rates => Set<Rate>();

    public DbSet<SeasonRate> SeasonRates => Set<SeasonRate>();

    public DbSet<Service> Services => Set<Service>();

    public DbSet<Role> Roles => Set<Role>();

    public DbSet<User> Users => Set<User>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public DbSet<AccountToken> AccountTokens => Set<AccountToken>();

    public DbSet<Guest> Guests => Set<Guest>();

    public DbSet<CorporateClient> CorporateClients => Set<CorporateClient>();

    public DbSet<Booking> Bookings => Set<Booking>();

    public DbSet<BookingNight> BookingNights => Set<BookingNight>();

    public DbSet<BookingHistory> BookingHistory => Set<BookingHistory>();

    public DbSet<HousekeepingTask> HousekeepingTasks => Set<HousekeepingTask>();

    public DbSet<MaintenanceIssue> MaintenanceIssues => Set<MaintenanceIssue>();

    public DbSet<Invoice> Invoices => Set<Invoice>();

    public DbSet<InvoiceItem> InvoiceItems => Set<InvoiceItem>();

    public DbSet<Payment> Payments => Set<Payment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Booking and invoice numbers for people (UC-04, FR-34): the columns take nextval(...)
        // as their DEFAULT (BookingConfiguration, InvoiceConfiguration).
        modelBuilder.HasSequence<long>("BookingNumbers", Schemas.Bookings).StartsAt(100001);
        modelBuilder.HasSequence<long>("InvoiceNumbers", Schemas.Billing).StartsAt(100001);

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
