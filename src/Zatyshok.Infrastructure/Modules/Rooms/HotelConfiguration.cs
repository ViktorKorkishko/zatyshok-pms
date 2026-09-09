using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zatyshok.Domain.Modules.Rooms;

namespace Zatyshok.Infrastructure.Modules.Rooms;

public class HotelConfiguration : IEntityTypeConfiguration<Hotel>
{
    public void Configure(EntityTypeBuilder<Hotel> builder)
    {
        builder.Property(h => h.Name).HasMaxLength(200);
        builder.Property(h => h.City).HasMaxLength(100);
        builder.Property(h => h.Address).HasMaxLength(300);
        builder.Property(h => h.TimeZoneId).HasMaxLength(64);
        builder.Property(h => h.Phone).HasMaxLength(32);
        builder.Property(h => h.Email).HasMaxLength(256);

        // Availability search filters by city (FR-10).
        builder.HasIndex(h => h.City);
    }
}
