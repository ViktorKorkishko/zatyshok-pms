using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zatyshok.Domain.Modules.Identity;
using Zatyshok.Infrastructure.Persistence;

namespace Zatyshok.Infrastructure.Modules.Identity;

public class AccountTokenConfiguration : IEntityTypeConfiguration<AccountToken>
{
    public void Configure(EntityTypeBuilder<AccountToken> builder)
    {
        builder.ToTable("AccountTokens", Schemas.Identity, t =>
        {
            t.HasEnumCheck<AccountToken, AccountTokenPurpose>("Purpose");
            t.HasCheckConstraint("CK_AccountTokens_Expiry", "\"ExpiresAtUtc\" > \"CreatedAtUtc\"");
        });

        builder.Property(t => t.Purpose)
            .HasConversion<string>()
            .HasMaxLength(32);

        builder.Property(t => t.TokenHash).HasMaxLength(64);

        builder.HasIndex(t => t.TokenHash).IsUnique();

        // Also serves the foreign key, so EF creates no separate index on UserId.
        builder.HasIndex(t => new { t.UserId, t.Purpose });

        builder.HasOne(t => t.User)
            .WithMany()
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
