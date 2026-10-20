namespace Zatyshok.Domain.Modules.Housekeeping;

/// <summary>Cleaning task states (table 6.1, table 12.27). Stored as strings in the database.</summary>
public enum HousekeepingTaskStatus
{
    New,
    Assigned,
    InProgress,
    Done,
    Cancelled,
}
