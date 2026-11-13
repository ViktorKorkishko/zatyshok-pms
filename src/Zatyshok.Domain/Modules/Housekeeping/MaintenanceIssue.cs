using Zatyshok.Domain.Common;

namespace Zatyshok.Domain.Modules.Housekeeping;

/// <summary>
/// Fault of a room (FR-30, UC-16, table 12.18). It is open until resolved; a room is
/// OutOfOrder exactly while it has an open fault, and the T6 service changes both in one
/// transaction (<c>Room.MarkOutOfOrder</c> / <c>Room.ReturnToService</c>).
/// </summary>
public class MaintenanceIssue
{
    // For EF Core.
    private MaintenanceIssue()
    {
    }

    /// <summary>Registers a fault found by a housekeeper, head housekeeper or manager, optionally during a cleaning task (UC-14 A1).</summary>
    public MaintenanceIssue(Guid roomId, string description, Guid reportedByUserId, DateTime nowUtc, Guid? housekeepingTaskId = null)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            throw new DomainException("Опишіть несправність.");
        }

        RoomId = roomId;
        Description = description.Trim();
        ReportedByUserId = reportedByUserId;
        ReportedAtUtc = nowUtc;
        HousekeepingTaskId = housekeepingTaskId;
    }

    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid RoomId { get; private set; }

    public Guid? HousekeepingTaskId { get; private set; }

    public HousekeepingTask? HousekeepingTask { get; private set; }

    public string Description { get; private set; } = null!;

    public Guid ReportedByUserId { get; private set; }

    public DateTime ReportedAtUtc { get; private set; }

    /// <summary>Head housekeeper or manager who returned the room to service (FR-30).</summary>
    public Guid? ResolvedByUserId { get; private set; }

    public DateTime? ResolvedAtUtc { get; private set; }

    public string? ResolutionNote { get; private set; }

    public bool IsOpen => ResolvedAtUtc is null;

    /// <summary>Open → resolved: the room returns to service (OutOfOrder → Dirty) and gets a deep cleaning task.</summary>
    public void Resolve(Guid resolvedByUserId, string? resolutionNote, DateTime nowUtc)
    {
        if (!IsOpen)
        {
            throw new DomainException("Несправність уже усунуто.");
        }

        if (nowUtc < ReportedAtUtc)
        {
            throw new DomainException("Час усунення не може бути ранішим за час виявлення несправності.");
        }

        ResolvedByUserId = resolvedByUserId;
        ResolvedAtUtc = nowUtc;
        ResolutionNote = resolutionNote;
    }
}
