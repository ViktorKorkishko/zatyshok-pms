using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zatyshok.Domain.Modules.Bookings;
using Zatyshok.Domain.Modules.Housekeeping;
using Zatyshok.Domain.Modules.Identity;
using Zatyshok.Domain.Modules.Rooms;
using Zatyshok.Infrastructure.Persistence;

namespace Zatyshok.Infrastructure.Modules.Housekeeping;

public class HousekeepingTaskConfiguration : IEntityTypeConfiguration<HousekeepingTask>
{
    public void Configure(EntityTypeBuilder<HousekeepingTask> builder)
    {
        builder.ToTable("HousekeepingTasks", Schemas.Housekeeping, t =>
        {
            t.HasEnumCheck<HousekeepingTask, HousekeepingTaskType>("Type");
            t.HasEnumCheck<HousekeepingTask, HousekeepingTaskStatus>("Status");
            t.HasCheckConstraint("CK_HousekeepingTasks_Priority", "\"Priority\" BETWEEN 1 AND 3");
            t.HasCheckConstraint(
                "CK_HousekeepingTasks_Assignee",
                "\"Status\" NOT IN ('Assigned', 'InProgress', 'Done') OR \"AssignedToUserId\" IS NOT NULL");
            t.HasCheckConstraint(
                "CK_HousekeepingTasks_AfterDepartureBooking",
                "\"Type\" <> 'AfterDeparture' OR \"BookingId\" IS NOT NULL");
        });

        builder.Property(t => t.Type)
            .HasConversion<string>()
            .HasMaxLength(32);

        builder.Property(t => t.Status)
            .HasConversion<string>()
            .HasMaxLength(32);

        // Priority is not an enum string but a short (smallint by convention): the housekeeper's
        // list is sorted by it, and sorting strings would be wrong (FR-28, 12.3.5).

        builder.Property(t => t.Notes).HasMaxLength(1000);

        // xmin as the concurrency token (see RoomConfiguration).
        builder.Property(t => t.Version).IsRowVersion();

        // At most one unfinished after-departure task per room: a repeated check-out request
        // cannot create a second one (FR-27).
        builder.HasIndex(t => t.RoomId)
            .IsUnique()
            .HasFilter("\"Type\" = 'AfterDeparture' AND \"Status\" IN ('New', 'Assigned', 'InProgress')")
            .HasDatabaseName("UX_HousekeepingTasks_RoomId_ActiveAfterDeparture");

        // The housekeeper's task list (UC-23) and the tasks of a room.
        builder.HasIndex(t => new { t.AssignedToUserId, t.Status });
        builder.HasIndex(t => new { t.RoomId, t.Status });

        // Room, booking and users belong to other modules: only foreign keys (9.4.1, rule 2).
        builder.HasOne<Room>().WithMany().HasForeignKey(t => t.RoomId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Booking>().WithMany().HasForeignKey(t => t.BookingId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(t => t.AssignedToUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(t => t.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
