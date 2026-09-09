using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zatyshok.Domain.Modules.Rooms;

namespace Zatyshok.Infrastructure.Modules.Rooms;

public class SeasonRateConfiguration : IEntityTypeConfiguration<SeasonRate>
{
    public void Configure(EntityTypeBuilder<SeasonRate> builder)
    {
        builder.Property(s => s.Name).HasMaxLength(200);
        builder.Property(s => s.PricePerNight).HasPrecision(12, 2);

        // Price of a night is looked up by date within one rate. Overlap of periods
        // is validated by the rates service (T3), not by a database constraint.
        builder.HasIndex(s => new { s.RateId, s.StartDate });

        builder.HasOne(s => s.Rate)
            .WithMany(r => r.SeasonRates)
            .HasForeignKey(s => s.RateId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
