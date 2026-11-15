namespace Zatyshok.Domain.Modules.Housekeeping;

/// <summary>Cleaning task type (FR-27).</summary>
public enum HousekeepingTaskType
{
    /// <summary>After a guest's departure; created by the system on check-out.</summary>
    AfterDeparture,

    /// <summary>Routine cleaning of an occupied room.</summary>
    Routine,

    /// <summary>Deep cleaning; also created when a room returns from repair (an assumption, table 12.26).</summary>
    Deep,
}
