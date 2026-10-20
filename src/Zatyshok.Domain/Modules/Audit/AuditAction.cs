namespace Zatyshok.Domain.Modules.Audit;

/// <summary>Audited action (FR-04, UC-02 A1, UC-03, table 12.24).</summary>
public enum AuditAction
{
    Create,
    Update,
    Delete,
    StatusChange,
    Login,
    LoginFailed,
    Lockout,
    PasswordChange,
}
