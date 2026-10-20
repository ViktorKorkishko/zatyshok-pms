using Zatyshok.Domain.Common;

namespace Zatyshok.Domain.Modules.Billing;

/// <summary>
/// Invoice of a stay with its lines and payments (table 12.19, table 12.28). It is opened at the
/// first prepayment or at check-in, always with one accommodation line per booking night
/// (12.3.6). The payment state is derived from the sums after every change of a line or payment;
/// Voided is final. The booking belongs to another module, so the invoice keeps BookingId only
/// and receives the nights as <see cref="NightPrice"/> values.
/// </summary>
public class Invoice
{
    /// <summary>Text of an accommodation line; the night itself is in <see cref="InvoiceItem.ServiceDate"/>.</summary>
    public const string AccommodationDescription = "Проживання";

    // The collections change only through the methods below, which recalculate the status.
    // EF Core reads and writes these fields directly.
    private readonly List<InvoiceItem> _items = new();
    private readonly List<Payment> _payments = new();

    // For EF Core.
    private Invoice()
    {
    }

    /// <summary>Opens an invoice with an accommodation line for every night of the booking (FR-31 as refined in 12.3.6).</summary>
    public Invoice(Guid bookingId, Guid? corporateClientId, IEnumerable<NightPrice> nights, DateTime nowUtc)
    {
        BookingId = bookingId;
        CorporateClientId = corporateClientId;
        OpenedAtUtc = nowUtc;
        SyncAccommodationLines(nights, nowUtc);
        RecalculateStatus();
    }

    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Printed number (FR-34); the database assigns it from the InvoiceNumbers sequence.</summary>
    public long Number { get; private set; }

    public Guid BookingId { get; private set; }

    /// <summary>Organization that pays (FR-26).</summary>
    public Guid? CorporateClientId { get; private set; }

    public InvoiceStatus Status { get; private set; } = InvoiceStatus.Open;

    public DateTime OpenedAtUtc { get; private set; }

    /// <summary>Set at check-out (FR-20); after that no new lines are added. The payment state does not change on closing (UC-13 A2).</summary>
    public DateTime? ClosedAtUtc { get; private set; }

    public DateTime? VoidedAtUtc { get; private set; }

    /// <summary>Accountant who voided the invoice (table 6.1).</summary>
    public Guid? VoidedByUserId { get; private set; }

    public string? VoidReason { get; private set; }

    /// <summary>Concurrency token mapped to the PostgreSQL system column xmin (12.3).</summary>
    public uint Version { get; private set; }

    public IReadOnlyCollection<InvoiceItem> Items => _items;

    public IReadOnlyCollection<Payment> Payments => _payments;

    public bool IsClosed => ClosedAtUtc is not null;

    /// <summary>
    /// T — sum of the lines. Uses Quantity × UnitPrice rather than <see cref="InvoiceItem.Amount"/>,
    /// because Amount is filled by the database only when the line is saved.
    /// </summary>
    public decimal GetTotal() => _items.Sum(i => i.Quantity * i.UnitPrice);

    /// <summary>P — active payments minus active refunds (table 12.28).</summary>
    public decimal GetPaidAmount()
        => _payments.Where(p => !p.IsVoided).Sum(p => p.Kind == PaymentKind.Payment ? p.Amount : -p.Amount);

    /// <summary>T − P: what the guest still owes; negative when the invoice is overpaid and a refund is due.</summary>
    public decimal GetBalance() => GetTotal() - GetPaidAmount();

    /// <summary>
    /// Adds a service line from the catalog (FR-32). The catalog price may be overridden by the
    /// operator; the audit log records it. Accommodation lines are managed by <see cref="SyncAccommodation"/>.
    /// </summary>
    public InvoiceItem AddItem(
        Guid serviceId,
        string description,
        DateOnly serviceDate,
        int quantity,
        decimal unitPrice,
        Guid? createdByUserId,
        DateTime nowUtc)
    {
        EnsureNotVoided("додати позицію");
        EnsureNotClosed();

        var item = new InvoiceItem(Id, InvoiceItemKind.Service, serviceId, description, serviceDate, quantity, unitPrice, createdByUserId, nowUtc);
        _items.Add(item);
        RecalculateStatus();
        return item;
    }

    /// <summary>Removes a wrong service line (UC-18); if the invoice becomes overpaid, the difference is returned with a refund.</summary>
    public void RemoveItem(Guid itemId)
    {
        EnsureNotVoided("видалити позицію");

        var item = _items.SingleOrDefault(i => i.Id == itemId)
            ?? throw new DomainException("Позицію не знайдено в цьому рахунку.");
        if (item.Kind == InvoiceItemKind.Accommodation)
        {
            throw new DomainException("Позиції проживання змінюються лише разом з ночами бронювання.");
        }

        _items.Remove(item);
        RecalculateStatus();
    }

    /// <summary>
    /// Makes the accommodation lines match the booking nights (12.3.6): adds lines for new nights,
    /// removes lines of nights that are gone and updates changed prices. Called at check-in and when
    /// the dates change (UC-06, FR-21, UC-13 A1). Service lines are not touched.
    /// </summary>
    public void SyncAccommodation(IEnumerable<NightPrice> nights, DateTime nowUtc)
    {
        EnsureNotVoided("звірити позиції проживання");
        EnsureNotClosed();

        SyncAccommodationLines(nights, nowUtc);
        RecalculateStatus();
    }

