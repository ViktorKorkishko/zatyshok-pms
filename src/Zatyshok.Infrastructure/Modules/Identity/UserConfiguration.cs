using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zatyshok.Domain.Modules.Identity;
using Zatyshok.Domain.Modules.Rooms;
using Zatyshok.Infrastructure.Persistence;

namespace Zatyshok.Infrastructure.Modules.Identity;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users", Schemas.Identity, t =>
        {
            // Emails are stored in lower case, so the unique index ignores case (FR-01, UC-01 A1).
            t.HasCheckConstraint("CK_Users_EmailLowerCase", "\"Email\" = lower(\"Email\")");
            t.HasEnumCheck<User, UserStatus>("Status");
            t.HasCheckConstraint("CK_Users_FailedLoginCount", "\"FailedLoginCount\" >= 0");
        });

        builder.Property(u => u.Email).HasMaxLength(256);
        builder.Property(u => u.PasswordHash).HasMaxLength(256);
        builder.Property(u => u.FullName).HasMaxLength(300).UseCollation(Collations.Ukrainian);

        builder.Property(u => u.Status)
            .HasConversion<string>()
            .HasMaxLength(32);

        // xmin as the concurrency token (see RoomConfiguration).
        builder.Property(u => u.Version).IsRowVersion();

        builder.HasIndex(u => u.Email).IsUnique();

        builder.HasOne(u => u.Role)
            .WithMany()
            .HasForeignKey(u => u.RoleId)
            .OnDelete(DeleteBehavior.Restrict);

        // Hotel belongs to the Rooms module: only a foreign key, no navigation (9.4.1, rule 2).
        builder.HasOne<Hotel>()
            .WithMany()
            .HasForeignKey(u => u.HotelId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
