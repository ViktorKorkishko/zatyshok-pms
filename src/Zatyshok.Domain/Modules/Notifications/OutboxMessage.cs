using Zatyshok.Domain.Common;

namespace Zatyshok.Domain.Modules.Notifications;

/// <summary>
/// Email queued in the same transaction as the business change and sent later by the Worker
/// (transactional outbox, FR-39, table 12.22). The delivery state changes only through
/// <see cref="MarkSent"/> and <see cref="MarkAttemptFailed"/> (table 12.28).
/// </summary>
public class OutboxMessage
{
    /// <summary>Attempts before the message fails for good (UC-22 A1).</summary>
    public const int DefaultMaxAttempts = 5;

    /// <summary>Length of the LastError column; longer error texts are cut.</summary>
    public const int MaxErrorLength = 2000;

    /// <summary>Delay after the first failure; it doubles after each next one.</summary>
    public static readonly TimeSpan DefaultRetryDelay = TimeSpan.FromMinutes(1);

    // For EF Core.
    private OutboxMessage()
    {
    }

    /// <summary>
    /// Queues an email in the Pending state, due at once (table 12.28). <paramref name="nowUtc"/>
    /// is the time of the business change, passed by the service as for the other entities.
    /// </summary>
    public OutboxMessage(
        string type,
        string recipient,
        string subject,
        string body,
        DateTime nowUtc,
        string? relatedEntityType = null,
        Guid? relatedEntityId = null,
        string? deduplicationKey = null)
    {
        Type = type;
        Recipient = recipient;
        Subject = subject;
        Body = body;
        RelatedEntityType = relatedEntityType;
        RelatedEntityId = relatedEntityId;
        DeduplicationKey = deduplicationKey;
        CreatedAtUtc = nowUtc;
        NextAttemptAtUtc = nowUtc;
    }

    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Kind of the email, e.g. BookingConfirmed, ArrivalReminder, PasswordReset.</summary>
    public string Type { get; private set; } = null!;

    public string Recipient { get; private set; } = null!;

    public string Subject { get; private set; } = null!;

    /// <summary>Text from the template (UC-22); cleared after sending for emails with tokens (12.8.5).</summary>
    public string Body { get; set; } = null!;

    public OutboxMessageStatus Status { get; private set; } = OutboxMessageStatus.Pending;

    public int AttemptCount { get; private set; }

    /// <summary>The Worker picks Pending messages whose time has come.</summary>
    public DateTime NextAttemptAtUtc { get; private set; }

    public string? LastError { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime? SentAtUtc { get; private set; }

    /// <summary>Type of the related entity, e.g. Booking; a logical link without a foreign key.</summary>
    public string? RelatedEntityType { get; private set; }

    public Guid? RelatedEntityId { get; private set; }

    /// <summary>Unique key against duplicates, e.g. "ArrivalReminder:{BookingId}:{CheckInDate}" (FR-40).</summary>
    public string? DeduplicationKey { get; private set; }

    /// <summary>Pending → Sent: the mail server accepted the message.</summary>
    public void MarkSent(DateTime nowUtc)
    {
        EnsurePending("позначити надісланим");
        AttemptCount++;
        Status = OutboxMessageStatus.Sent;
        SentAtUtc = nowUtc;
    }

    /// <summary>
    /// Pending → Pending with the next attempt later (delay, 2 × delay, 4 × delay, …), or
    /// Pending → Failed after the last attempt (UC-22 A1). The number of attempts and the delay
    /// are Worker settings (12.3.2), so the Worker may pass its configured values.
    /// </summary>
    public void MarkAttemptFailed(string error, DateTime nowUtc, int maxAttempts = DefaultMaxAttempts, TimeSpan? retryDelay = null)
    {
        EnsurePending("зареєструвати невдалу спробу");
        AttemptCount++;
        LastError = error.Length <= MaxErrorLength ? error : error[..MaxErrorLength];

        if (AttemptCount >= maxAttempts)
        {
            Status = OutboxMessageStatus.Failed;
            return;
        }

        var delay = retryDelay ?? DefaultRetryDelay;
        NextAttemptAtUtc = nowUtc + delay * Math.Pow(2, AttemptCount - 1);
    }

    private void EnsurePending(string action)
    {
        if (Status != OutboxMessageStatus.Pending)
        {
            throw DomainException.InvalidTransition("Лист", StatusName(Status), action);
        }
    }

    // State names for error messages (NFR-13).
    private static string StatusName(OutboxMessageStatus status) => status switch
    {
        OutboxMessageStatus.Pending => "очікує надсилання",
        OutboxMessageStatus.Sent => "надіслано",
        OutboxMessageStatus.Failed => "не доставлено",
        _ => status.ToString(),
    };
}
