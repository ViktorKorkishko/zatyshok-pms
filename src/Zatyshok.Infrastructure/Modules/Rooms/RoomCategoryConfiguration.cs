using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zatyshok.Domain.Modules.Rooms;

namespace Zatyshok.Infrastructure.Modules.Rooms;

public class RoomCategoryConfiguration : IEntityTypeConfiguration<RoomCategory>
{
    public void Configure(EntityTypeBuilder<RoomCategory> builder)
    {
        builder.Property(c => c.Name).HasMaxLength(100);
        builder.Property(c => c.Description).HasMaxLength(1000);

        // Categories are a network-wide dictionary, so the name must be unique.
        builder.HasIndex(c => c.Name).IsUnique();
    }
}