    /// <summary>
    /// Records a payment or refund (FR-33). A payment above the balance is rejected (UC-18 A1);
    /// a refund cannot exceed what was paid. Allowed after closing: a debt may be paid later.
    /// </summary>
    public Payment AddPayment(
        PaymentKind kind,
        PaymentMethod method,
        decimal amount,
        DateTime paidAtUtc,
        string? note,
        Guid createdByUserId,
        DateTime nowUtc)
    {
        EnsureNotVoided("внести платіж");
        if (amount <= 0)
        {
            throw new DomainException("Сума платежу має бути більшою за нуль.");
        }

        if (kind == PaymentKind.Payment && amount > GetBalance())
        {
            throw new DomainException("Сума платежу перевищує залишок за рахунком.");
        }

        if (kind == PaymentKind.Refund && amount > GetPaidAmount())
        {
            throw new DomainException("Сума повернення перевищує сплачену суму.");
        }

        var payment = new Payment(Id, kind, method, amount, paidAtUtc, note, createdByUserId, nowUtc);
        _payments.Add(payment);
        RecalculateStatus();
        return payment;
    }

    /// <summary>Voids a wrong payment or refund (UC-18 A2); it no longer counts in P.</summary>
    public void VoidPayment(Guid paymentId, string reason, Guid voidedByUserId, DateTime nowUtc)
    {
        EnsureNotVoided("анулювати платіж");

        var payment = _payments.SingleOrDefault(p => p.Id == paymentId)
            ?? throw new DomainException("Платіж не знайдено в цьому рахунку.");

        // Otherwise the refunds would exceed the remaining payments.
        if (!payment.IsVoided && payment.Kind == PaymentKind.Payment && GetPaidAmount() - payment.Amount < 0)
        {
            throw new DomainException("Спершу анулюйте повернення: без цього платежу повернення перевищать сплачену суму.");
        }

        payment.Void(reason, voidedByUserId, nowUtc);
        RecalculateStatus();
    }

    /// <summary>
    /// Derives the state from the sums (table 12.28): Open if P = 0, PartiallyPaid if 0 &lt; P &lt; T,
    /// Paid if P ≥ T. A voided invoice keeps its state. The methods of this class call it after every
    /// change; it is public for the service that recalculates under the invoice row lock (listing 12.2).
    /// </summary>
    public void RecalculateStatus()
    {
        if (Status == InvoiceStatus.Voided)
        {
            return;
        }

        var total = GetTotal();
        var paid = GetPaidAmount();
        Status = paid <= 0 ? InvoiceStatus.Open
            : paid < total ? InvoiceStatus.PartiallyPaid
            : InvoiceStatus.Paid;
    }

    /// <summary>Closes the invoice for new lines at check-out (FR-20). The payment state stays as it is.</summary>
    public void Close(DateTime nowUtc)
    {
        EnsureNotVoided("закрити");
        if (IsClosed)
        {
            throw new DomainException("Рахунок уже закрито.");
        }

        ClosedAtUtc = nowUtc;
    }

    /// <summary>Any state → Voided by an accountant with a reason (table 6.1, UC-18 A2).</summary>
    public void Void(string reason, Guid voidedByUserId, DateTime nowUtc)
    {
        EnsureNotVoided("анулювати");
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new DomainException("Причина анулювання рахунку обов'язкова.");
        }

        Status = InvoiceStatus.Voided;
        VoidedAtUtc = nowUtc;
        VoidedByUserId = voidedByUserId;
        VoidReason = reason.Trim();
    }

    private void SyncAccommodationLines(IEnumerable<NightPrice> nights, DateTime nowUtc)
    {
        var prices = new Dictionary<DateOnly, decimal>();
        foreach (var night in nights)
        {
            if (night.Price < 0)
            {
                throw new DomainException($"Ціна ночі {night.Night:yyyy-MM-dd} не може бути від'ємною.");
            }

            if (!prices.TryAdd(night.Night, night.Price))
            {
                throw new DomainException($"Ніч {night.Night:yyyy-MM-dd} указано двічі.");
            }
        }

        // A booking has at least one night, so a live invoice always has T > 0 (12.3.6).
        if (prices.Count == 0)
        {
            throw new DomainException("Рахунок має містити позиції проживання: потрібна щонайменше одна ніч.");
        }

        // Lines of the nights that stay are kept and only repriced.
        _items.RemoveAll(i => i.Kind == InvoiceItemKind.Accommodation && !prices.ContainsKey(i.ServiceDate));
        foreach (var item in _items.Where(i => i.Kind == InvoiceItemKind.Accommodation))
        {
            item.UnitPrice = prices[item.ServiceDate];
        }

        var billedNights = _items.Where(i => i.Kind == InvoiceItemKind.Accommodation).Select(i => i.ServiceDate).ToHashSet();
        foreach (var (night, price) in prices.Where(p => !billedNights.Contains(p.Key)).OrderBy(p => p.Key))
        {
            _items.Add(new InvoiceItem(Id, InvoiceItemKind.Accommodation, null, AccommodationDescription, night, 1, price, null, nowUtc));
        }
    }

    private void EnsureNotVoided(string action)
    {
        if (Status == InvoiceStatus.Voided)
        {
            throw DomainException.InvalidTransition("Рахунок", Status, action);
        }
    }

    private void EnsureNotClosed()
    {
        if (IsClosed)
        {
            throw new DomainException("Рахунок закрито під час виселення: нові позиції не додаються.");
        }
    }
}
