using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zatyshok.Domain.Modules.Bookings;
using Zatyshok.Infrastructure.Persistence;

namespace Zatyshok.Infrastructure.Modules.Bookings;

public class BookingNightConfiguration : IEntityTypeConfiguration<BookingNight>
{
    public void Configure(EntityTypeBuilder<BookingNight> builder)
    {
        builder.ToTable("BookingNights", Schemas.Bookings, t =>
            t.HasCheckConstraint("CK_BookingNights_Price", "\"Price\" >= 0"));

        // A night has no Id of its own: the key is (booking, date), one row per night (12.1.2).
        builder.HasKey(n => new { n.BookingId, n.Night });

        builder.Property(n => n.Price).HasPrecision(12, 2);

        // Revenue and occupancy reports by date (FR-35).
        builder.HasIndex(n => n.Night);

        // Nights are part of the booking: a night removed from Booking.Nights is deleted.
        builder.HasOne(n => n.Booking)
            .WithMany(b => b.Nights)
            .HasForeignKey(n => n.BookingId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
