using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zatyshok.Domain.Modules.Bookings;
using Zatyshok.Domain.Modules.Guests;
using Zatyshok.Domain.Modules.Rooms;
using Zatyshok.Infrastructure.Persistence;

namespace Zatyshok.Infrastructure.Modules.Bookings;

public class BookingConfiguration : IEntityTypeConfiguration<Booking>
{
    // Statuses in which a booking counts against availability and holds its room (FR-09).
    private const string ActiveFilter = "\"Status\" IN ('Confirmed', 'CheckedIn')";

    public void Configure(EntityTypeBuilder<Booking> builder)
    {
        builder.ToTable("Bookings", Schemas.Bookings, t =>
        {
            t.HasEnumCheck<Booking, BookingStatus>("Status");
            t.HasEnumCheck<Booking, BookingSource>("Source");
            t.HasCheckConstraint("CK_Bookings_Dates", "\"CheckOutDate\" > \"CheckInDate\"");
            t.HasCheckConstraint("CK_Bookings_GuestsCount", "\"GuestsCount\" >= 1");
            t.HasCheckConstraint("CK_Bookings_TotalPrice", "\"TotalPrice\" >= 0");

            // A checked-in or completed booking has a room and the matching timestamps (FR-19, FR-20).
            t.HasCheckConstraint("CK_Bookings_RoomForStay", "\"Status\" NOT IN ('CheckedIn', 'Completed') OR \"RoomId\" IS NOT NULL");
            t.HasCheckConstraint("CK_Bookings_CheckedInAt", "(\"Status\" IN ('CheckedIn', 'Completed')) = (\"CheckedInAtUtc\" IS NOT NULL)");
            t.HasCheckConstraint("CK_Bookings_CheckedOutAt", "(\"Status\" = 'Completed') = (\"CheckedOutAtUtc\" IS NOT NULL)");

            // Recommended in 12.3.3: a new, cancelled or no-show booking holds no room. The domain
            // already clears RoomId on Cancel/MarkNoShow; the database now guarantees it too.
            t.HasCheckConstraint("CK_Bookings_NoRoomWhenInactive", "\"Status\" NOT IN ('New', 'Cancelled', 'NoShow') OR \"RoomId\" IS NULL");
        });

        // The number shown to the guest comes from a sequence (starts at 100001, see
        // ZatyshokDbContext). EF leaves the column out of INSERT and reads the value back.
        builder.Property(b => b.Number).HasDefaultValueSql("nextval('bookings.\"BookingNumbers\"')");

        builder.Property(b => b.Status)
            .HasConversion<string>()
            .HasMaxLength(32);

        builder.Property(b => b.Source)
            .HasConversion<string>()
            .HasMaxLength(32);

        builder.Property(b => b.TotalPrice).HasPrecision(12, 2);
        builder.Property(b => b.Wishes).HasMaxLength(1000);
        builder.Property(b => b.CancellationReason).HasMaxLength(500);

        // xmin as the concurrency token (see RoomConfiguration).
        builder.Property(b => b.Version).IsRowVersion();

        builder.HasIndex(b => b.Number).IsUnique();

        // A room cannot belong to two active bookings at once (NFR-04). The partial index also
        // serves the RoomId foreign key, so EF creates no separate IX_Bookings_RoomId (12.3.3).
        builder.HasIndex(b => b.RoomId)
            .IsUnique()
            .HasFilter(ActiveFilter)
            .HasDatabaseName("UX_Bookings_RoomId_Active");

        // Bookings that overlap the requested period, for the availability check (listing 12.1).
        builder.HasIndex(b => new { b.HotelId, b.RoomCategoryId, b.CheckInDate, b.CheckOutDate })
            .HasFilter(ActiveFilter)
            .HasDatabaseName("IX_Bookings_Availability");

        // Lists and expected arrivals of a hotel (FR-18, FR-22); no-show search by the Worker (FR-15).
        builder.HasIndex(b => new { b.HotelId, b.CheckInDate });
        builder.HasIndex(b => new { b.Status, b.CheckInDate });

        // Hotel, category, room, guest and corporate client belong to other modules: only foreign
        // keys, no navigations (9.4.1, rule 2). EF adds indexes for GuestId, RoomCategoryId and
        // CorporateClientId; HotelId is covered by the indexes above.
        builder.HasOne<Hotel>().WithMany().HasForeignKey(b => b.HotelId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<RoomCategory>().WithMany().HasForeignKey(b => b.RoomCategoryId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Room>().WithMany().HasForeignKey(b => b.RoomId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Guest>().WithMany().HasForeignKey(b => b.GuestId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CorporateClient>().WithMany().HasForeignKey(b => b.CorporateClientId).OnDelete(DeleteBehavior.Restrict);

        // Nights and History are read-only collections over the private fields _nights and _history;
        // EF finds the fields by name and fills them (relationships: BookingNightConfiguration,
        // BookingHistoryConfiguration).
    }
}
