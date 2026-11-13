using Zatyshok.Domain.Common;

namespace Zatyshok.Domain.Modules.Billing;

/// <summary>
/// Payment or refund of an invoice (FR-33, table 12.21). Payments are never edited or deleted:
/// a wrong one is voided and no longer counts (UC-18 A2). Created and voided only through <see cref="Invoice"/>.
/// </summary>
public class Payment
{
    // For EF Core.
    private Payment()
    {
    }

    internal Payment(
        Guid invoiceId,
        PaymentKind kind,
        PaymentMethod method,
        decimal amount,
        DateTime paidAtUtc,
        string? note,
        Guid createdByUserId,
        DateTime nowUtc)
    {
        InvoiceId = invoiceId;
        Kind = kind;
        Method = method;
        Amount = amount;
        PaidAtUtc = paidAtUtc;
        Note = note;
        CreatedByUserId = createdByUserId;
        CreatedAtUtc = nowUtc;
    }

    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid InvoiceId { get; private set; }

    public Invoice Invoice { get; private set; } = null!;

    public PaymentKind Kind { get; private set; } = PaymentKind.Payment;

    public PaymentMethod Method { get; private set; }

    /// <summary>Always positive, also for a refund.</summary>
    public decimal Amount { get; private set; }

    /// <summary>Date and time of the payment given by the operator (FR-33).</summary>
    public DateTime PaidAtUtc { get; private set; }

    public string? Note { get; private set; }

    public Guid CreatedByUserId { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime? VoidedAtUtc { get; private set; }

    /// <summary>Accountant who voided the payment (UC-18 A2).</summary>
    public Guid? VoidedByUserId { get; private set; }

    public string? VoidReason { get; private set; }

    public bool IsVoided => VoidedAtUtc is not null;

    internal void Void(string reason, Guid voidedByUserId, DateTime nowUtc)
    {
        if (IsVoided)
        {
            throw new DomainException("Платіж уже анульовано.");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new DomainException("Причина анулювання платежу обов'язкова.");
        }

        VoidedAtUtc = nowUtc;
        VoidedByUserId = voidedByUserId;
        VoidReason = reason.Trim();
    }
}
