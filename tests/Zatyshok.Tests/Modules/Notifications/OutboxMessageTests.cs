using Zatyshok.Domain.Common;
using Zatyshok.Domain.Modules.Notifications;

namespace Zatyshok.Tests.Modules.Notifications;

/// <summary>Email delivery states of table 12.28: Pending → Sent, Pending → Pending, Pending → Failed.</summary>
public class OutboxMessageTests
{
    private static readonly DateTime Now = new(2026, 10, 20, 9, 0, 0, DateTimeKind.Utc);

    private static OutboxMessage NewMessage() => new()
    {
        Type = "BookingConfirmed",
        Recipient = "guest@example.com",
        Subject = "Бронювання підтверджено",
        Body = "Дякуємо за бронювання",
    };

    [Fact]
    public void NewMessageIsPendingAndDueNow()
    {
        var before = DateTime.UtcNow;
        var message = NewMessage();

        Assert.Equal(OutboxMessageStatus.Pending, message.Status);
        Assert.Equal(0, message.AttemptCount);
        Assert.InRange(message.NextAttemptAtUtc, before, DateTime.UtcNow);
    }

    [Fact]
    public void SentMessageRecordsTimeAndAttempt()
    {
        var message = NewMessage();

        message.MarkSent(Now);

        Assert.Equal(OutboxMessageStatus.Sent, message.Status);
        Assert.Equal(Now, message.SentAtUtc);
        Assert.Equal(1, message.AttemptCount);
    }

    [Fact]
    public void FailedAttemptsRetryWithGrowingIntervalAndFailAfterTheFifth()
    {
        var message = NewMessage();

        var expectedDelays = new[] { 1, 2, 4, 8 };
        foreach (var minutes in expectedDelays)
        {
            message.MarkAttemptFailed("SMTP timeout", Now);

            Assert.Equal(OutboxMessageStatus.Pending, message.Status);
            Assert.Equal(Now.AddMinutes(minutes), message.NextAttemptAtUtc);
        }

        message.MarkAttemptFailed("SMTP timeout", Now);

        Assert.Equal(OutboxMessageStatus.Failed, message.Status);
        Assert.Equal(5, message.AttemptCount);
        Assert.Equal("SMTP timeout", message.LastError);
        Assert.Null(message.SentAtUtc);
    }

    [Fact]
    public void WorkerSettingsOverrideAttemptsAndDelay()
    {
        var message = NewMessage();

        message.MarkAttemptFailed("error", Now, maxAttempts: 2, retryDelay: TimeSpan.FromSeconds(30));
        Assert.Equal(Now.AddSeconds(30), message.NextAttemptAtUtc);

        message.MarkAttemptFailed("error", Now, maxAttempts: 2, retryDelay: TimeSpan.FromSeconds(30));
        Assert.Equal(OutboxMessageStatus.Failed, message.Status);
    }

    [Fact]
    public void LongErrorIsCutToTheColumnLength()
    {
        var message = NewMessage();

        message.MarkAttemptFailed(new string('x', 3000), Now);

        Assert.Equal(OutboxMessage.MaxErrorLength, message.LastError!.Length);
    }

    [Theory]
    [InlineData(OutboxMessageStatus.Sent)]
    [InlineData(OutboxMessageStatus.Failed)]
    public void SentAndFailedAreFinal(OutboxMessageStatus status)
    {
        var message = NewMessage();
        if (status == OutboxMessageStatus.Sent)
        {
            message.MarkSent(Now);
        }
        else
        {
            message.MarkAttemptFailed("error", Now, maxAttempts: 1);
        }

        Assert.Throws<DomainException>(() => message.MarkSent(Now));
        Assert.Throws<DomainException>(() => message.MarkAttemptFailed("error", Now));
        Assert.Equal(status, message.Status);
    }
}
