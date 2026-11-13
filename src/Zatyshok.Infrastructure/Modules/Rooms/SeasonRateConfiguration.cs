using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zatyshok.Domain.Modules.Rooms;
using Zatyshok.Infrastructure.Persistence;

namespace Zatyshok.Infrastructure.Modules.Rooms;

public class SeasonRateConfiguration : IEntityTypeConfiguration<SeasonRate>
{
    public void Configure(EntityTypeBuilder<SeasonRate> builder)
    {
        builder.ToTable("SeasonRates", Schemas.Rooms, t =>
        {
            // EndDate is the last night of the period (inclusive), so a one-night period is allowed.
            t.HasCheckConstraint("CK_SeasonRates_Period", "\"EndDate\" >= \"StartDate\"");
            t.HasCheckConstraint("CK_SeasonRates_PricePerNight", "\"PricePerNight\" > 0");
        });

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
