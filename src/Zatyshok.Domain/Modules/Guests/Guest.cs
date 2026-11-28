namespace Zatyshok.Domain.Modules.Guests;

/// <summary>
/// Guest profile (FR-23, table 12.15). A profile created by an operator has no account
/// (<see cref="UserId"/> is null). Personal data is masked in the audit log and replaced
/// with placeholders on anonymization (UC-09, NFR-09).
/// </summary>
public class Guest
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Portal account; no navigation because User belongs to the Identity module.</summary>
    public Guid? UserId { get; set; }

    public required string LastName { get; set; }

    public required string FirstName { get; set; }

    public string? MiddleName { get; set; }

    public DateOnly? BirthDate { get; set; }

    /// <summary>ISO 3166-1 alpha-2 country code, e.g. "UA" (the format is an assumption, 12.3.4).</summary>
    public string? Citizenship { get; set; }

    /// <summary>Set together with <see cref="DocumentNumber"/> (CK_Guests_Document).</summary>
    public DocumentType? DocumentType { get; set; }

    /// <summary>Required at check-in (FR-23, UC-12 A3); visible to reception and managers only (NFR-09).</summary>
    public string? DocumentNumber { get; set; }

    public string? Phone { get; set; }

    /// <summary>Email for notifications; without it no letter is queued (UC-22 A2).</summary>
    public string? Email { get; set; }

    /// <summary>Standing wishes: floor, bed type.</summary>
    public string? Preferences { get; set; }

    /// <summary>Staff notes, never shown to the guest.</summary>
    public string? StaffNotes { get; set; }

    public DateTime? PersonalDataConsentAtUtc { get; set; }

    public DateTime? AnonymizedAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAtUtc { get; set; }

    /// <summary>
    /// Document data needed for check-in (FR-23). The check-in service of FrontDesk asks the
    /// Guests module for it (figure 9.12) before calling <c>Booking.CheckIn</c>.
    /// </summary>
    public bool HasDocument() => DocumentType is not null && !string.IsNullOrWhiteSpace(DocumentNumber);
}
