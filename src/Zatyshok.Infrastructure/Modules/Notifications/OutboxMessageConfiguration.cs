using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zatyshok.Domain.Modules.Notifications;
using Zatyshok.Infrastructure.Persistence;

namespace Zatyshok.Infrastructure.Modules.Notifications;

public class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("OutboxMessages", Schemas.Notifications, t =>
        {
            t.HasEnumCheck<OutboxMessage, OutboxMessageStatus>("Status");
            t.HasCheckConstraint("CK_OutboxMessages_AttemptCount", "\"AttemptCount\" >= 0");
            t.HasCheckConstraint("CK_OutboxMessages_SentAt", "(\"Status\" = 'Sent') = (\"SentAtUtc\" IS NOT NULL)");
        });

        builder.Property(m => m.Type).HasMaxLength(100);
        builder.Property(m => m.Recipient).HasMaxLength(256);
        builder.Property(m => m.Subject).HasMaxLength(300);

        // Body has no length limit: a string without HasMaxLength becomes PostgreSQL "text".

        builder.Property(m => m.Status)
            .HasConversion<string>()
            .HasMaxLength(32);

        builder.Property(m => m.LastError).HasMaxLength(OutboxMessage.MaxErrorLength);
        builder.Property(m => m.RelatedEntityType).HasMaxLength(100);
        builder.Property(m => m.DeduplicationKey).HasMaxLength(200);

        // The Worker's queue query (WHERE "Status" = 'Pending' ORDER BY "NextAttemptAtUtc", 12.8.2)
        // reads only this small partial index, not the sent messages.
        builder.HasIndex(m => m.NextAttemptAtUtc)
            .HasFilter("\"Status\" = 'Pending'")
            .HasDatabaseName("IX_OutboxMessages_Pending");

        // The same reminder is never queued twice (FR-40); several NULLs do not conflict.
        builder.HasIndex(m => m.DeduplicationKey).IsUnique();

        // "Confirmation sent" mark in the booking card (UC-20). No foreign key: a letter may be
        // about a booking, an account or something else (12.2).
        builder.HasIndex(m => new { m.RelatedEntityType, m.RelatedEntityId });
    }
}
