using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zatyshok.Domain.Modules.Bookings;
using Zatyshok.Domain.Modules.Identity;
using Zatyshok.Infrastructure.Persistence;

namespace Zatyshok.Infrastructure.Modules.Bookings;

public class BookingHistoryConfiguration : IEntityTypeConfiguration<BookingHistory>
{
    public void Configure(EntityTypeBuilder<BookingHistory> builder)
    {
        builder.ToTable("BookingHistory", Schemas.Bookings, t =>
        {
            t.HasEnumCheck<BookingHistory, BookingHistoryEventType>("EventType");
            t.HasCheckConstraint(
                "CK_BookingHistory_FieldName",
                "\"EventType\" NOT IN ('FieldChanged', 'StatusChanged') OR \"FieldName\" IS NOT NULL");
        });

        builder.Property(h => h.EventType)
            .HasConversion<string>()
            .HasMaxLength(32);

        builder.Property(h => h.FieldName).HasMaxLength(100);
        builder.Property(h => h.OldValue).HasMaxLength(1000);
        builder.Property(h => h.NewValue).HasMaxLength(1000);
        builder.Property(h => h.Comment).HasMaxLength(500);

        // Chronology in the booking card (UC-17); also serves the BookingId foreign key.
        builder.HasIndex(h => new { h.BookingId, h.OccurredAtUtc });

        // History is never deleted (12.8.5), so the booking cannot be deleted while it has records.
        builder.HasOne(h => h.Booking)
            .WithMany(b => b.History)
            .HasForeignKey(h => h.BookingId)
            .OnDelete(DeleteBehavior.Restrict);

        // User belongs to the Identity module: only a foreign key (9.4.1, rule 2). NULL = the system.
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(h => h.AuthorUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
