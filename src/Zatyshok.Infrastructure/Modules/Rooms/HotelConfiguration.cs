using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zatyshok.Domain.Modules.Rooms;
using Zatyshok.Infrastructure.Persistence;

namespace Zatyshok.Infrastructure.Modules.Rooms;

public class HotelConfiguration : IEntityTypeConfiguration<Hotel>
{
    public void Configure(EntityTypeBuilder<Hotel> builder)
    {
        builder.ToTable("Hotels", Schemas.Rooms, t =>
        {
            t.HasCheckConstraint("CK_Hotels_Stars", "\"Stars\" BETWEEN 1 AND 5");
            t.HasCheckConstraint("CK_Hotels_CancellationDeadlineDaysBefore", "\"CancellationDeadlineDaysBefore\" >= 0");
        });

        builder.Property(h => h.Name).HasMaxLength(200);
        builder.Property(h => h.City).HasMaxLength(100);
        builder.Property(h => h.Address).HasMaxLength(300);
        builder.Property(h => h.TimeZoneId).HasMaxLength(64);
        builder.Property(h => h.Phone).HasMaxLength(32);
        builder.Property(h => h.Email).HasMaxLength(256);

        // Hotel rules (R-08). The DEFAULT is needed so that the migration can add these NOT NULL
        // columns to a table that already has hotels (table 12.6). ValueGeneratedNever makes EF
        // always send the value from the entity: otherwise EF would skip a value equal to the CLR
        // default (0 days, 00:00) on INSERT, and the database would silently put 1 day or 18:00 instead.
        builder.Property(h => h.CancellationDeadlineDaysBefore).HasDefaultValue(1).ValueGeneratedNever();
        builder.Property(h => h.CancellationDeadlineTime).HasDefaultValue(new TimeOnly(18, 0)).ValueGeneratedNever();
        builder.Property(h => h.CheckInTime).HasDefaultValue(new TimeOnly(14, 0)).ValueGeneratedNever();
        builder.Property(h => h.CheckOutTime).HasDefaultValue(new TimeOnly(12, 0)).ValueGeneratedNever();

        // Availability search filters by city (FR-10).
        builder.HasIndex(h => h.City);
    }
}
