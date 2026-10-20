using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zatyshok.Domain.Modules.Guests;
using Zatyshok.Infrastructure.Persistence;

namespace Zatyshok.Infrastructure.Modules.Guests;

public class CorporateClientConfiguration : IEntityTypeConfiguration<CorporateClient>
{
    public void Configure(EntityTypeBuilder<CorporateClient> builder)
    {
        builder.ToTable("CorporateClients", Schemas.Guests, t =>
        {
            // 8 digits is an assumption (12.3.4); another format needs a new migration.
            t.HasCheckConstraint("CK_CorporateClients_Edrpou", "\"Edrpou\" ~ '^[0-9]{8}$'");
            t.HasCheckConstraint(
                "CK_CorporateClients_Contract",
                "\"ContractStartDate\" IS NULL OR \"ContractEndDate\" IS NULL OR \"ContractEndDate\" >= \"ContractStartDate\"");
            t.HasCheckConstraint("CK_CorporateClients_PaymentDeferralDays", "\"PaymentDeferralDays\" >= 0");
        });

        builder.Property(c => c.Name).HasMaxLength(300).UseCollation(Collations.Ukrainian);
        builder.Property(c => c.Edrpou).HasMaxLength(8);
        builder.Property(c => c.ContractNumber).HasMaxLength(50);
        builder.Property(c => c.ContactPerson).HasMaxLength(200);
        builder.Property(c => c.ContactPhone).HasMaxLength(32);
        builder.Property(c => c.ContactEmail).HasMaxLength(256);

        builder.HasIndex(c => c.Edrpou).IsUnique();
    }
}
