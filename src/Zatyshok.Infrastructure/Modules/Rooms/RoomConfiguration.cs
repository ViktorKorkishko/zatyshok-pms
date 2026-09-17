using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zatyshok.Domain.Modules.Rooms;

namespace Zatyshok.Infrastructure.Modules.Rooms;

public class RoomConfiguration : IEntityTypeConfiguration<Room>
{
    public void Configure(EntityTypeBuilder<Room> builder)
    {
        builder.Property(r => r.Number).HasMaxLength(16);
        builder.Property(r => r.Amenities).HasMaxLength(1000);
        builder.Property(r => r.Notes).HasMaxLength(2000);

        builder.Property(r => r.Status)
            .HasConversion<string>()
            .HasMaxLength(32);

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
