namespace Zatyshok.Domain.Common;

/// <summary>
/// A domain rule is violated: a forbidden state transition (for example, cancelling a
/// completed booking, FR-16) or a broken invariant of an entity. The API turns it into
/// HTTP 409 with this message, so messages are written in Ukrainian for the end user.
/// </summary>
public class DomainException(string message) : Exception(message)
{
    /// <summary>
    /// Message for a forbidden transition of tables 12.25–12.28, e.g.
    /// "Бронювання: дію «скасувати» заборонено в стані «завершене»." The state is passed by its
    /// Ukrainian name from report 1 (figures 6.1–6.2, table 6.1), not the enum name, because the
    /// message is shown to the user (NFR-13).
    /// </summary>
    public static InvalidTransitionException InvalidTransition(string entity, string stateName, string action)
        => new($"{entity}: дію «{action}» заборонено в стані «{stateName}».");
}
