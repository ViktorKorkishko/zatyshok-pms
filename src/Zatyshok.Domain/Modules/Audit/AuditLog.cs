namespace Zatyshok.Domain.Modules.Audit;

/// <summary>
/// Entry of the append-only audit log (FR-04, NFR-10, table 12.23). It has no foreign keys so
/// that it outlives any change of the data; personal values are masked as "***" before writing
/// (12.3.7). The database trigger forbids UPDATE, TRUNCATE and DELETE of entries younger than 3 years.
/// </summary>
public class AuditLog
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>Who did it; null — the system or an anonymous login attempt.</summary>
    public Guid? UserId { get; set; }

    /// <summary>Staff member's name at the time of the action (UC-10); null for guests.</summary>
    public string? UserName { get; set; }

    /// <summary>Hotel the action concerns; a manager sees only their hotel (UC-19).</summary>
    public Guid? HotelId { get; set; }

    public AuditAction Action { get; set; }

    /// <summary>Booking, Invoice, Payment, Room, User, ...</summary>
    public required string EntityType { get; set; }

    public Guid? EntityId { get; set; }

    /// <summary>Changed fields as JSON: {"Status": ["Free", "OutOfOrder"]}.</summary>
    public string? Changes { get; set; }

    /// <summary>Id of the HTTP request that links the entries of one operation.</summary>
    public string? CorrelationId { get; set; }
}
