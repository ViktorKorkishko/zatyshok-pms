using Zatyshok.Domain.Common;

namespace Zatyshok.Domain.Modules.Housekeeping;

/// <summary>
/// Cleaning task for a room (FR-27–FR-29, table 12.17). The hotel is known through the room
/// (3NF), so the task keeps only RoomId. States follow table 12.27. Room, booking and users
/// belong to other modules and are referenced by id only; the T6 services check that the
/// assignee is a housekeeper of the same hotel and move the room Dirty → Free after completion.
/// </summary>
public class HousekeepingTask
{
    public const short HighPriority = 1;
    public const short NormalPriority = 2;
    public const short LowPriority = 3;

    // For EF Core.
    private HousekeepingTask()
    {
    }

    /// <summary>
    /// Creates a task in the New state (table 12.27). AfterDeparture tasks are created by the
    /// system after check-out and point to the booking; a head housekeeper creates Routine or
    /// Deep tasks manually (<paramref name="createdByUserId"/> is the author; null means the system).
    /// </summary>
    public HousekeepingTask(
        Guid roomId,
        HousekeepingTaskType type,
        DateTime nowUtc,
        Guid? bookingId = null,
        Guid? createdByUserId = null,
        short priority = NormalPriority,
        DateTime? dueAtUtc = null,
        string? notes = null)
    {
        if (type == HousekeepingTaskType.AfterDeparture && bookingId is null)
        {
            throw new DomainException("Завдання «після виїзду» має посилатися на бронювання.");
        }

        if (type == HousekeepingTaskType.AfterDeparture && createdByUserId is not null)
        {
            throw new DomainException("Завдання «після виїзду» створює система; вручну створюються поточні та генеральні прибирання.");
        }

        if (priority is < HighPriority or > LowPriority)
        {
            throw new DomainException("Пріоритет завдання — від 1 (високий) до 3 (низький).");
        }

        RoomId = roomId;
        Type = type;
        BookingId = bookingId;
        CreatedByUserId = createdByUserId;
        Priority = priority;
        DueAtUtc = dueAtUtc;
        Notes = notes;
        CreatedAtUtc = nowUtc;
    }

    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid RoomId { get; private set; }

    /// <summary>The booking after whose departure the task was created (FR-27).</summary>
    public Guid? BookingId { get; private set; }

    public HousekeepingTaskType Type { get; private set; }

    public HousekeepingTaskStatus Status { get; private set; } = HousekeepingTaskStatus.New;

    /// <summary>1 — high, 2 — normal, 3 — low; the housekeeper's list is sorted by it (FR-28).</summary>
    public short Priority { get; private set; } = NormalPriority;

    public DateTime? DueAtUtc { get; private set; }

    public Guid? AssignedToUserId { get; private set; }

    /// <summary>Author of a manual task; null — created by the system.</summary>
    public Guid? CreatedByUserId { get; private set; }

    /// <summary>Notes, including a mark about a fault found during cleaning (UC-14 A1).</summary>
    public string? Notes { get; set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime? AssignedAtUtc { get; private set; }

    public DateTime? StartedAtUtc { get; private set; }

    /// <summary>Completion time; used for the KPI "from check-out to a ready room" (БЦ-5).</summary>
    public DateTime? CompletedAtUtc { get; private set; }

    public DateTime? CancelledAtUtc { get; private set; }

    /// <summary>Concurrency token mapped to the PostgreSQL system column xmin (12.3).</summary>
    public uint Version { get; private set; }

    /// <summary>
    /// New → Assigned, or a reassignment that keeps the state (Assigned → Assigned,
    /// InProgress → InProgress, UC-15 A1).
    /// </summary>
    public void Assign(Guid userId, DateTime nowUtc)
    {
        EnsureStatus("призначити виконавця", HousekeepingTaskStatus.New, HousekeepingTaskStatus.Assigned, HousekeepingTaskStatus.InProgress);

        AssignedToUserId = userId;
        AssignedAtUtc = nowUtc;
        if (Status == HousekeepingTaskStatus.New)
        {
            Status = HousekeepingTaskStatus.Assigned;
        }
    }

    /// <summary>Assigned → InProgress: the housekeeper starts (UC-14).</summary>
    public void Start(DateTime nowUtc)
    {
        EnsureStatus("почати", HousekeepingTaskStatus.Assigned);
        Status = HousekeepingTaskStatus.InProgress;
        StartedAtUtc = nowUtc;
    }

    /// <summary>
    /// InProgress → Done (FR-29). Assigned → Done is not allowed: table 6.1 requires the
    /// "in progress" step. A fault found during the work is registered before this call (UC-14 A1).
    /// </summary>
    public void Complete(DateTime nowUtc)
    {
        EnsureStatus("завершити", HousekeepingTaskStatus.InProgress);
        Status = HousekeepingTaskStatus.Done;
        CompletedAtUtc = nowUtc;
    }

    /// <summary>New, Assigned or InProgress → Cancelled by the head housekeeper (UC-14 A2).</summary>
    public void Cancel(DateTime nowUtc)
    {
        EnsureStatus("скасувати", HousekeepingTaskStatus.New, HousekeepingTaskStatus.Assigned, HousekeepingTaskStatus.InProgress);
        Status = HousekeepingTaskStatus.Cancelled;
        CancelledAtUtc = nowUtc;
    }

    private void EnsureStatus(string action, params HousekeepingTaskStatus[] allowed)
    {
        if (!allowed.Contains(Status))
        {
            throw DomainException.InvalidTransition("Завдання на прибирання", Status, action);
        }
    }
}
