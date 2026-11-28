using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zatyshok.Domain.Modules.Rooms;
using Zatyshok.Infrastructure.Persistence;

namespace Zatyshok.Infrastructure.Modules.Rooms;

public class ServiceConfiguration : IEntityTypeConfiguration<Service>
{
    public void Configure(EntityTypeBuilder<Service> builder)
    {
        builder.ToTable("Services", Schemas.Rooms, t =>
            t.HasCheckConstraint("CK_Services_Price", "\"Price\" >= 0"));

        builder.Property(s => s.Name).HasMaxLength(200);
        builder.Property(s => s.Description).HasMaxLength(1000);
        builder.Property(s => s.Price).HasPrecision(12, 2);

        builder.HasIndex(s => s.Name).IsUnique();
    }
}
