using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zatyshok.Domain.Modules.Rooms;

namespace Zatyshok.Infrastructure.Modules.Rooms;

public class RateConfiguration : IEntityTypeConfiguration<Rate>
{
    public void Configure(EntityTypeBuilder<Rate> builder)
    {
        builder.Property(r => r.BasePricePerNight).HasPrecision(12, 2);

        // FR-08: exactly one rate per (hotel, category) pair.
        builder.HasIndex(r => new { r.HotelId, r.RoomCategoryId }).IsUnique();

        builder.HasOne(r => r.Hotel)
            .WithMany(h => h.Rates)
            .HasForeignKey(r => r.HotelId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.Category)
            .WithMany()
            .HasForeignKey(r => r.RoomCategoryId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
