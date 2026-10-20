using Zatyshok.Domain.Common;

namespace Zatyshok.Domain.Modules.Billing;

/// <summary>Invoice line: one night of the stay or a service (table 12.20). Created and changed only by <see cref="Invoice"/>.</summary>
public class InvoiceItem
{
    // For EF Core.
    private InvoiceItem()
    {
    }

    internal InvoiceItem(
        Guid invoiceId,
        InvoiceItemKind kind,
        Guid? serviceId,
        string description,
        DateOnly serviceDate,
        int quantity,
        decimal unitPrice,
        Guid? createdByUserId,
        DateTime nowUtc)
    {
        if (kind == InvoiceItemKind.Service && serviceId is null)
        {
            throw new DomainException("Позиція послуги має посилатися на послугу з довідника.");
        }

        if (string.IsNullOrWhiteSpace(description))
        {
            throw new DomainException("Назва позиції обов'язкова.");
        }

        if (quantity <= 0)
        {
            throw new DomainException("Кількість має бути більшою за нуль.");
        }

        if (unitPrice < 0)
        {
            throw new DomainException("Ціна не може бути від'ємною.");
        }

        Money.EnsureTwoDecimals(unitPrice, "Ціна позиції");

        InvoiceId = invoiceId;
        Kind = kind;
        ServiceId = serviceId;
        Description = description.Trim();
        ServiceDate = serviceDate;
        Quantity = quantity;
        UnitPrice = unitPrice;
        CreatedByUserId = createdByUserId;
        CreatedAtUtc = nowUtc;
    }

    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid InvoiceId { get; private set; }

    public Invoice Invoice { get; private set; } = null!;

    public InvoiceItemKind Kind { get; private set; }

    /// <summary>Catalog service of a Service line; no navigation because Service belongs to the Rooms module.</summary>
    public Guid? ServiceId { get; private set; }

    /// <summary>Name at the time the line was added (a snapshot).</summary>
    public string Description { get; private set; } = null!;

    /// <summary>The night of the stay or the date the service was provided.</summary>
    public DateOnly ServiceDate { get; private set; }

    /// <summary>Always 1 for an accommodation line (CK_InvoiceItems_OneNightPerLine).</summary>
    public int Quantity { get; private set; } = 1;

    public decimal UnitPrice { get; internal set; }

    /// <summary>
    /// Quantity × UnitPrice computed by the database (generated column, 12.3.6). EF Core fills it
    /// after SaveChanges, so before that it is 0 for a new line; code uses Quantity × UnitPrice.
    /// </summary>
    public decimal Amount { get; private set; }

    /// <summary>Who added the line; null — the system (accommodation lines).</summary>
    public Guid? CreatedByUserId { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }
}
