namespace Zatyshok.Domain.Modules.Identity;

/// <summary>Account states (table 12.28). Stored as strings in the database.</summary>
public enum UserStatus
{
    /// <summary>A guest registered on the portal but has not confirmed the email yet (UC-01).</summary>
    PendingConfirmation,

    Active,

    /// <summary>Blocked by the administrator (FR-03). A temporary lockout after failed logins is not a state.</summary>
    Blocked,
}
