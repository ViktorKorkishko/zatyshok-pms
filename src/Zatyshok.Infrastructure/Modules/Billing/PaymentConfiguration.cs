using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zatyshok.Domain.Modules.Billing;
using Zatyshok.Domain.Modules.Identity;
using Zatyshok.Infrastructure.Persistence;

namespace Zatyshok.Infrastructure.Modules.Billing;

public class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("Payments", Schemas.Billing, t =>
        {
            t.HasEnumCheck<Payment, PaymentKind>("Kind");
            t.HasEnumCheck<Payment, PaymentMethod>("Method");

            // A refund is positive too; Kind gives the sign.
            t.HasCheckConstraint("CK_Payments_Amount", "\"Amount\" > 0");

            // Time, author and reason of a void are either all filled or all empty (UC-18 A2).
            t.HasCheckConstraint(
                "CK_Payments_Void",
                "(\"VoidedAtUtc\" IS NULL) = (\"VoidedByUserId\" IS NULL) AND (\"VoidedAtUtc\" IS NULL) = (\"VoidReason\" IS NULL)");
        });

        builder.Property(p => p.Kind)
            .HasConversion<string>()
            .HasMaxLength(32);

        builder.Property(p => p.Method)
            .HasConversion<string>()
            .HasMaxLength(32);

        builder.Property(p => p.Amount).HasPrecision(12, 2);
        builder.Property(p => p.Note).HasMaxLength(500);
        builder.Property(p => p.VoidReason).HasMaxLength(500);

        // Payments are never deleted, only voided (12.3.6).
        builder.HasOne(p => p.Invoice)
            .WithMany(v => v.Payments)
            .HasForeignKey(p => p.InvoiceId)
            .OnDelete(DeleteBehavior.Restrict);

        // User belongs to the Identity module: only foreign keys (9.4.1, rule 2).
        builder.HasOne<User>().WithMany().HasForeignKey(p => p.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(p => p.VoidedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
