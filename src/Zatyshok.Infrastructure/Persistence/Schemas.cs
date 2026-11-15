namespace Zatyshok.Infrastructure.Persistence;

/// <summary>
/// PostgreSQL schema of each module (report 2, table 12.1). One database and one
/// <see cref="ZatyshokDbContext"/>, but every module keeps its tables in its own schema.
/// The EF migrations history table stays in <c>public</c>.
/// </summary>
public static class Schemas
{
    public const string Identity = "identity";

    public const string Rooms = "rooms";

    public const string Guests = "guests";

    public const string Bookings = "bookings";

    public const string Housekeeping = "housekeeping";

    public const string Billing = "billing";

    public const string Notifications = "notifications";

    public const string Audit = "audit";

    /// <summary>Only views for the Reporting module (12.8.3); created by SQL in a migration, not by the EF model.</summary>
    public const string Reporting = "reporting";
}
