using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zatyshok.Domain.Modules.Billing;
using Zatyshok.Domain.Modules.Identity;
using Zatyshok.Domain.Modules.Rooms;
using Zatyshok.Infrastructure.Persistence;

namespace Zatyshok.Infrastructure.Modules.Billing;

public class InvoiceItemConfiguration : IEntityTypeConfiguration<InvoiceItem>
{
    public void Configure(EntityTypeBuilder<InvoiceItem> builder)
    {
        builder.ToTable("InvoiceItems", Schemas.Billing, t =>
        {
            t.HasEnumCheck<InvoiceItem, InvoiceItemKind>("Kind");
            t.HasCheckConstraint("CK_InvoiceItems_ServiceRef", "(\"Kind\" = 'Service') = (\"ServiceId\" IS NOT NULL)");
            t.HasCheckConstraint("CK_InvoiceItems_Quantity", "\"Quantity\" > 0");
            t.HasCheckConstraint("CK_InvoiceItems_OneNightPerLine", "\"Kind\" <> 'Accommodation' OR \"Quantity\" = 1");
            t.HasCheckConstraint("CK_InvoiceItems_UnitPrice", "\"UnitPrice\" >= 0");
        });

        builder.Property(i => i.Kind)
            .HasConversion<string>()
            .HasMaxLength(32);

        builder.Property(i => i.Description).HasMaxLength(300);
        builder.Property(i => i.UnitPrice).HasPrecision(12, 2);

        // Generated column: PostgreSQL computes the amount on every INSERT/UPDATE and EF reads it
        // back after SaveChanges. Until then Amount is 0 in memory, so the domain sums Quantity × UnitPrice.
        builder.Property(i => i.Amount)
            .HasPrecision(12, 2)
            .HasComputedColumnSql("\"Quantity\" * \"UnitPrice\"", stored: true);

        // Lines of an invoice. Declared explicitly: otherwise EF would count the partial index below
        // as the foreign key index, but that index contains accommodation lines only.
        builder.HasIndex(i => i.InvoiceId);

        // Recommended in 12.3.6: one accommodation line per night in an invoice. Service lines
        // with the same date are allowed.
        builder.HasIndex(i => new { i.InvoiceId, i.ServiceDate })
            .IsUnique()
            .HasFilter("\"Kind\" = 'Accommodation'")
            .HasDatabaseName("UX_InvoiceItems_InvoiceId_ServiceDate");

        // Revenue by date (FR-36).
        builder.HasIndex(i => i.ServiceDate);

        // Lines are part of the invoice: a line removed from Invoice.Items is deleted.
        builder.HasOne(i => i.Invoice)
            .WithMany(v => v.Items)
            .HasForeignKey(i => i.InvoiceId)
            .OnDelete(DeleteBehavior.Cascade);

        // Service and user belong to other modules: only foreign keys (9.4.1, rule 2).
        builder.HasOne<Service>().WithMany().HasForeignKey(i => i.ServiceId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(i => i.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
