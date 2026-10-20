using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zatyshok.Domain.Modules.Billing;
using Zatyshok.Domain.Modules.Bookings;
using Zatyshok.Domain.Modules.Guests;
using Zatyshok.Domain.Modules.Identity;
using Zatyshok.Infrastructure.Persistence;

namespace Zatyshok.Infrastructure.Modules.Billing;

public class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
{
    public void Configure(EntityTypeBuilder<Invoice> builder)
    {
        builder.ToTable("Invoices", Schemas.Billing, t =>
        {
            t.HasEnumCheck<Invoice, InvoiceStatus>("Status");
            t.HasCheckConstraint("CK_Invoices_VoidedAt", "(\"Status\" = 'Voided') = (\"VoidedAtUtc\" IS NOT NULL)");
            t.HasCheckConstraint(
                "CK_Invoices_VoidDetails",
                "\"Status\" <> 'Voided' OR (\"VoidedByUserId\" IS NOT NULL AND \"VoidReason\" IS NOT NULL)");
        });

        // The printed number comes from a sequence (starts at 100001, see ZatyshokDbContext).
        builder.Property(i => i.Number).HasDefaultValueSql("nextval('billing.\"InvoiceNumbers\"')");

        builder.Property(i => i.Status)
            .HasConversion<string>()
            .HasMaxLength(32);

        builder.Property(i => i.VoidReason).HasMaxLength(500);

        // xmin as the concurrency token (see RoomConfiguration).
        builder.Property(i => i.Version).IsRowVersion();

        builder.HasIndex(i => i.Number).IsUnique();

        // One non-voided invoice per booking (FR-31); also serves the BookingId foreign key.
        builder.HasIndex(i => i.BookingId)
            .IsUnique()
            .HasFilter("\"Status\" <> 'Voided'")
            .HasDatabaseName("UX_Invoices_BookingId_NotVoided");

        // Booking, corporate client and user belong to other modules: only foreign keys (9.4.1, rule 2).
        builder.HasOne<Booking>().WithMany().HasForeignKey(i => i.BookingId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CorporateClient>().WithMany().HasForeignKey(i => i.CorporateClientId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(i => i.VoidedByUserId).OnDelete(DeleteBehavior.Restrict);

        // Items and Payments are read-only collections over the private fields _items and _payments;
        // EF finds the fields by name (relationships: InvoiceItemConfiguration, PaymentConfiguration).
    }
}
