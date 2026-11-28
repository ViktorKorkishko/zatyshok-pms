namespace Zatyshok.Domain.Modules.Bookings;

/// <summary>Kind of a booking history record (UC-17, table 12.24).</summary>
public enum BookingHistoryEventType
{
    Created,

    /// <summary>A field changed; FieldName, OldValue and NewValue are filled.</summary>
    FieldChanged,

    /// <summary>The status changed; FieldName is "Status".</summary>
    StatusChanged,

    /// <summary>A payment or refund was recorded in the booking's invoice (event PaymentRecorded, table 9.7).</summary>
    PaymentRecorded,
}
