namespace Zatyshok.Domain.Modules.Notifications;

/// <summary>Delivery state of an email in the outbox (UC-22 A1, table 12.28).</summary>
public enum OutboxMessageStatus
{
    Pending,
    Sent,

    /// <summary>Final: all attempts failed; the booking card shows that the email was not delivered.</summary>
    Failed,
}
