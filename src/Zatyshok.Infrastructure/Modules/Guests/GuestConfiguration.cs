using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zatyshok.Domain.Modules.Guests;
using Zatyshok.Domain.Modules.Identity;
using Zatyshok.Infrastructure.Persistence;

namespace Zatyshok.Infrastructure.Modules.Guests;

public class GuestConfiguration : IEntityTypeConfiguration<Guest>
{
    public void Configure(EntityTypeBuilder<Guest> builder)
    {
        builder.ToTable("Guests", Schemas.Guests, t =>
        {
            t.HasEnumCheck<Guest, DocumentType>("DocumentType");
            t.HasCheckConstraint("CK_Guests_Document", "(\"DocumentType\" IS NULL) = (\"DocumentNumber\" IS NULL)");
        });

        // Names are searched and sorted, so they use the Ukrainian collation (12.1.3).
        builder.Property(g => g.LastName).HasMaxLength(100).UseCollation(Collations.Ukrainian);
        builder.Property(g => g.FirstName).HasMaxLength(100).UseCollation(Collations.Ukrainian);
        builder.Property(g => g.MiddleName).HasMaxLength(100).UseCollation(Collations.Ukrainian);

        // ISO 3166-1 alpha-2 code: char(2).
        builder.Property(g => g.Citizenship).HasMaxLength(2).IsFixedLength();

        builder.Property(g => g.DocumentType)
            .HasConversion<string>()
            .HasMaxLength(32);

        builder.Property(g => g.DocumentNumber).HasMaxLength(32);
        builder.Property(g => g.Phone).HasMaxLength(32);
        builder.Property(g => g.Email).HasMaxLength(256);
        builder.Property(g => g.Preferences).HasMaxLength(1000);
        builder.Property(g => g.StaffNotes).HasMaxLength(2000);

        // Guest search (FR-24).
        builder.HasIndex(g => g.LastName);
        builder.HasIndex(g => g.Phone);
        builder.HasIndex(g => g.Email);
        builder.HasIndex(g => g.DocumentNumber);

        // One profile per portal account. User belongs to the Identity module: only a foreign key,
        // no navigation (9.4.1, rule 2). For a one-to-one link EF creates the unique index
        // IX_Guests_UserId; several NULLs do not conflict in PostgreSQL.
        builder.HasOne<User>()
            .WithOne()
            .HasForeignKey<Guest>(g => g.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
