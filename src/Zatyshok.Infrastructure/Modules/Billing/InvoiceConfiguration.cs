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

        // xmin as the concurrency token (see RoomConfiguration). It protects only changes of the
        // invoice row itself (Status, ClosedAtUtc, voiding): EF checks it in the UPDATE of Invoices.
        // A payment or a line that leaves Status as it was only inserts a child row, the invoice row
        // is not updated and xmin is not checked, so two payments at once could overpay the invoice.
        // Payments and lines are therefore serialized by locking the invoice row first (listing 12.2),
        // a required part of the T6 invoice service. The lock is a statement of its own,
        // SELECT 1 … FOR UPDATE, and the invoice is loaded after it: a FromSql lock query that
        // returns the invoice would load the auto-included Items and Payments in the same statement,
        // as they were before the wait for the lock (InvoiceLockConcurrencyTests).
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
        //
        // Both are always loaded with the invoice, even without Include: the status is derived from
        // their sums (table 12.28), and with empty lists a new line or payment would be saved with a
        // wrong status. Lists use IgnoreAutoIncludes() or Select(...).
        builder.Navigation(i => i.Items).AutoInclude();
        builder.Navigation(i => i.Payments).AutoInclude();
    }
}
