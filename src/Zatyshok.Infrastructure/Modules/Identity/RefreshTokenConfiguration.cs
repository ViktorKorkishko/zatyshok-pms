using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zatyshok.Domain.Modules.Identity;
using Zatyshok.Infrastructure.Persistence;

namespace Zatyshok.Infrastructure.Modules.Identity;

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("RefreshTokens", Schemas.Identity, t =>
            t.HasCheckConstraint("CK_RefreshTokens_Expiry", "\"ExpiresAtUtc\" <= \"SessionExpiresAtUtc\""));

        // Only a SHA-256 hash (64 hex characters) is stored, never the token itself.
        builder.Property(t => t.TokenHash).HasMaxLength(64);

        builder.HasIndex(t => t.TokenHash).IsUnique();
        builder.HasIndex(t => t.SessionId);

        // Tokens are part of the account: deleting a user deletes them (12.8.1).
        builder.HasOne(t => t.User)
            .WithMany(u => u.RefreshTokens)
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
