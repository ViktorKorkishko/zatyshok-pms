namespace Zatyshok.Domain.Modules.Bookings;

/// <summary>
/// Record of the booking chronology shown in the booking card (FR-17, UC-17, table 12.14).
/// Example from UC-17: FieldChanged, FieldName = CheckOutDate, OldValue = 2026-10-12,
/// NewValue = 2026-10-13. Records are written only by <see cref="Booking"/> methods.
/// </summary>
public class BookingHistory
{
    // For EF Core.
    private BookingHistory()
    {
    }

    internal BookingHistory(
        Guid bookingId,
        BookingHistoryEventType eventType,
        string? fieldName,
        string? oldValue,
        string? newValue,
        string? comment,
        Guid? authorUserId,
        DateTime occurredAtUtc)
    {
        BookingId = bookingId;
        EventType = eventType;
        FieldName = fieldName;
        OldValue = oldValue;
        NewValue = newValue;
        Comment = comment;
        AuthorUserId = authorUserId;
        OccurredAtUtc = occurredAtUtc;
    }

    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid BookingId { get; private set; }

    public Booking Booking { get; private set; } = null!;

    public DateTime OccurredAtUtc { get; private set; }

    /// <summary>Staff member or guest; null means the system (e.g. a no-show marked by the Worker).</summary>
    public Guid? AuthorUserId { get; private set; }

    public BookingHistoryEventType EventType { get; private set; }

    public string? FieldName { get; private set; }

    public string? OldValue { get; private set; }

    public string? NewValue { get; private set; }

    /// <summary>Explanation: cancellation reason, upgrade decision (UC-12 A1), payment details.</summary>
    public string? Comment { get; private set; }
}
