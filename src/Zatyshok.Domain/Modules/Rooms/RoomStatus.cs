namespace Zatyshok.Domain.Modules.Rooms;

/// <summary>Room states per figure 6.2 of docs/03. Stored as strings in the database.</summary>
public enum RoomStatus
{
    Free,

    /// <summary>Pre-assigned to a booking arriving today.</summary>
    Reserved,

    Occupied,

    /// <summary>Needs cleaning after check-out.</summary>
    Dirty,

    /// <summary>Under repair; excluded from availability (FR-09, FR-30).</summary>
    OutOfOrder,
}
