using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zatyshok.Domain.Modules.Rooms;
using Zatyshok.Infrastructure.Persistence;

namespace Zatyshok.Infrastructure.Modules.Rooms;

public class RoomConfiguration : IEntityTypeConfiguration<Room>
{
    public void Configure(EntityTypeBuilder<Room> builder)
    {
        builder.ToTable("Rooms", Schemas.Rooms, t =>
        {
            t.HasCheckConstraint("CK_Rooms_Capacity", "\"Capacity\" > 0");
            t.HasEnumCheck<Room, RoomStatus>("Status");
        });

        builder.Property(r => r.Number).HasMaxLength(16);
        builder.Property(r => r.Amenities).HasMaxLength(1000);
        builder.Property(r => r.Notes).HasMaxLength(2000);

        builder.Property(r => r.Status)
            .HasConversion<string>()
            .HasMaxLength(32);

        // Optimistic concurrency: Version is the PostgreSQL system column xmin, so reception and
        // housekeeping changing the same room at once get a conflict (HTTP 409) instead of a lost
        // update. EF adds "WHERE xmin = <value read>" to the UPDATE of the row. The column is never
        // created: every PostgreSQL table already has it, so Npgsql generates no SQL for the xmin
        // lines of the migrations.
        builder.Property(r => r.Version).IsRowVersion();

        // FR-06: the door number is unique within one hotel.
        builder.HasIndex(r => new { r.HotelId, r.Number }).IsUnique();

        // Availability is computed per (hotel, category) (FR-09).
        builder.HasIndex(r => new { r.HotelId, r.RoomCategoryId });

        builder.HasOne(r => r.Hotel)
            .WithMany(h => h.Rooms)
            .HasForeignKey(r => r.HotelId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.Category)
            .WithMany()
            .HasForeignKey(r => r.RoomCategoryId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
