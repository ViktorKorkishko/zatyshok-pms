using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Zatyshok.Infrastructure.Persistence;

/// <summary>
/// Recognizes PostgreSQL errors inside <see cref="DbUpdateException"/>: EF Core wraps the
/// <see cref="PostgresException"/> of SaveChanges, which has the SQLSTATE and the constraint name.
/// Services catch these to replace the database message with a Ukrainian one (HTTP 409).
/// </summary>
public static class PostgresErrors
{
    /// <summary>23505: a unique index rejected the row, e.g. a second room "101" in one hotel (FR-06).</summary>
    public static bool IsUniqueViolation(this DbUpdateException exception, string constraintName)
        => exception.IsUniqueViolation() && ((PostgresException)exception.InnerException!).ConstraintName == constraintName;

    /// <summary>23505 of any unique index: for the general 409 of the API, when a service did not expect it.</summary>
    public static bool IsUniqueViolation(this DbUpdateException exception)
        => exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };

    /// <summary>
    /// 23503 on DELETE: other rows still reference this one through ON DELETE RESTRICT, i.e. the
    /// record has history (bookings, rooms, invoice lines) and may only be deactivated (table 12.12).
    /// </summary>
    public static bool IsForeignKeyViolation(this DbUpdateException exception)
        => exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.ForeignKeyViolation };
}
