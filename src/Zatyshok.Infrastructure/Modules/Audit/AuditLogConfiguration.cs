using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zatyshok.Domain.Modules.Audit;
using Zatyshok.Infrastructure.Persistence;

namespace Zatyshok.Infrastructure.Modules.Audit;

/// <summary>
/// The audit log has no foreign keys on purpose: a record must outlive any change or deletion
/// of the entity it describes and must not block anonymization (12.3.7). Changes to existing
/// records are blocked by a trigger (migration ReportingViewsAndAuditGuard, listing 12.5).
/// </summary>
public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("AuditLogs", Schemas.Audit, t => t.HasEnumCheck<AuditLog, AuditAction>("Action"));

        builder.Property(a => a.UserName).HasMaxLength(300);

        builder.Property(a => a.Action)
            .HasConversion<string>()
            .HasMaxLength(32);

        builder.Property(a => a.EntityType).HasMaxLength(100);

        // Changed fields as JSON: {"Status": ["Free", "OutOfOrder"]}.
        builder.Property(a => a.Changes).HasColumnType("jsonb");

        builder.Property(a => a.CorrelationId).HasMaxLength(64);

        // Filters of the audit log view (FR-04): by period, by user, by entity, by hotel (UC-19).
        builder.HasIndex(a => a.OccurredAtUtc);
        builder.HasIndex(a => new { a.UserId, a.OccurredAtUtc });
        builder.HasIndex(a => new { a.EntityType, a.EntityId });
        builder.HasIndex(a => new { a.HotelId, a.OccurredAtUtc });
    }
}
