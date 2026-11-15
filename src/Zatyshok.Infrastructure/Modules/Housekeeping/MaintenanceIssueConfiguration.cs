using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zatyshok.Domain.Modules.Housekeeping;
using Zatyshok.Domain.Modules.Identity;
using Zatyshok.Domain.Modules.Rooms;
using Zatyshok.Infrastructure.Persistence;

namespace Zatyshok.Infrastructure.Modules.Housekeeping;

public class MaintenanceIssueConfiguration : IEntityTypeConfiguration<MaintenanceIssue>
{
    public void Configure(EntityTypeBuilder<MaintenanceIssue> builder)
    {
        builder.ToTable("MaintenanceIssues", Schemas.Housekeeping, t =>
        {
            t.HasCheckConstraint("CK_MaintenanceIssues_Resolution", "(\"ResolvedAtUtc\" IS NULL) = (\"ResolvedByUserId\" IS NULL)");
            t.HasCheckConstraint(
                "CK_MaintenanceIssues_ResolvedAfterReported",
                "\"ResolvedAtUtc\" IS NULL OR \"ResolvedAtUtc\" >= \"ReportedAtUtc\"");
        });

        builder.Property(m => m.Description).HasMaxLength(2000);
        builder.Property(m => m.ResolutionNote).HasMaxLength(1000);

        // At most one open issue per room; a room is OutOfOrder exactly while it has one (FR-30).
        builder.HasIndex(m => m.RoomId)
            .IsUnique()
            .HasFilter("\"ResolvedAtUtc\" IS NULL")
            .HasDatabaseName("UX_MaintenanceIssues_RoomId_Open");

        // The task in which the issue was found belongs to the same module, so a navigation is allowed.
        builder.HasOne(m => m.HousekeepingTask)
            .WithMany()
            .HasForeignKey(m => m.HousekeepingTaskId)
            .OnDelete(DeleteBehavior.Restrict);

        // Room and users belong to other modules: only foreign keys (9.4.1, rule 2).
        builder.HasOne<Room>().WithMany().HasForeignKey(m => m.RoomId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(m => m.ReportedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(m => m.ResolvedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
